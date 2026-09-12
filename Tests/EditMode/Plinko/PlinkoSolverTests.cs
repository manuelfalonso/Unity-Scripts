using System.Collections.Generic;
using NUnit.Framework;
using SombraStudios.Shared.Gameplay.Plinko;
using UnityEngine;

namespace SombraStudios.Shared.Tests.Plinko
{
    /// <summary>
    /// Covers the deterministic Plinko drop simulator: layout geometry, bounce reflection, the simulation
    /// itself, and the baked path set. Everything under test is a plain object, so no scene is needed.
    /// </summary>
    public class PlinkoSolverTests
    {
        private static PlinkoGenerationConfig FastGeneration(int simulations = 600, int perPair = 6)
        {
            return new PlinkoGenerationConfig
            {
                SimulationsPerEntry = simulations,
                PathsPerPair = perPair,
            };
        }

        private static PlinkoBakeResult BakeGrid(PlinkoGenerationConfig generation = null)
        {
            return PlinkoSolver.Bake(
                PlinkoLayoutGenerator.CreateStaggeredGrid(rows: 12, columns: 9, entryCount: 5,
                    catchPointCount: 9),
                new PlinkoSimConfig(),
                new PlinkoBounceConfig(),
                generation ?? FastGeneration());
        }

        [Test]
        public void Layout_StaggeredGridPlacesPegsEntriesAndTiledCatchPoints()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaggeredGrid(
                rows: 12, columns: 9, entryCount: 5, catchPointCount: 9);

            Assert.AreEqual(12 * 9, layout.Pegs.Count);
            Assert.AreEqual(5, layout.EntryCount);
            Assert.AreEqual(9, layout.CatchPointCount);
            Assert.IsTrue(layout.Validate(out string error), error);

            // No gap between catch points, so a token crossing the line can never miss all of them.
            for (int i = 1; i < layout.CatchPointCount; i++)
            {
                Assert.AreEqual(layout.CatchPoints[i - 1].MaxX, layout.CatchPoints[i].MinX, 1e-4f);
            }

            foreach (Vector2 entry in layout.Entries)
            {
                Assert.IsTrue(layout.PlayArea.Contains(entry), $"Entry {entry} is outside the play area.");
            }
        }

        [Test]
        public void Layout_StaircaseHasOnePegPerEntryAtDistinctHeights()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase(count: 5);

            Assert.AreEqual(5, layout.EntryCount);
            Assert.AreEqual(5, layout.Pegs.Count);
            Assert.AreEqual(5, layout.CatchPointCount);

