using System.Collections.Generic;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// What the token struck at a point along its drop.
    /// </summary>
    public readonly struct PlinkoContact
    {
        /// <summary>Whether a peg or a wall was struck.</summary>
        public readonly PlinkoTraceOutcome Kind;

        /// <summary>Index of the peg or wall inside the layout.</summary>
        public readonly int Index;

        /// <summary>Board space position of the ball centre at the contact.</summary>
        public readonly Vector2 Position;

        /// <summary>Time from the start of the drop, in seconds.</summary>
        public readonly float Time;

        /// <summary>Speed at the moment of contact, useful for scaling hit effects and sounds.</summary>
        public readonly float ImpactSpeed;

        /// <summary>
        /// Creates a contact record.
        /// </summary>
        public PlinkoContact(PlinkoTraceOutcome kind, int index, Vector2 position, float time, float impactSpeed)
        {
            Kind = kind;
            Index = index;
            Position = position;
            Time = time;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>
    /// One complete simulated drop: every ballistic arc between bounces, every contact along the way, and the
    /// catch point it ends in.
    /// </summary>
    /// <remarks>
    /// This is a <b>recording</b> of a single physical simulation, not a recipe to be re-simulated. That is
    /// what makes the system deterministic: the landing catch point was decided when the drop was baked, and
    /// playback only reads back positions. Because the whole thing came from one continuous simulation, each
    /// arc begins exactly where the previous one ended, so the token never jumps.
    /// </remarks>
    public sealed class PlinkoPath
    {
        private readonly float[] _segmentStartTimes;

        /// <summary>Entry this drop was released from.</summary>
        public int EntryIndex { get; }

        /// <summary>Catch point this drop ends in.</summary>
        public int CatchPointIndex { get; }

        /// <summary>The ballistic arcs between bounces, in order.</summary>
        public IReadOnlyList<ArcSegment> Arcs { get; }

        /// <summary>Everything the token struck, in order.</summary>
        public IReadOnlyList<PlinkoContact> Contacts { get; }

        /// <summary>Total drop time, in seconds.</summary>
        public float Duration { get; }

        /// <summary>Hash of the contact sequence, used to tell two drops apart.</summary>
        public ulong Signature { get; }

        /// <summary>Number of pegs struck.</summary>
        public int PegHitCount { get; }

        /// <summary>Number of walls or side shapes struck.</summary>
        public int WallHitCount { get; }

        /// <summary>
        /// Creates a path. Built by <see cref="PlinkoDropSimulator"/>.
        /// </summary>
        public PlinkoPath(
            int entryIndex,
            int catchPointIndex,
            ArcSegment[] arcs,
            float[] segmentStartTimes,
            PlinkoContact[] contacts,
            float duration,
            ulong signature)
        {
            EntryIndex = entryIndex;
            CatchPointIndex = catchPointIndex;
            Arcs = arcs;
            _segmentStartTimes = segmentStartTimes;
            Contacts = contacts;
            Duration = duration;
            Signature = signature;

            for (int i = 0; i < contacts.Length; i++)
            {
                if (contacts[i].Kind == PlinkoTraceOutcome.Peg)
                {
                    PegHitCount++;
                }
                else if (contacts[i].Kind == PlinkoTraceOutcome.Wall)
                {
                    WallHitCount++;
                }
            }
        }

        /// <summary>
        /// Board space position of the token at <paramref name="time"/> seconds into the drop, clamped to the
        /// start and end of the path.
        /// </summary>
        public Vector2 Evaluate(float time)
        {
            if (Arcs.Count == 0)
            {
                return Vector2.zero;
            }

            if (time <= 0f)
            {
                return Arcs[0].Origin;
            }

            if (time >= Duration)
            {
                return Arcs[Arcs.Count - 1].End;
            }

            int index = FindSegmentIndex(time);
            return Arcs[index].Evaluate(time - _segmentStartTimes[index]);
        }

        /// <summary>
        /// Board space velocity of the token at <paramref name="time"/> seconds into the drop.
        /// </summary>
        public Vector2 EvaluateVelocity(float time)
        {
            if (Arcs.Count == 0)
            {
                return Vector2.zero;
            }

            int index = FindSegmentIndex(Mathf.Clamp(time, 0f, Duration));
            return Arcs[index].VelocityAt(Mathf.Clamp(time, 0f, Duration) - _segmentStartTimes[index]);
        }

        /// <summary>
        /// Index of the arc in progress at <paramref name="time"/> seconds into the drop.
        /// </summary>
        public int FindSegmentIndex(float time)
        {
            int low = 0;
            int high = _segmentStartTimes.Length - 1;

            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (_segmentStartTimes[middle] <= time)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        /// <summary>
        /// How far apart two drops are, counted as contacts that do not match position for position. Used to
        /// keep the baked set from filling with near-identical drops.
        /// </summary>
        public int GetContactDifference(PlinkoPath other)
        {
            int shared = Mathf.Min(Contacts.Count, other.Contacts.Count);
            int difference = Mathf.Abs(Contacts.Count - other.Contacts.Count);

            for (int i = 0; i < shared; i++)
            {
                if (Contacts[i].Kind != other.Contacts[i].Kind || Contacts[i].Index != other.Contacts[i].Index)
                {
                    difference++;
                }
            }

            return difference;
        }

        /// <summary>
        /// The largest distance between two drops when both are sampled at the same fractions of their own
        /// duration, in board units.
        /// </summary>
        /// <remarks>
        /// Contact identity alone cannot separate two drops on a sparse board: if both hit the same single
        /// peg, their contact lists are identical however differently they fly. This measures what the eye
        /// actually judges, so it works whether a drop touches two pegs or twenty.
        /// </remarks>
        public float GetTrajectoryDistance(PlinkoPath other, int samples = 24)
        {
            float largest = 0f;

            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                float distance = (Evaluate(Duration * t) - other.Evaluate(other.Duration * t)).magnitude;
                largest = Mathf.Max(largest, distance);
            }

            return largest;
        }

        /// <summary>
        /// Index of the first contact at which two drops stop agreeing, or the shared length when one is a
        /// prefix of the other.
        /// </summary>
        public int GetFirstDivergenceIndex(PlinkoPath other)
        {
            int shared = Mathf.Min(Contacts.Count, other.Contacts.Count);
            for (int i = 0; i < shared; i++)
            {
                if (Contacts[i].Kind != other.Contacts[i].Kind || Contacts[i].Index != other.Contacts[i].Index)
                {
                    return i;
                }
            }

            return shared;
        }
    }
}
