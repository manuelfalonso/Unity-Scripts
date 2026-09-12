using SombraStudios.Shared.Extensions;
using System;
using UnityEngine;
using UnityEngine.Splines;

namespace SombraStudios.Shared.Splines
{
    /// <summary>
    /// Animates objects using Unity Splines package and Animation Curves
    /// Works either with Objects and UI
    /// Requieres:
    /// UnityEngine.Splines
    /// Documentation: https://docs.unity3d.com/Packages/com.unity.splines@2.4/manual/index.html
    /// </summary>
    [RequireComponent(typeof(SplineAnimate))]
    public class SplineController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The spline this object travels along.")]
        [SerializeField] private SplineContainer _splineContainer = null;

        [Tooltip("The animator driving the movement. Resolved from this GameObject when left empty.")]
        [SerializeField] private SplineAnimate _splineAnimation = null;

        [Header("Scale")]
        [Tooltip("Drive the object's local scale from the curve below while it travels.")]
        [SerializeField] private bool _animateScale = false;

        [Tooltip("Scale over the animation's normalized time. The curve's own time range is remapped onto 0-1.")]
        [SerializeField] private AnimationCurve _scaleCurve = null;

        [Header("Debug")]
        [Tooltip("Log every animation event to the console.")]
        [SerializeField] private bool _showLogs = false;

        [Tooltip("When off, Play, Stop and Reset are ignored.")]
        [SerializeField] private bool _isActive = true;

        /// <summary>
        /// Gets or sets whether this controller responds to play, stop and reset requests.
        /// </summary>
        public bool IsActive { get => _isActive; set => _isActive = value; }

        /// <summary>
        /// Raised after the animation starts.
        /// </summary>
        public event Action AnimationStarted;

        /// <summary>
        /// Raised on every animation step, carrying the current position, rotation and local scale.
        /// </summary>
        public event Action<Vector3, Quaternion, Vector3> AnimationUpdated;

        /// <summary>
        /// Raised after the animation is paused.
        /// </summary>
        public event Action AnimationStopped;

        /// <summary>
        /// Raised after the animation is restarted from the beginning.
        /// </summary>
        public event Action AnimationReset;

        /// <summary>
        /// Raised once the animation reaches the end of the spline.
        /// </summary>
        public event Action AnimationCompleted;


        private void Awake()
        {
            this.EnsureComponent(ref _splineAnimation);
        }

        private void OnEnable()
        {
            if (_splineAnimation == null) { return; }
            _splineAnimation.Updated -= OnAnimationUpdated;
            _splineAnimation.Updated += OnAnimationUpdated;
        }

        private void Start()
        {
            if (_splineAnimation == null) { return; }
            if (_splineAnimation.PlayOnAwake) { OnAnimationStarted(); }
        }

        private void OnDisable()
        {
            if (_splineAnimation == null) { return; }
            _splineAnimation.Updated -= OnAnimationUpdated;
        }


        /// <summary>
        /// Plays current Animation
        /// </summary>
        public void PlayAnimation()
        {
            if (_splineAnimation == null) { return; }
            if (!_isActive) { return; }
            _splineAnimation.Play();
            OnAnimationStarted();
        }

        /// <summary>
        /// Plays current Animation from the normalized <paramref name="progressionValue"/> time
        /// </summary>
        /// <param name="progressionValue">Where to start, from 0 at the spline's beginning to 1 at its end.</param>
        public void PlayAnimation(float progressionValue)
        {
            if (_splineAnimation == null) { return; }
            if (!_isActive) { return; }
            if (progressionValue < 0 || progressionValue > 1)
            {
                Debug.LogWarning("Progression value must be normalized between 0 and 1.", this);
                return;
            }
            _splineAnimation.NormalizedTime = progressionValue;
            _splineAnimation.Play();
            OnAnimationStarted();
        }

        /// <summary>
        /// Stops current Animation
        /// </summary>
        public void StopAnimation()
        {
            if (_splineAnimation == null) { return; }
            if (!_isActive) { return; }
            _splineAnimation.Pause();
            OnAnimationStopped();
        }

        /// <summary>
        /// Resets current Animation
        /// </summary>
        public void ResetAnimation()
        {
            if (_splineAnimation == null) { return; }
            if (!_isActive) { return; }
            _splineAnimation.Restart(false);
            OnAnimationReset();
        }


        private void UpdateScale()
        {
            if (!_animateScale) { return; }
            if (_scaleCurve == null || _scaleCurve.keys.Length == 0) { return; }

            // The curve's own time range is remapped onto the animation's normalized time, so a curve
            // authored over any span still spans the whole spline.
            var lerpInitialValue = _scaleCurve.keys[0].time;
            var lerpFinalValue = _scaleCurve.keys[_scaleCurve.length - 1].time;
            var lerpTime = _splineAnimation.NormalizedTime;

            var interpolatedValue = Mathf.Lerp(lerpInitialValue, lerpFinalValue, lerpTime);
            var curveValue = _scaleCurve.Evaluate(interpolatedValue);
            var newScale = Vector3.one * curveValue;

            transform.localScale = newScale;
        }


        private void OnAnimationStarted()
        {
            AnimationStarted?.Invoke();
            if (_showLogs)
                Utility.Loggers.Logger.Log("AnimationStarted", this);
        }

        private void OnAnimationUpdated(Vector3 position, Quaternion rotation)
        {
            if (!_isActive) { return; }
            UpdateScale();
            AnimationUpdated?.Invoke(position, rotation, transform.localScale);
            if (_showLogs)
                Utility.Loggers.Logger.Log("AnimationUpdated", this);

            if (_splineAnimation.NormalizedTime >= 1f) { OnAnimationCompleted(); }
        }

        private void OnAnimationStopped()
        {
            AnimationStopped?.Invoke();
            if (_showLogs)
                Utility.Loggers.Logger.Log("AnimationStopped", this);
        }

        private void OnAnimationReset()
        {
            AnimationReset?.Invoke();
            if (_showLogs)
                Utility.Loggers.Logger.Log("AnimationReset", this);
        }

        private void OnAnimationCompleted()
        {
            AnimationCompleted?.Invoke();
            if (_showLogs)
                Utility.Loggers.Logger.Log("AnimationCompleted", this);
        }
    }
}