            var heights = new HashSet<float>();
            for (int i = 0; i < layout.Pegs.Count; i++)
            {
                Assert.IsTrue(heights.Add(layout.Pegs[i].y), "Two pegs share a height.");

                // Each peg sits directly below its own entry.
                Assert.AreEqual(layout.Entries[i].x, layout.Pegs[i].x, 1e-4f);
                Assert.Less(layout.Pegs[i].y, layout.Entries[i].y);
            }
        }

        [Test]
        public void Bounce_ReflectsAboutTheNormalAndLosesEnergy()
        {
            var bounce = new PlinkoBounceConfig { ScatterDegrees = 0f, SpeedScatter = 0f, Restitution = 0.6f };
            var random = new DeterministicRandom(1);

            // Straight down onto the top of a peg mirrors straight back up.
            Vector2 result = bounce.Reflect(new Vector2(0f, -4f), Vector2.up, false, random);

            Assert.Greater(result.y, 0f, "A token dropped onto a peg top must bounce upward.");
            Assert.AreEqual(0f, result.x, 1e-3f);
            Assert.AreEqual(2.4f, result.magnitude, 1e-2f, "Speed should be damped by restitution.");
        }

        [Test]
        public void Bounce_AlwaysLeavesTheSurface()
        {
            var bounce = new PlinkoBounceConfig();
            var random = new DeterministicRandom(7);

            // Sweep incoming directions, including ones grazing along the surface.
            for (int i = 0; i < 360; i += 5)
            {
                float radians = i * Mathf.Deg2Rad;
                var incoming = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 3f;
                Vector2 normal = Vector2.up;

                Vector2 result = bounce.Reflect(incoming, normal, false, random);

                Assert.Greater(
                    Vector2.Dot(result.normalized, normal), 0f,
                    $"Bounce at {i} degrees did not leave the surface, so the token would stick.");
                Assert.GreaterOrEqual(result.magnitude, bounce.MinBounceSpeed - 1e-3f);
            }
        }

        [Test]
        public void Bounce_ScatterProducesManyOutcomesNotTwo()
        {
            var bounce = new PlinkoBounceConfig();
            var random = new DeterministicRandom(99);
            var angles = new HashSet<int>();

            for (int i = 0; i < 200; i++)
            {
                Vector2 result = bounce.Reflect(new Vector2(1f, -3f), Vector2.up, false, random);
                angles.Add(Mathf.RoundToInt(Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg));
            }

            // The old model gave a peg exactly two exits. A real reflection plus scatter must do better.
            Assert.Greater(angles.Count, 10,
                "Bounce scatter collapsed to a handful of outcomes, which is what made drops look scripted.");
        }

        [Test]
        public void Simulator_IsReproducibleForTheSameSeed()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaggeredGrid();
            var simulator = new PlinkoDropSimulator(layout, new PlinkoSimConfig(), new PlinkoBounceConfig());

            PlinkoPath first = simulator.Simulate(0, new DeterministicRandom(1234));
            PlinkoPath second = simulator.Simulate(0, new DeterministicRandom(1234));

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(first.Signature, second.Signature);
            Assert.AreEqual(first.CatchPointIndex, second.CatchPointIndex);
            Assert.AreEqual(first.Duration, second.Duration, 0f, "Durations must be bit identical.");
        }

        [Test]
        public void Simulator_ArcsJoinUpExactly()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaggeredGrid();
            var simulator = new PlinkoDropSimulator(layout, new PlinkoSimConfig(), new PlinkoBounceConfig());
            var random = new DeterministicRandom(55);

            int checkedPaths = 0;
            for (int i = 0; i < 40; i++)
            {
                PlinkoPath path = simulator.Simulate(i % layout.EntryCount, random);
                if (path == null)
                {
                    continue;
                }

                // One continuous simulation, so every arc must start where the previous one ended. This is
                // what the graph based model could not give, and why the token used to jump around pegs.
                for (int a = 1; a < path.Arcs.Count; a++)
                {
                    float gap = (path.Arcs[a].Origin - path.Arcs[a - 1].End).magnitude;
                    Assert.Less(gap, 1e-3f, $"Arc {a} does not start where arc {a - 1} ended.");
                }

                checkedPaths++;
            }

            Assert.Greater(checkedPaths, 0);
        }

        [Test]
        public void Simulator_EveryPathLandsWhereItSaysGeometrically()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaggeredGrid();
            var simulator = new PlinkoDropSimulator(layout, new PlinkoSimConfig(), new PlinkoBounceConfig());
            var random = new DeterministicRandom(808);

            int checkedPaths = 0;
            for (int i = 0; i < 120; i++)
            {
                PlinkoPath path = simulator.Simulate(i % layout.EntryCount, random);
                if (path == null)
                {
                    continue;
                }

                PlinkoCatchPoint target = layout.CatchPoints[path.CatchPointIndex];
                Vector2 landing = path.Evaluate(path.Duration);

                Assert.IsTrue(
                    target.ContainsX(landing.x),
                    $"Recorded catch point {path.CatchPointIndex} but landed at x = {landing.x}.");
                Assert.AreEqual(target.Y, landing.y, 1e-2f);

                checkedPaths++;
            }

            Assert.Greater(checkedPaths, 0);
        }

        [Test]
        public void Simulator_NeverReportsSuccessWithoutReachingACatchPoint()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var simulator = new PlinkoDropSimulator(layout, new PlinkoSimConfig(), new PlinkoBounceConfig());
            var random = new DeterministicRandom(4321);

            for (int i = 0; i < 200; i++)
            {
                PlinkoPath path = simulator.Simulate(i % layout.EntryCount, random);

                if (path == null)
                {
                    Assert.AreNotEqual(PlinkoDropFailure.None, simulator.LastFailure);
                    continue;
                }

                Assert.AreEqual(PlinkoDropFailure.None, simulator.LastFailure);
                Assert.GreaterOrEqual(path.CatchPointIndex, 0);
                Assert.Less(path.CatchPointIndex, layout.CatchPointCount);
                Assert.Greater(path.Duration, 0f);
            }
        }

        [Test]
        public void Solver_GridLandsEveryDropAndCoversMostPairs()
        {
            PlinkoBakeResult result = BakeGrid(FastGeneration(simulations: 1200, perPair: 6));
            string report = result.Report.ToString();

            Assert.IsNull(result.Report.Error, report);
            Assert.IsTrue(result.IsUsable);

            // The spec's no-stuck-tokens rule: essentially every simulated drop must find a catch point.
            Assert.Greater(result.Report.LandingRate, 0.95f, report);

            // With real reflections an outer entry cannot always throw a token to the far side, so full
            // coverage is not a requirement. What matters is that most pairs work and the rest are reported.
            int pairs = result.PathSet.EntryCount * result.PathSet.CatchPointCount;
            int reachable = CountReachablePairs(result.PathSet);

            Assert.Greater(reachable / (float)pairs, 0.85f, report);
            Assert.AreEqual(pairs - reachable, result.Report.UnreachablePairs.Count, report);
        }

        [Test]
        public void Solver_EveryKeptPathEndsInThePairItIsFiledUnder()
        {
            PlinkoBakeResult result = BakeGrid();

            for (int entry = 0; entry < result.PathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < result.PathSet.CatchPointCount; catchPoint++)
                {
                    foreach (PlinkoPath path in result.PathSet.GetPaths(entry, catchPoint))
                    {
                        Assert.AreEqual(entry, path.EntryIndex);
                        Assert.AreEqual(catchPoint, path.CatchPointIndex);
                    }
                }
            }
        }

        [Test]
        public void Solver_KeepsDistinctPathsPerPair()
        {
            PlinkoBakeResult result = BakeGrid();

            for (int entry = 0; entry < result.PathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < result.PathSet.CatchPointCount; catchPoint++)
                {
                    var signatures = new HashSet<ulong>();
                    foreach (PlinkoPath path in result.PathSet.GetPaths(entry, catchPoint))
                    {
                        Assert.IsTrue(
                            signatures.Add(path.Signature), $"Duplicate drop in E{entry}->C{catchPoint}.");
                    }
                }
            }
        }

        [Test]
        public void Solver_IsReproducibleForTheSameSeed()
        {
            PlinkoGenerationConfig generation = FastGeneration();

            PlinkoBakeResult first = BakeGrid(generation);
            PlinkoBakeResult second = BakeGrid(generation);

            Assert.AreEqual(first.Report.TotalPaths, second.Report.TotalPaths);

            for (int entry = 0; entry < first.PathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < first.PathSet.CatchPointCount; catchPoint++)
                {
                    IReadOnlyList<PlinkoPath> a = first.PathSet.GetPaths(entry, catchPoint);
                    IReadOnlyList<PlinkoPath> b = second.PathSet.GetPaths(entry, catchPoint);

                    Assert.AreEqual(a.Count, b.Count, $"Count differs for E{entry}->C{catchPoint}.");
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.AreEqual(a[i].Signature, b[i].Signature);
                    }
                }
            }
        }

        [Test]
        public void Solver_StaircaseReportsWhatItCannotReachRatherThanFailing()
        {
            PlinkoBakeResult result = PlinkoSolver.Bake(
                PlinkoLayoutGenerator.CreateStaircase(count: 5),
                new PlinkoSimConfig(),
                new PlinkoBounceConfig(),
                FastGeneration(simulations: 1500, perPair: 6));

            Assert.IsNull(result.Report.Error, result.Report.ToString());
            Assert.IsTrue(result.IsUsable);
            Assert.Greater(result.Report.TotalPaths, 0, result.Report.ToString());

            // A five peg board cannot throw a token from every entry into every catch point. That is a fact
            // about the layout, and the report must state it instead of hanging or lying.
            int reachablePairs = CountReachablePairs(result.PathSet);

            Assert.Greater(reachablePairs, 0, "The staircase board reached nothing at all.");
            Assert.AreEqual(
                result.PathSet.EntryCount * result.PathSet.CatchPointCount - reachablePairs,
                result.Report.UnreachablePairs.Count,
                "Every unreached pair must appear in the report.");
        }

        [Test]
        public void Solver_RepeatedDrawsForOnePairGiveDifferentPaths()
        {
            PlinkoBakeResult result = BakeGrid(FastGeneration(simulations: 2000, perPair: 6));

            const int Entry = 2;
            const int CatchPoint = 4;

            int poolSize = result.PathSet.GetPathCount(Entry, CatchPoint);
            Assert.Greater(poolSize, 1, "This assertion needs a pool with more than one drop.");

            var random = new DeterministicRandom(11);
            var seen = new HashSet<ulong>();

            // The headline requirement: same entry, same catch point, a different drop each time.
            for (int i = 0; i < poolSize; i++)
            {
                PlinkoPath path = result.PathSet.Draw(Entry, CatchPoint, random);
                Assert.AreEqual(CatchPoint, path.CatchPointIndex);
                Assert.IsTrue(seen.Add(path.Signature), "A drop repeated before the pool was exhausted.");
            }
        }

        [Test]
        public void Solver_ReportsInvalidLayoutInsteadOfThrowing()
        {
            var layout = new PlinkoLayout();
            PlinkoBakeResult result = PlinkoSolver.Bake(layout);

            Assert.IsNotNull(result.Report.Error);
            Assert.IsFalse(result.IsUsable);
            Assert.IsFalse(result.Report.IsComplete);
        }

        [Test]
        public void Path_EvaluateIsContinuousAcrossArcBoundaries()
        {
            PlinkoBakeResult result = BakeGrid();
            PlinkoPath path = null;

            for (int catchPoint = 0; catchPoint < result.PathSet.CatchPointCount && path == null; catchPoint++)
            {
                IReadOnlyList<PlinkoPath> paths = result.PathSet.GetPaths(0, catchPoint);
                if (paths.Count > 0)
                {
                    path = paths[0];
                }
            }

            Assert.IsNotNull(path, "The bake produced no paths from entry 0.");

            Vector2 previous = path.Evaluate(0f);
            const int Samples = 600;
            float longestStep = 0f;

            for (int i = 1; i <= Samples; i++)
            {
                Vector2 current = path.Evaluate(path.Duration * i / Samples);
                longestStep = Mathf.Max(longestStep, (current - previous).magnitude);
                previous = current;
            }

            Assert.Less(longestStep, 0.2f, $"Largest sample-to-sample jump was {longestStep}.");
        }

        [Test]
        public void DeterministicRandom_ProducesTheSameStreamForTheSameSeed()
        {
            var first = new DeterministicRandom(123456);
            var second = new DeterministicRandom(123456);
            var different = new DeterministicRandom(123457);

            bool anyDifference = false;
            for (int i = 0; i < 100; i++)
            {
                uint a = first.NextUInt();
                Assert.AreEqual(a, second.NextUInt());
                anyDifference |= a != different.NextUInt();
            }

            Assert.IsTrue(anyDifference, "Adjacent seeds must not produce the same stream.");
        }

        private static int CountReachablePairs(PlinkoPathSet pathSet)
        {
            int reachable = 0;
            for (int entry = 0; entry < pathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < pathSet.CatchPointCount; catchPoint++)
                {
                    if (pathSet.IsReachable(entry, catchPoint))
                    {
                        reachable++;
                    }
                }
            }

            return reachable;
        }
    }
}
