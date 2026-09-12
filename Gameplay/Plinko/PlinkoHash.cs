using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Produces a signature over a layout and its simulation settings, so baked path data can be checked
    /// against the board it was baked from.
    /// </summary>
    /// <remarks>
    /// A baked path is only valid for the exact board and settings that produced it. Move one peg, change
    /// gravity by a hair, and every recorded drop is a lie that still plays back convincingly — the token
    /// glides through empty space and lands in a catch point it could no longer reach. The signature is what
    /// makes that detectable instead of shipped.
    /// </remarks>
    public static class PlinkoHash
    {
        private const ulong Offset = 14695981039346656037ul;
        private const ulong Prime = 1099511628211ul;

        /// <summary>
        /// Signature of everything that decides which paths a bake produces.
        /// </summary>
        public static ulong Compute(
            PlinkoLayout layout,
            PlinkoSimConfig sim,
            PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation)
        {
            ulong hash = Offset;

            hash = Combine(hash, layout);
            hash = Combine(hash, sim);
            hash = Combine(hash, bounce);
            hash = Combine(hash, generation);

            return hash;
        }

        /// <summary>
        /// Formats a signature the way it is stored on an asset.
        /// </summary>
        public static string Format(ulong signature)
        {
            return signature.ToString("X16");
        }

        private static ulong Combine(ulong hash, PlinkoLayout layout)
        {
            if (layout == null)
            {
                return Combine(hash, -1);
            }

            hash = Combine(hash, layout.PegRadius);
            hash = Combine(hash, layout.Pegs.Count);
            for (int i = 0; i < layout.Pegs.Count; i++)
            {
                hash = Combine(hash, layout.Pegs[i]);
            }

            hash = Combine(hash, layout.Entries.Count);
            for (int i = 0; i < layout.Entries.Count; i++)
            {
                hash = Combine(hash, layout.Entries[i]);
            }

            hash = Combine(hash, layout.CatchPoints.Count);
            for (int i = 0; i < layout.CatchPoints.Count; i++)
            {
                PlinkoCatchPoint catchPoint = layout.CatchPoints[i];
                hash = Combine(hash, catchPoint.MinX);
                hash = Combine(hash, catchPoint.MaxX);
                hash = Combine(hash, catchPoint.Y);
            }

            hash = Combine(hash, layout.Walls.Count);
            for (int i = 0; i < layout.Walls.Count; i++)
            {
                hash = Combine(hash, layout.Walls[i].From);
                hash = Combine(hash, layout.Walls[i].To);
            }

            hash = Combine(hash, layout.PlayArea.xMin);
            hash = Combine(hash, layout.PlayArea.yMin);
            hash = Combine(hash, layout.PlayArea.width);
            hash = Combine(hash, layout.PlayArea.height);

            return hash;
        }

        private static ulong Combine(ulong hash, PlinkoSimConfig sim)
        {
            hash = Combine(hash, sim.BallRadius);
            hash = Combine(hash, sim.LaunchSpeed);
            hash = Combine(hash, sim.Gravity);
            hash = Combine(hash, sim.MaxBounces);
            hash = Combine(hash, sim.MaxDropDuration);
            hash = Combine(hash, sim.TraceSubsteps);
            hash = Combine(hash, sim.TraceMaxTime);
            hash = Combine(hash, sim.RefineIterations);

            return hash;
        }

        private static ulong Combine(ulong hash, PlinkoBounceConfig bounce)
        {
            hash = Combine(hash, bounce.Restitution);
            hash = Combine(hash, bounce.ScatterDegrees);
            hash = Combine(hash, bounce.SpeedScatter);
            hash = Combine(hash, bounce.MinBounceSpeed);
            hash = Combine(hash, bounce.MinDepartureAngle);
            hash = Combine(hash, bounce.WallRestitution);
            hash = Combine(hash, bounce.WallScatterDegrees);

            return hash;
        }

        private static ulong Combine(ulong hash, PlinkoGenerationConfig generation)
        {
            hash = Combine(hash, generation.PathsPerPair);
            hash = Combine(hash, generation.SimulationsPerEntry);
            hash = Combine(hash, generation.MaxCandidatesPerPair);
            hash = Combine(hash, generation.Seed);
            hash = Combine(hash, generation.MinTrajectoryDistance);
            hash = Combine(hash, generation.MinContactDifference);
            hash = Combine(hash, generation.MaxDivergenceFraction);
            hash = Combine(hash, generation.NormalizeDuration ? 1 : 0);
            hash = Combine(hash, generation.DurationTolerance);

            return hash;
        }

        private static ulong Combine(ulong hash, Vector2 value)
        {
            return Combine(Combine(hash, value.x), value.y);
        }

        private static ulong Combine(ulong hash, float value)
        {
            // Hash the bit pattern, so a change of one ULP is still detected.
            return Combine(hash, BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
        }

        private static ulong Combine(ulong hash, int value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (ulong)((value >> (i * 8)) & 0xFF);
                    hash *= Prime;
                }
            }

            return hash;
        }
    }
}
