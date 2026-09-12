using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// The ball and the simulation budget: size, gravity, launch speed, and how long a drop may go on.
    /// </summary>
    [Serializable]
    public sealed class PlinkoSimConfig
    {
        [Header("Ball")]
        [Tooltip("Radius of the Drop Token, in board units. Collision against a peg uses this plus the peg " +
                 "radius, so a bigger token fits through fewer gaps.")]
        public float BallRadius = 0.18f;

        [Tooltip("Downward speed the token has when released from an entry, in board units per second.")]
        public float LaunchSpeed = 1f;

        [Tooltip("Gravity magnitude, in board units per second squared, acting along -Y. The default treats " +
                 "one board unit as roughly 0.25 m, which is 9.81 / 0.25.")]
        public float Gravity = 39.24f;

        [Header("Drop Budget")]
        [Tooltip("Most bounces a single drop may make before it is abandoned. Guards against a token that " +
                 "never finds a catch point.")]
        [Min(1)]
        public int MaxBounces = 80;

        [Tooltip("Longest a whole drop may last, in seconds. A drop that exceeds this is discarded rather " +
                 "than shown, which is how the spec's no-stuck-tokens rule is enforced.")]
        [Min(0.5f)]
        public float MaxDropDuration = 20f;

        [Header("Tracing")]
        [Tooltip("Fixed steps used to march one arc between bounces. Never derived from framerate, because " +
                 "the simulation runs once at bake time and must reproduce exactly.")]
        [Min(8)]
        public int TraceSubsteps = 160;

        [Tooltip("Longest single arc between two bounces, in seconds.")]
        [Min(0.1f)]
        public float TraceMaxTime = 2.5f;

        [Tooltip("Bisection iterations used to sharpen a contact time. Fixed so results are reproducible.")]
        [Min(1)]
        public int RefineIterations = 24;

        /// <summary>
        /// Gravity as a board space vector.
        /// </summary>
        public Vector2 GravityVector => new Vector2(0f, -Gravity);

        /// <summary>
        /// Validates the config and returns a human readable reason when it cannot drive a simulation.
        /// </summary>
        /// <param name="error">The first problem found, or <c>null</c> when the config is usable.</param>
        /// <returns><c>true</c> when the config is usable.</returns>
        public bool Validate(out string error)
        {
            if (BallRadius <= 0f)
            {
                error = "BallRadius must be greater than zero.";
                return false;
            }

            if (Gravity <= 0f)
            {
                error = "Gravity must be greater than zero, otherwise the token never descends.";
                return false;
            }

            if (TraceSubsteps < 8)
            {
                error = "TraceSubsteps must be at least 8.";
                return false;
            }

            if (TraceMaxTime <= 0f || MaxDropDuration <= 0f)
            {
                error = "TraceMaxTime and MaxDropDuration must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Creates a copy, so a caller can tweak values without mutating a shared instance.
        /// </summary>
        public PlinkoSimConfig Clone()
        {
            return (PlinkoSimConfig)MemberwiseClone();
        }
    }
}
