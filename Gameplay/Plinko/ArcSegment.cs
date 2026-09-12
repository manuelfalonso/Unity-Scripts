using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// One ballistic hop: the ball leaves <see cref="Origin"/> with <see cref="LaunchVelocity"/> and follows
    /// a parabola under <see cref="Gravity"/> for <see cref="Duration"/> seconds.
    /// </summary>
    /// <remarks>
    /// A path is a chain of these. The chain is what gets played back; the landing bin was already decided by
    /// the integer edge sequence that produced the chain, so evaluating these floats can never change it.
    /// </remarks>
    [Serializable]
    public struct ArcSegment
    {
        [Tooltip("Board space position the hop starts from.")]
        public Vector2 Origin;

        [Tooltip("Board space velocity the hop starts with.")]
        public Vector2 LaunchVelocity;

        [Tooltip("Board space acceleration applied over the hop.")]
        public Vector2 Gravity;

        [Tooltip("How long the hop lasts, in seconds.")]
        public float Duration;

        /// <summary>
        /// Creates a hop.
        /// </summary>
        public ArcSegment(Vector2 origin, Vector2 launchVelocity, Vector2 gravity, float duration)
        {
            Origin = origin;
            LaunchVelocity = launchVelocity;
            Gravity = gravity;
            Duration = duration;
        }

        /// <summary>
        /// Position at <paramref name="time"/> seconds into the hop.
        /// </summary>
        public Vector2 Evaluate(float time)
        {
            return Origin + LaunchVelocity * time + 0.5f * Gravity * (time * time);
        }

        /// <summary>
        /// Velocity at <paramref name="time"/> seconds into the hop.
        /// </summary>
        public Vector2 VelocityAt(float time)
        {
            return LaunchVelocity + Gravity * time;
        }

        /// <summary>
        /// Position at the end of the hop.
        /// </summary>
        public Vector2 End => Evaluate(Duration);
    }
}
