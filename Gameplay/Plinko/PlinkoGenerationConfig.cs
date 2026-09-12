using System;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// How many drops to simulate, how many to keep per entry and catch point pair, and what makes two kept
    /// drops different enough to both be worth keeping.
    /// </summary>
    [Serializable]
    public sealed class PlinkoGenerationConfig
    {
        [Header("Volume")]
        [Tooltip("How many distinct paths to keep for each entry and catch point pair.")]
        [Min(1)]
        public int PathsPerPair = 8;

        [Tooltip("How many drops to simulate per entry. The spec calls for a few thousand per level; raise " +
                 "this when the report shows pairs that could not be filled.")]
        [Min(1)]
        public int SimulationsPerEntry = 1200;

        [Tooltip("Seed for the bake. The same seed, layout and settings always produce the same path set.")]
        public int Seed = 20260910;

        [Tooltip("Most landed drops to hold on to per pair while choosing which to keep. A busy catch point " +
                 "can attract thousands of drops when only a handful are wanted; recording them all costs " +
                 "memory for nothing. Drops arrive in random order, so the first few hundred are already a " +
                 "fair sample. 0 means keep everything.")]
        [Min(0)]
        public int MaxCandidatesPerPair = 96;

        [Header("Distinctness")]
        [Tooltip("How far apart, in board units, two kept drops must get at some point along the way. This " +
                 "is the primary test because it works on a sparse board too, where two drops can strike the " +
                 "same single peg and still fly very differently.")]
        [Min(0f)]
        public float MinTrajectoryDistance = 0.35f;

        [Tooltip("Minimum number of differing contacts between two kept paths. Only applied when both drops " +
                 "have enough contacts for it to be satisfiable, so it never starves a sparse layout.")]
        [Min(0)]
        public int MinContactDifference = 2;

        [Tooltip("How late two kept paths may first diverge, as a fraction of their contact count. Early " +
                 "divergence is what the eye reads as a genuinely different drop.")]
        [Range(0.1f, 1f)]
        public float MaxDivergenceFraction = 0.7f;

        [Header("Duration")]
        [Tooltip("Keep every catch point's drop durations inside a shared band. Without this, a nearby catch " +
                 "point finishes measurably sooner than a distant one and the outcome can be read with a " +
                 "stopwatch before the token lands.")]
        public bool NormalizeDuration = true;

        [Tooltip("How far a kept path's duration may sit from the median drop duration, as a fraction.")]
        [Range(0.05f, 2f)]
        public float DurationTolerance = 0.35f;

        /// <summary>
        /// Validates the config and returns a human readable reason when it cannot drive a bake.
        /// </summary>
        /// <param name="error">The first problem found, or <c>null</c> when the config is usable.</param>
        /// <returns><c>true</c> when the config is usable.</returns>
        public bool Validate(out string error)
        {
            if (PathsPerPair < 1)
            {
                error = "PathsPerPair must be at least 1.";
                return false;
            }

            if (SimulationsPerEntry < 1)
            {
                error = "SimulationsPerEntry must be at least 1.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Creates a copy, so a caller can tweak values without mutating a shared instance.
        /// </summary>
        public PlinkoGenerationConfig Clone()
        {
            return (PlinkoGenerationConfig)MemberwiseClone();
        }
    }
}
