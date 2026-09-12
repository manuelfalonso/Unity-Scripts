using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko
{
    /// <summary>
    /// Everything one bake produced.
    /// </summary>
    public sealed class PlinkoBakeResult
    {
        /// <summary>The layout that was simulated.</summary>
        public PlinkoLayout Layout { get; set; }

        /// <summary>The kept paths, indexed by entry and catch point.</summary>
        public PlinkoPathSet PathSet { get; set; }

        /// <summary>What the bake managed and what it could not.</summary>
        public PlinkoBakeReport Report { get; set; }

        /// <summary>True when a usable path set came out.</summary>
        public bool IsUsable => PathSet != null && Report != null && Report.Error == null;
    }

    /// <summary>
    /// Runs the drop simulation many times over a layout and keeps a spread of distinct paths for every entry
    /// and catch point pair.
    /// </summary>
    /// <remarks>
    /// This is the whole pipeline the spec describes: simulate a level off-line, discard drops that never
    /// reach a catch point, and file the rest by where they landed so one can be picked and replayed when the
    /// player drops a token.
    /// <para>
    /// Coverage is a measured outcome rather than something known in advance. Where a physical board simply
    /// cannot throw a token from one entry into one catch point, no number of simulations will find a path,
    /// and the report says so instead of hanging.
    /// </para>
    /// </remarks>
    public static class PlinkoSolver
    {
        /// <summary>
        /// Bakes a layout with default ball, bounce and generation settings.
        /// </summary>
        public static PlinkoBakeResult Bake(PlinkoLayout layout)
        {
            return Bake(layout, new PlinkoSimConfig(), new PlinkoBounceConfig(), new PlinkoGenerationConfig());
        }

        /// <summary>
        /// Bakes a layout.
        /// </summary>
        /// <param name="layout">Hand placed board to simulate.</param>
        /// <param name="sim">Ball size, gravity and drop budget.</param>
        /// <param name="bounce">How pegs and walls throw the token back.</param>
        /// <param name="generation">How many drops to simulate and how many to keep.</param>
        public static PlinkoBakeResult Bake(
            PlinkoLayout layout,
            PlinkoSimConfig sim,
            PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation)
        {
            var report = new PlinkoBakeReport();
            var result = new PlinkoBakeResult { Layout = layout, Report = report };

            if (layout == null)
            {
                report.Error = "The layout is null.";
                return result;
            }

            if (!layout.Validate(out string layoutError))
            {
                report.Error = layoutError;
                return result;
            }

            if (!sim.Validate(out string simError))
            {
                report.Error = simError;
                return result;
            }

            if (!bounce.Validate(out string bounceError))
            {
                report.Error = bounceError;
                return result;
            }

            if (!generation.Validate(out string generationError))
            {
                report.Error = generationError;
                return result;
            }

            report.Signature = PlinkoHash.Compute(layout, sim, bounce, generation);

            var stopwatch = Stopwatch.StartNew();

            int entryCount = layout.EntryCount;
            int catchCount = layout.CatchPointCount;

            var simulator = new PlinkoDropSimulator(layout, sim, bounce);
            var candidates = new List<PlinkoPath>[entryCount * catchCount];
            for (int i = 0; i < candidates.Length; i++)
            {
                candidates[i] = new List<PlinkoPath>();
            }

            int candidateCap = generation.MaxCandidatesPerPair > 0
                ? Mathf.Max(generation.MaxCandidatesPerPair, generation.PathsPerPair)
                : int.MaxValue;

            var durations = new List<float>(entryCount * generation.SimulationsPerEntry / 4);
            var landedPerPair = new int[entryCount * catchCount];
            var random = new DeterministicRandom(0);

            for (int entry = 0; entry < entryCount; entry++)
            {
                for (int i = 0; i < generation.SimulationsPerEntry; i++)
                {
                    // Every drop gets its own stream, so a drop's outcome does not depend on how many were
                    // simulated before it. That makes a bake reproducible whatever order drops are run in,
                    // and is what would let this fan out across threads later.
                    random.Reseed(DeriveDropSeed(generation.Seed, entry, i));

                    PlinkoDropOutcome outcome = simulator.SimulateDrop(entry, random);
                    report.Simulated++;

                    if (!outcome.Landed)
                    {
                        report.RecordFailure(outcome.Failure);
                        continue;
                    }

                    // Durations feed the shared band, so every landed drop counts even when its path is not
                    // recorded.
                    durations.Add(outcome.Duration);

                    int pairIndex = entry * catchCount + outcome.CatchPointIndex;
                    landedPerPair[pairIndex]++;

                    List<PlinkoPath> pool = candidates[pairIndex];
                    if (pool.Count >= candidateCap)
                    {
                        continue;
                    }

                    pool.Add(simulator.BuildLastPath());
                }
            }

            report.Landed = durations.Count;

            float median = 0f;
            if (durations.Count > 0)
            {
                durations.Sort();
                median = durations[durations.Count / 2];
            }

            report.MedianDuration = median;

            Vector2? band = generation.NormalizeDuration && median > 0f
                ? new Vector2(
                    median * (1f - generation.DurationTolerance),
                    median * (1f + generation.DurationTolerance))
                : (Vector2?)null;

            var pathSet = new PlinkoPathSet(entryCount, catchCount);

            for (int entry = 0; entry < entryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < catchCount; catchPoint++)
                {
                    List<PlinkoPath> pool = candidates[entry * catchCount + catchPoint];
                    string pair = $"E{entry}->C{catchPoint}";

                    if (pool.Count == 0)
                    {
                        report.UnreachablePairs.Add(pair);
                        continue;
                    }

                    List<PlinkoPath> kept = SelectDistinct(pool, generation, band);

                    if (kept.Count == 0)
                    {
                        // An empty pair is worse than a timing outlier, so drop the band and say so.
                        kept = SelectDistinct(pool, generation, null);
                        if (kept.Count > 0)
                        {
                            report.DurationRelaxedPairs.Add(pair);
                        }
                    }

                    if (kept.Count < generation.PathsPerPair)
                    {
                        report.UnderfilledPairs.Add(
                            $"{pair} ({kept.Count}/{generation.PathsPerPair}, " +
                            $"{landedPerPair[entry * catchCount + catchPoint]} landed)");
                    }

                    pathSet.SetPaths(entry, catchPoint, kept);
                }
            }

            stopwatch.Stop();

            FillStatistics(pathSet, report);
            report.TotalPaths = pathSet.TotalPathCount;
            report.ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

            result.PathSet = pathSet;
            return result;
        }

        /// <summary>
        /// Derives the stream seed for one drop, from the bake seed, the entry and the drop's index.
        /// </summary>
        /// <remarks>
        /// Each drop is independent of every other, so raising the simulation count adds drops rather than
        /// shifting the ones already there, and the bake would give identical results if the drops were run
        /// in any order or in parallel.
        /// </remarks>
        public static int DeriveDropSeed(int seed, int entryIndex, int dropIndex)
        {
            unchecked
            {
                int hash = seed * 486187739;
                hash = (hash ^ (entryIndex * 92821)) * 16777619;
                hash = (hash ^ dropIndex) * 16777619;
                return hash ^ (hash >> 15);
            }
        }

        /// <summary>
        /// Picks a spread of visibly different paths out of everything that landed in one catch point.
        /// </summary>
        /// <remarks>
        /// Candidates are walked in order of how early they diverge from what has already been kept, so the
        /// chosen set splits apart near the top of the board rather than all following one another down and
        /// parting at the last peg.
        /// </remarks>
        private static List<PlinkoPath> SelectDistinct(
            List<PlinkoPath> pool, PlinkoGenerationConfig generation, Vector2? durationBand)
        {
            var kept = new List<PlinkoPath>(generation.PathsPerPair);
            var signatures = new HashSet<ulong>();

            for (int i = 0; i < pool.Count && kept.Count < generation.PathsPerPair; i++)
            {
                PlinkoPath candidate = pool[i];

                if (!signatures.Add(candidate.Signature))
                {
                    continue;
                }

                if (durationBand.HasValue &&
                    (candidate.Duration < durationBand.Value.x || candidate.Duration > durationBand.Value.y))
                {
                    continue;
                }

                if (IsDistinctEnough(candidate, kept, generation))
                {
                    kept.Add(candidate);
                }
            }

            // Nothing cleared the distinctness bar, so fall back to whatever landed rather than nothing.
            if (kept.Count == 0 && !durationBand.HasValue && pool.Count > 0)
            {
                kept.Add(pool[0]);
            }

            return kept;
        }

        private static bool IsDistinctEnough(
            PlinkoPath candidate, List<PlinkoPath> kept, PlinkoGenerationConfig generation)
        {
            for (int i = 0; i < kept.Count; i++)
            {
                PlinkoPath existing = kept[i];

                // Geometry first: this is what a player sees, and it is the only test that means anything on
                // a board sparse enough that two drops share their whole contact list.
                if (candidate.GetTrajectoryDistance(existing) < generation.MinTrajectoryDistance)
                {
                    return false;
                }

                int shortest = Mathf.Min(candidate.Contacts.Count, existing.Contacts.Count);
                int longest = Mathf.Max(candidate.Contacts.Count, existing.Contacts.Count);

                // Only demand differing contacts where there are enough contacts to differ in, otherwise a
                // one-peg layout could never satisfy it and every pair would starve.
                if (shortest >= generation.MinContactDifference * 2 &&
                    candidate.GetContactDifference(existing) < generation.MinContactDifference)
                {
                    return false;
                }

                if (longest == 0)
                {
                    continue;
                }

                float divergence = candidate.GetFirstDivergenceIndex(existing) / (float)longest;
                if (divergence > generation.MaxDivergenceFraction)
                {
                    return false;
                }
            }

            return true;
        }

        private static void FillStatistics(PlinkoPathSet pathSet, PlinkoBakeReport report)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            int maxContacts = 0;
            long contactTotal = 0;
            int counted = 0;

            for (int entry = 0; entry < pathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < pathSet.CatchPointCount; catchPoint++)
                {
                    IReadOnlyList<PlinkoPath> paths = pathSet.GetPaths(entry, catchPoint);
                    for (int i = 0; i < paths.Count; i++)
                    {
                        min = Mathf.Min(min, paths[i].Duration);
                        max = Mathf.Max(max, paths[i].Duration);
                        maxContacts = Mathf.Max(maxContacts, paths[i].Contacts.Count);
                        contactTotal += paths[i].Contacts.Count;
                        counted++;
                    }
                }
            }

            report.MinDuration = min == float.MaxValue ? 0f : min;
            report.MaxDuration = max == float.MinValue ? 0f : max;
            report.MaxContacts = maxContacts;
            report.MeanContacts = counted == 0 ? 0f : contactTotal / (float)counted;
        }
    }
}
