using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// How a peg or wall throws the ball back: a real reflection, plus the bounded scatter that makes two
    /// drops down the same board diverge.
    /// </summary>
    /// <remarks>
    /// This replaces the earlier "two fixed deflections per peg" model, which was the reason drops looked
    /// awkward: the ball left every peg at the same speed and the same angle no matter how it arrived, so it
    /// read as a marble on rails rather than a bouncing token.
    /// <para>
    /// Here the outgoing velocity is the genuine mirror of the incoming one about the contact normal, damped
    /// by <see cref="Restitution"/>. Arrival speed and angle therefore carry through the whole board. The
    /// branching that gives many paths per catch point comes from <see cref="ScatterDegrees"/> and
    /// <see cref="SpeedScatter"/>, which stand in for the spin, surface irregularity and sub-millimetre
    /// aiming differences a physical board has and a simulation otherwise does not.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class PlinkoBounceConfig
    {
        [Header("Peg Bounce")]
        [Tooltip("Fraction of speed kept through a peg bounce. Below about 0.4 the ball dies on the pegs; " +
                 "above about 0.8 it ricochets for a long time and drops get slow.")]
        [Range(0.1f, 0.95f)]
        public float Restitution = 0.7f;

        [Tooltip("Maximum angle, in degrees, by which a bounce may be rotated away from the true reflection. " +
                 "This is the main variety dial: 0 makes every drop from an entry identical, large values " +
                 "look bouncy but unphysical.")]
        [Range(0f, 60f)]
        public float ScatterDegrees = 18f;

        [Tooltip("Maximum fraction by which a bounce may speed up or slow down beyond restitution. Adds " +
                 "variety in timing as well as direction.")]
        [Range(0f, 0.5f)]
        public float SpeedScatter = 0.12f;

        [Tooltip("Speed floor after a bounce, so a ball that has lost almost everything still clears the peg " +
                 "instead of creeping along its surface.")]
        [Min(0f)]
        public float MinBounceSpeed = 0.6f;

        [Tooltip("Smallest angle, in degrees, between the outgoing velocity and the contact normal. Stops a " +
                 "grazing bounce from running along the surface and re-colliding forever.")]
        [Range(1f, 45f)]
        public float MinDepartureAngle = 8f;

        [Header("Wall Bounce")]
        [Tooltip("Fraction of speed kept through a wall or side shape bounce. Usually lower than a peg, so " +
                 "the ball settles back into play rather than pinging across the board.")]
        [Range(0.1f, 0.95f)]
        public float WallRestitution = 0.45f;

        [Tooltip("Maximum scatter, in degrees, applied to a wall bounce. Kept small: walls should return the " +
                 "ball to play predictably.")]
        [Range(0f, 30f)]
        public float WallScatterDegrees = 6f;

        /// <summary>
        /// Validates the config and returns a human readable reason when it cannot drive a simulation.
        /// </summary>
        /// <param name="error">The first problem found, or <c>null</c> when the config is usable.</param>
        /// <returns><c>true</c> when the config is usable.</returns>
        public bool Validate(out string error)
        {
            if (Restitution <= 0f || WallRestitution <= 0f)
            {
                error = "Restitution values must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Reflects <paramref name="incoming"/> about <paramref name="normal"/> and applies the configured
        /// damping and scatter.
        /// </summary>
        /// <param name="incoming">Velocity at the moment of contact.</param>
        /// <param name="normal">Unit surface normal pointing away from the collider, towards the ball.</param>
        /// <param name="isWall"><c>true</c> to use the wall restitution and scatter instead of the peg ones.</param>
        /// <param name="random">Seeded generator supplying the scatter.</param>
        public Vector2 Reflect(Vector2 incoming, Vector2 normal, bool isWall, DeterministicRandom random)
        {
            float restitution = isWall ? WallRestitution : Restitution;
            float scatter = isWall ? WallScatterDegrees : ScatterDegrees;

            // The genuine mirror of the incoming velocity. Everything else only perturbs this.
            Vector2 reflected = incoming - 2f * Vector2.Dot(incoming, normal) * normal;
            reflected *= restitution;

            float speed = reflected.magnitude;
            if (speed <= 1e-5f)
            {
                // A dead-on stop still has to leave the peg, so push it straight back out.
                reflected = normal * Mathf.Max(MinBounceSpeed, 0.01f);
                speed = reflected.magnitude;
            }

            float angleOffset = (float)((random.NextDouble() * 2d - 1d) * scatter);
            float speedFactor = 1f + (float)((random.NextDouble() * 2d - 1d) * SpeedScatter);

            Vector2 direction = Rotate(reflected / speed, angleOffset);
            speed = Mathf.Max(speed * Mathf.Max(speedFactor, 0.05f), MinBounceSpeed);

            return EnsureDeparting(direction, normal) * speed;
        }

        /// <summary>
        /// Rotates a direction away from the surface until it clears
        /// <see cref="MinDepartureAngle"/>, so the ball always leaves rather than skimming.
        /// </summary>
        private Vector2 EnsureDeparting(Vector2 direction, Vector2 normal)
        {
            float minimumDot = Mathf.Sin(MinDepartureAngle * Mathf.Deg2Rad);
            float dot = Vector2.Dot(direction, normal);

            if (dot >= minimumDot)
            {
                return direction;
            }

            // Rebuild the direction from the surface tangent, keeping the side it was heading towards.
            var tangent = new Vector2(-normal.y, normal.x);
            float tangentSign = Vector2.Dot(direction, tangent) >= 0f ? 1f : -1f;

            float tangentAmount = Mathf.Sqrt(Mathf.Max(0f, 1f - minimumDot * minimumDot));
            return (normal * minimumDot + tangent * (tangentSign * tangentAmount)).normalized;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);

            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        /// <summary>
        /// Creates a copy, so a caller can tweak values without mutating a shared instance.
        /// </summary>
        public PlinkoBounceConfig Clone()
        {
            return (PlinkoBounceConfig)MemberwiseClone();
        }
    }
}
