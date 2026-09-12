using System.Collections.Generic;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Why a simulated drop was thrown away.
    /// </summary>
    public enum PlinkoDropFailure
    {
        /// <summary>The drop finished in a catch point.</summary>
        None = 0,

        /// <summary>The token left the play area.</summary>
        LeftPlayArea = 1,

        /// <summary>An arc ran out of trace time without reaching anything.</summary>
        Timeout = 2,

        /// <summary>The drop used up its bounce budget without finding a catch point.</summary>
        BounceBudget = 3,

        /// <summary>The drop ran longer than the allowed duration.</summary>
        TooLong = 4,
    }

    /// <summary>
    /// The outcome of one simulated drop, without the recording.
    /// </summary>
    /// <remarks>
    /// Returned before any path object is built, so a bake can decide whether it still wants this drop —
    /// most are surplus once a pair has filled up — and skip the allocation entirely.
    /// </remarks>
    public readonly struct PlinkoDropOutcome
    {
        /// <summary>Whether the token reached a catch point.</summary>
        public readonly bool Landed;

        /// <summary>Catch point reached, or -1.</summary>
        public readonly int CatchPointIndex;

        /// <summary>How long the drop took, in seconds.</summary>
        public readonly float Duration;

        /// <summary>How many pegs and walls were struck.</summary>
        public readonly int ContactCount;

        /// <summary>Why the drop was discarded, or <see cref="PlinkoDropFailure.None"/>.</summary>
        public readonly PlinkoDropFailure Failure;

        internal PlinkoDropOutcome(
            bool landed, int catchPointIndex, float duration, int contactCount, PlinkoDropFailure failure)
        {
            Landed = landed;
            CatchPointIndex = catchPointIndex;
            Duration = duration;
            ContactCount = contactCount;
            Failure = failure;
        }
    }

    /// <summary>
    /// Simulates one drop of the token through a layout, bouncing off pegs and walls until it falls into a
    /// catch point.
    /// </summary>
    /// <remarks>
    /// This is the "backend simulator" the spec calls for. It runs off-line, many times per layout, and each
    /// successful run can be recorded as a <see cref="PlinkoPath"/> and later replayed verbatim. Nothing here
    /// runs while the player watches, and no Unity physics is involved.
    /// <para>
    /// Every bounce is a true reflection about the contact normal, damped and then scattered within bounds
    /// (see <see cref="PlinkoBounceConfig"/>). Two drops from the same entry diverge because the scatter
    /// differs, not because the peg offers a fixed pair of exits.
    /// </para>
    /// <para>
    /// Simulating and recording are separate steps: <see cref="SimulateDrop"/> allocates nothing, and
    /// <see cref="BuildLastPath"/> turns the most recent landed drop into a path only when the caller wants
    /// to keep it.
    /// </para>
    /// </remarks>
    public sealed class PlinkoDropSimulator
    {
        private readonly PlinkoLayoutSnapshot _layout;
        private readonly PlinkoSimConfig _sim;
        private readonly PlinkoBounceConfig _bounce;

        private readonly List<ArcSegment> _arcs = new List<ArcSegment>(64);
        private readonly List<float> _startTimes = new List<float>(64);
        private readonly List<PlinkoContact> _contacts = new List<PlinkoContact>(64);

        private int _lastEntryIndex = -1;
        private int _lastCatchPointIndex = -1;
        private float _lastDuration;
        private bool _lastLanded;

        /// <summary>Why the last simulated drop failed, or <see cref="PlinkoDropFailure.None"/>.</summary>
        public PlinkoDropFailure LastFailure { get; private set; }

        /// <summary>The snapshot this simulator runs against.</summary>
        public PlinkoLayoutSnapshot Layout => _layout;

        /// <summary>
        /// Creates a simulator bound to one layout and one set of settings.
        /// </summary>
        public PlinkoDropSimulator(PlinkoLayout layout, PlinkoSimConfig sim, PlinkoBounceConfig bounce)
            : this(new PlinkoLayoutSnapshot(layout), sim, bounce)
        {
        }

        /// <summary>
        /// Creates a simulator over an already taken snapshot, so several simulators can share one.
        /// </summary>
        public PlinkoDropSimulator(PlinkoLayoutSnapshot layout, PlinkoSimConfig sim, PlinkoBounceConfig bounce)
        {
            _layout = layout;
            _sim = sim;
            _bounce = bounce;
        }

        /// <summary>
        /// Simulates one drop and reports where it ended, without building a path. Allocates nothing.
        /// </summary>
        /// <param name="entryIndex">Entry to release from.</param>
        /// <param name="random">Seeded generator supplying the bounce scatter.</param>
        public PlinkoDropOutcome SimulateDrop(int entryIndex, DeterministicRandom random)
        {
            LastFailure = PlinkoDropFailure.None;
            _lastLanded = false;
            _lastEntryIndex = entryIndex;
            _lastCatchPointIndex = -1;
            _lastDuration = 0f;

            _arcs.Clear();
            _startTimes.Clear();
            _contacts.Clear();

            Vector2 gravity = _sim.GravityVector;
            Vector2 position = _layout.Entries[entryIndex];
            var velocity = new Vector2(0f, -_sim.LaunchSpeed);

            float elapsed = 0f;

            for (int bounce = 0; bounce <= _sim.MaxBounces; bounce++)
            {
                PlinkoTraceResult hit = PlinkoTracer.Trace(_layout, _sim, position, velocity);

                _arcs.Add(new ArcSegment(position, velocity, gravity, hit.Time));
                _startTimes.Add(elapsed);
                elapsed += hit.Time;

                if (elapsed > _sim.MaxDropDuration)
                {
                    return Fail(PlinkoDropFailure.TooLong);
                }

                switch (hit.Outcome)
                {
                    case PlinkoTraceOutcome.CatchPoint:
                        _lastLanded = true;
                        _lastCatchPointIndex = hit.Index;
                        _lastDuration = elapsed;
                        return new PlinkoDropOutcome(
                            true, hit.Index, elapsed, _contacts.Count, PlinkoDropFailure.None);

                    case PlinkoTraceOutcome.LeftPlayArea:
                        return Fail(PlinkoDropFailure.LeftPlayArea);

                    case PlinkoTraceOutcome.Timeout:
                        return Fail(PlinkoDropFailure.Timeout);
                }

                Vector2 impactVelocity = velocity + gravity * hit.Time;
                bool isWall = hit.Outcome == PlinkoTraceOutcome.Wall;

                _contacts.Add(new PlinkoContact(
                    hit.Outcome, hit.Index, hit.Position, elapsed, impactVelocity.magnitude));

                // The next arc starts exactly where this one ended, so the recording is continuous.
                position = hit.Position;
                velocity = _bounce.Reflect(impactVelocity, hit.Normal, isWall, random);
            }

            return Fail(PlinkoDropFailure.BounceBudget);
        }

        /// <summary>
        /// Turns the most recent landed drop into a recorded path. Returns <c>null</c> when the last drop did
        /// not land.
        /// </summary>
        public PlinkoPath BuildLastPath()
        {
            if (!_lastLanded)
            {
                return null;
            }

            ulong signature = 14695981039346656037ul;
            for (int i = 0; i < _contacts.Count; i++)
            {
                signature = Hash(signature, (int)_contacts[i].Kind);
                signature = Hash(signature, _contacts[i].Index);
            }

            // Two drops can share a contact sequence yet differ visibly in timing, so fold the duration in.
            signature = Hash(signature, Mathf.RoundToInt(_lastDuration * 1000f));

            return new PlinkoPath(
                _lastEntryIndex,
                _lastCatchPointIndex,
                _arcs.ToArray(),
                _startTimes.ToArray(),
                _contacts.ToArray(),
                _lastDuration,
                signature);
        }

        /// <summary>
        /// Simulates one drop and records it, or returns <c>null</c> when it failed to reach a catch point.
        /// Convenience over <see cref="SimulateDrop"/> plus <see cref="BuildLastPath"/>.
        /// </summary>
        public PlinkoPath Simulate(int entryIndex, DeterministicRandom random)
        {
            SimulateDrop(entryIndex, random);
            return BuildLastPath();
        }

        private PlinkoDropOutcome Fail(PlinkoDropFailure failure)
        {
            LastFailure = failure;
            _lastLanded = false;

            return new PlinkoDropOutcome(false, -1, 0f, _contacts.Count, failure);
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
