using System.Collections.Generic;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// A baked path set stored as an asset, together with the board it was baked from.
    /// </summary>
    /// <remarks>
    /// Simulating thousands of drops takes seconds; loading them takes none. Baking once into an asset moves
    /// that cost out of the player's session and into the Editor or CI, which is what the spec assumes when
    /// it puts the simulator on the backend.
    /// <para>
    /// The recording exploits two properties of a drop to stay small. Each arc begins exactly where the last
    /// one ended, so only the very first position needs storing; and gravity is constant across a bake, so it
    /// is stored once. That leaves a velocity and a duration per arc — twelve bytes — plus two small numbers
    /// per contact. Contact times and positions are recomputed on load rather than stored.
    /// </para>
    /// <para>
    /// <see cref="Signature"/> ties the data to the exact layout and settings that produced it. A baked path
    /// is only true for that board: move one peg and the recording still plays back convincingly while the
    /// token glides through empty space, so a mismatch has to be caught rather than trusted.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "PlinkoPathSet", menuName = "Sombra Studios/Gameplay/Plinko Path Set")]
    public class PlinkoPathSetSO : ScriptableObject
    {
        [Header("Source")]
        [Tooltip("The board these drops were simulated on. Kept with the data so a scene needs only this asset.")]
        [SerializeField] private PlinkoLayout _layout = new PlinkoLayout();

        [Tooltip("Signature of the layout and settings that produced this data. A board whose signature " +
                 "differs must not use it.")]
        [SerializeField] private string _signature;

        [Tooltip("Human readable summary of the bake that produced this asset.")]
        [SerializeField, TextArea(3, 12)] private string _report;

        [Header("Data")]
        [SerializeField] private int _entryCount;
        [SerializeField] private int _catchPointCount;
        [SerializeField] private Vector2 _gravity;

        [Tooltip("First path index for each entry and catch point pair.")]
        [SerializeField] private int[] _pairStart;

        [Tooltip("How many paths each pair holds.")]
        [SerializeField] private int[] _pairCount;

        [SerializeField] private Vector2[] _pathStartPosition;
        [SerializeField] private int[] _pathEntry;
        [SerializeField] private int[] _pathCatchPoint;
        [SerializeField] private int[] _pathArcStart;
        [SerializeField] private int[] _pathArcCount;

        [SerializeField] private Vector2[] _arcVelocity;
        [SerializeField] private float[] _arcDuration;

        [SerializeField] private byte[] _contactKind;
        [SerializeField] private int[] _contactIndex;

        /// <summary>The board these drops were simulated on.</summary>
        public PlinkoLayout Layout => _layout;

        /// <summary>Signature of the layout and settings that produced this data.</summary>
        public string Signature => _signature;

        /// <summary>Summary of the bake that produced this asset.</summary>
        public string Report => _report;

        /// <summary>Number of entries covered.</summary>
        public int EntryCount => _entryCount;

        /// <summary>Number of catch points covered.</summary>
        public int CatchPointCount => _catchPointCount;

        /// <summary>Number of recorded drops.</summary>
        public int PathCount => _pathEntry?.Length ?? 0;

        /// <summary>True when the asset holds usable data.</summary>
        public bool HasData => PathCount > 0 && _pairStart != null && _pairStart.Length > 0;

        /// <summary>
        /// Whether this asset was baked from the given layout and settings.
        /// </summary>
        public bool Matches(
            PlinkoLayout layout, PlinkoSimConfig sim, PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation)
        {
            return _signature == PlinkoHash.Format(PlinkoHash.Compute(layout, sim, bounce, generation));
        }

        /// <summary>
        /// Replaces the stored data with a freshly baked path set. Called by the Editor bake, and by any
        /// build script that wants to regenerate assets.
        /// </summary>
        /// <param name="layout">Board that was simulated.</param>
        /// <param name="pathSet">Paths to store.</param>
        /// <param name="gravity">Gravity the bake ran under, shared by every arc.</param>
        /// <param name="signature">Signature of the layout and settings.</param>
        /// <param name="report">Summary to keep alongside the data.</param>
        public void Store(
            PlinkoLayout layout, PlinkoPathSet pathSet, Vector2 gravity, ulong signature, string report)
        {
            _layout = layout.Clone();
            _signature = PlinkoHash.Format(signature);
            _report = report;
            _entryCount = pathSet.EntryCount;
            _catchPointCount = pathSet.CatchPointCount;
            _gravity = gravity;

            int pairs = _entryCount * _catchPointCount;
            _pairStart = new int[pairs];
            _pairCount = new int[pairs];

            var startPositions = new List<Vector2>(pathSet.TotalPathCount);
            var pathEntry = new List<int>(pathSet.TotalPathCount);
            var pathCatch = new List<int>(pathSet.TotalPathCount);
            var pathArcStart = new List<int>(pathSet.TotalPathCount);
            var pathArcCount = new List<int>(pathSet.TotalPathCount);
            var arcVelocity = new List<Vector2>(pathSet.TotalPathCount * 24);
            var arcDuration = new List<float>(pathSet.TotalPathCount * 24);
            var contactKind = new List<byte>(pathSet.TotalPathCount * 24);
            var contactIndex = new List<int>(pathSet.TotalPathCount * 24);

            for (int entry = 0; entry < _entryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < _catchPointCount; catchPoint++)
                {
                    int pair = entry * _catchPointCount + catchPoint;
                    IReadOnlyList<PlinkoPath> paths = pathSet.GetPaths(entry, catchPoint);

                    _pairStart[pair] = pathEntry.Count;
                    _pairCount[pair] = paths.Count;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        PlinkoPath path = paths[i];

                        pathEntry.Add(path.EntryIndex);
                        pathCatch.Add(path.CatchPointIndex);
                        startPositions.Add(path.Arcs.Count > 0 ? path.Arcs[0].Origin : Vector2.zero);
                        pathArcStart.Add(arcVelocity.Count);
                        pathArcCount.Add(path.Arcs.Count);

                        for (int a = 0; a < path.Arcs.Count; a++)
                        {
                            arcVelocity.Add(path.Arcs[a].LaunchVelocity);
                            arcDuration.Add(path.Arcs[a].Duration);
                        }

                        for (int c = 0; c < path.Contacts.Count; c++)
                        {
                            contactKind.Add((byte)path.Contacts[c].Kind);
                            contactIndex.Add(path.Contacts[c].Index);
                        }
                    }
                }
            }

            _pathStartPosition = startPositions.ToArray();
            _pathEntry = pathEntry.ToArray();
            _pathCatchPoint = pathCatch.ToArray();
            _pathArcStart = pathArcStart.ToArray();
            _pathArcCount = pathArcCount.ToArray();
            _arcVelocity = arcVelocity.ToArray();
            _arcDuration = arcDuration.ToArray();
            _contactKind = contactKind.ToArray();
            _contactIndex = contactIndex.ToArray();
        }

        /// <summary>
        /// Rebuilds the runtime path set from the stored data.
        /// </summary>
        /// <returns>The path set, or <c>null</c> when the asset holds no data.</returns>
        public PlinkoPathSet ToPathSet()
        {
            if (!HasData)
            {
                return null;
            }

            var pathSet = new PlinkoPathSet(_entryCount, _catchPointCount);

            for (int entry = 0; entry < _entryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < _catchPointCount; catchPoint++)
                {
                    int pair = entry * _catchPointCount + catchPoint;
                    int start = _pairStart[pair];
                    int count = _pairCount[pair];

                    var paths = new List<PlinkoPath>(count);
                    for (int i = 0; i < count; i++)
                    {
                        paths.Add(RebuildPath(start + i));
                    }

                    pathSet.SetPaths(entry, catchPoint, paths);
                }
            }

            return pathSet;
        }

        private PlinkoPath RebuildPath(int pathIndex)
        {
            int arcStart = _pathArcStart[pathIndex];
            int arcCount = _pathArcCount[pathIndex];

            var arcs = new ArcSegment[arcCount];
            var startTimes = new float[arcCount];

            // Every arc but the last ends in a contact, so the two counts move together and contact offsets
            // can be derived rather than stored.
            int contactCount = Mathf.Max(0, arcCount - 1);
            var contacts = new PlinkoContact[contactCount];
            int contactStart = arcStart - pathIndex;

            Vector2 position = _pathStartPosition[pathIndex];
            float elapsed = 0f;

            for (int a = 0; a < arcCount; a++)
            {
                Vector2 velocity = _arcVelocity[arcStart + a];
                float duration = _arcDuration[arcStart + a];

                var arc = new ArcSegment(position, velocity, _gravity, duration);
                arcs[a] = arc;
                startTimes[a] = elapsed;
                elapsed += duration;

                if (a < contactCount)
                {
                    Vector2 impact = velocity + _gravity * duration;
                    contacts[a] = new PlinkoContact(
                        (PlinkoTraceOutcome)_contactKind[contactStart + a],
                        _contactIndex[contactStart + a],
                        arc.End,
                        elapsed,
                        impact.magnitude);
                }

                // The next arc starts exactly where this one ended, which is why no origin is stored.
                position = arc.End;
            }

            ulong signature = 14695981039346656037ul;
            for (int c = 0; c < contactCount; c++)
            {
                signature = Hash(signature, (int)contacts[c].Kind);
                signature = Hash(signature, contacts[c].Index);
            }

            signature = Hash(signature, Mathf.RoundToInt(elapsed * 1000f));

            return new PlinkoPath(
                _pathEntry[pathIndex], _pathCatchPoint[pathIndex], arcs, startTimes, contacts,
                elapsed, signature);
        }

        private static ulong Hash(ulong hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (ulong)((value >> (i * 8)) & 0xFF);
                    hash *= 1099511628211ul;
                }
            }

            return hash;
        }
    }
}
