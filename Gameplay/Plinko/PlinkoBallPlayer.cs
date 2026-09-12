using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Plays one recorded <see cref="PlinkoPath"/> back by moving a transform along its ballistic arcs.
    /// </summary>
    /// <remarks>
    /// Playback reads the recording and nothing else. It never decides anything: the catch point was fixed
    /// when the drop was simulated, so the position maths here cannot change where the token lands even if it
    /// drifts. Time is accumulated into a single elapsed value rather than chained frame to frame, so a
    /// stutter or a framerate change cannot desynchronise the drop.
    /// </remarks>
    public class PlinkoBallPlayer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Board the path came from. Supplies the board to world projection.")]
        [SerializeField] private PlinkoBoard _board;

        [Tooltip("Transform to move. Leave empty to move this GameObject.")]
        [SerializeField] private Transform _ball;

        [Header("Playback")]
        [Tooltip("Playback rate. 1 is the duration the simulation produced; raise it to speed drops up.")]
        [Min(0.01f)]
        [SerializeField] private float _timeScale = 1f;

        [Tooltip("Use unscaled time, so the drop still plays while the game is paused.")]
        [SerializeField] private bool _useUnscaledTime;

        [Tooltip("Hide the token while no drop is playing.")]
        [SerializeField] private bool _hideWhenIdle = true;

        [Tooltip("Spin the token as it falls, proportional to its horizontal speed.")]
        [SerializeField] private bool _spinWhileFalling = true;

        [Tooltip("Degrees of spin per board unit travelled horizontally.")]
        [SerializeField] private float _spinPerUnit = 220f;

        private PlinkoPath _path;
        private float _elapsed;
        private int _nextContactIndex;
        private float _spin;
        private Transform _target;

        /// <summary>Raised for each peg struck, with the world position of the contact and the impact speed.</summary>
        public event Action<Vector3, float> PegContacted;

        /// <summary>Raised for each wall or side shape struck, with the world position and the impact speed.</summary>
        public event Action<Vector3, float> WallContacted;

        /// <summary>Raised once the token reaches its catch point, with that catch point's index.</summary>
        public event Action<int> TokenLanded;

        /// <summary>True while a drop is playing.</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>The drop currently playing, or <c>null</c>.</summary>
        public PlinkoPath CurrentPath => _path;

        /// <summary>How far the current drop has progressed, from 0 to 1.</summary>
        public float Progress =>
            _path == null || _path.Duration <= 0f ? 0f : Mathf.Clamp01(_elapsed / _path.Duration);

        private void Awake()
        {
            _target = _ball != null ? _ball : transform;

            if (_hideWhenIdle)
            {
                _target.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Starts playing a recorded drop from the beginning.
        /// </summary>
        /// <param name="path">Drop to play. Passing <c>null</c> stops playback.</param>
        public void Play(PlinkoPath path)
        {
            _path = path;
            _elapsed = 0f;
            _nextContactIndex = 0;
            _spin = 0f;
            IsPlaying = path != null;

            if (path == null)
            {
                return;
            }

            _target.gameObject.SetActive(true);
            _target.position = ToWorld(path.Evaluate(0f));
        }

        /// <summary>
        /// Asks the board for a drop to the given catch point and plays it.
        /// </summary>
        /// <param name="entryIndex">Entry to drop from.</param>
        /// <param name="catchPointIndex">Catch point the token must land in.</param>
        /// <returns><c>true</c> when a path was found and playback started.</returns>
        public bool Drop(int entryIndex, int catchPointIndex)
        {
            if (_board == null)
            {
                Debug.LogWarning("No board assigned, so nothing can be dropped.", this);
                return false;
            }

            PlinkoPath path = _board.GetPath(entryIndex, catchPointIndex);
            if (path == null)
            {
                return false;
            }

            Play(path);
            return true;
        }

        /// <summary>
        /// Stops playback and leaves the token where it is.
        /// </summary>
        public void Stop()
        {
            IsPlaying = false;

            if (_hideWhenIdle && _target != null)
            {
                _target.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsPlaying || _path == null)
            {
                return;
            }

            float delta = (_useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * _timeScale;
            Vector2 previous = _path.Evaluate(_elapsed);
            _elapsed += delta;
            Vector2 current = _path.Evaluate(_elapsed);

            _target.position = ToWorld(current);

            if (_spinWhileFalling)
            {
                _spin -= (current.x - previous.x) * _spinPerUnit;
                _target.localRotation = Quaternion.Euler(0f, 0f, _spin);
            }

            RaiseReachedContacts();

            if (_elapsed >= _path.Duration)
            {
                IsPlaying = false;
                TokenLanded?.Invoke(_path.CatchPointIndex);
            }
        }

        private void RaiseReachedContacts()
        {
            var contacts = _path.Contacts;

            while (_nextContactIndex < contacts.Count && _elapsed >= contacts[_nextContactIndex].Time)
            {
                PlinkoContact contact = contacts[_nextContactIndex];
                Vector3 world = ToWorld(contact.Position);

                if (contact.Kind == PlinkoTraceOutcome.Wall)
                {
                    WallContacted?.Invoke(world, contact.ImpactSpeed);
                }
                else
                {
                    PegContacted?.Invoke(world, contact.ImpactSpeed);
                }

                _nextContactIndex++;
            }
        }

        private Vector3 ToWorld(Vector2 boardPosition)
        {
            return _board != null
                ? _board.BoardToWorld(boardPosition)
                : new Vector3(boardPosition.x, boardPosition.y, 0f);
        }
    }
}
