using System.Collections.Generic;
using NUnit.Framework;
using SombraStudios.Shared.Gameplay.Plinko;
using UnityEngine;

namespace SombraStudios.Shared.Tests.Plinko
{
    /// <summary>
    /// Covers baking a path set into an asset and loading it back: the stored drops must replay identically
    /// to the ones that were simulated, and the signature must catch data baked from a different board.
    /// </summary>
    public class PlinkoPathSetAssetTests
    {
        private static PlinkoGenerationConfig FastGeneration()
        {
            return new PlinkoGenerationConfig { SimulationsPerEntry = 400, PathsPerPair = 4 };
        }

        private static PlinkoPathSetSO StoreRoundTrip(
            PlinkoLayout layout, PlinkoSimConfig sim, PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation, out PlinkoBakeResult result)
        {
            result = PlinkoSolver.Bake(layout, sim, bounce, generation);
            Assert.IsTrue(result.IsUsable, result.Report.ToString());

            var asset = ScriptableObject.CreateInstance<PlinkoPathSetSO>();
            asset.Store(layout, result.PathSet, sim.GravityVector, result.Report.Signature,
                result.Report.ToString());

            return asset;
        }

        [Test]
        public void Asset_RoundTripsEveryPathExactly()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();

            PlinkoPathSetSO asset = StoreRoundTrip(
                layout, sim, new PlinkoBounceConfig(), FastGeneration(), out PlinkoBakeResult result);

            PlinkoPathSet loaded = asset.ToPathSet();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(result.PathSet.EntryCount, loaded.EntryCount);
            Assert.AreEqual(result.PathSet.CatchPointCount, loaded.CatchPointCount);
            Assert.AreEqual(result.PathSet.TotalPathCount, loaded.TotalPathCount);

            int compared = 0;

            for (int entry = 0; entry < loaded.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < loaded.CatchPointCount; catchPoint++)
                {
                    IReadOnlyList<PlinkoPath> original = result.PathSet.GetPaths(entry, catchPoint);
                    IReadOnlyList<PlinkoPath> restored = loaded.GetPaths(entry, catchPoint);

                    Assert.AreEqual(original.Count, restored.Count, $"E{entry}->C{catchPoint}");

                    for (int i = 0; i < original.Count; i++)
                    {
                        PlinkoPath a = original[i];
                        PlinkoPath b = restored[i];

                        Assert.AreEqual(a.EntryIndex, b.EntryIndex);
                        Assert.AreEqual(a.CatchPointIndex, b.CatchPointIndex);
                        Assert.AreEqual(a.Arcs.Count, b.Arcs.Count);
                        Assert.AreEqual(a.Contacts.Count, b.Contacts.Count);
                        Assert.AreEqual(a.Duration, b.Duration, 1e-4f);
                        Assert.AreEqual(a.Signature, b.Signature, "Signatures must survive the round trip.");

                        // Positions are what the player sees, so they are what has to match.
                        const int Samples = 40;
                        for (int s = 0; s <= Samples; s++)
                        {
                            float t = a.Duration * s / Samples;
                            Assert.Less(
                                (a.Evaluate(t) - b.Evaluate(t)).magnitude, 1e-3f,
                                $"E{entry}->C{catchPoint} path {i} diverged at t = {t}.");
                        }

                        for (int c = 0; c < a.Contacts.Count; c++)
                        {
                            Assert.AreEqual(a.Contacts[c].Kind, b.Contacts[c].Kind);
                            Assert.AreEqual(a.Contacts[c].Index, b.Contacts[c].Index);
                            Assert.AreEqual(a.Contacts[c].Time, b.Contacts[c].Time, 1e-3f);
                        }

                        compared++;
                    }
                }
            }

            Assert.Greater(compared, 0);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Asset_SignatureMatchesTheBoardItWasBakedFrom()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            PlinkoGenerationConfig generation = FastGeneration();

            PlinkoPathSetSO asset = StoreRoundTrip(layout, sim, bounce, generation, out _);

            Assert.IsTrue(asset.Matches(layout, sim, bounce, generation));

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Asset_SignatureRejectsAMovedPeg()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            PlinkoGenerationConfig generation = FastGeneration();

            PlinkoPathSetSO asset = StoreRoundTrip(layout, sim, bounce, generation, out _);

            // A peg moved by a millimetre invalidates every recorded drop, and the token would sail through
            // where it used to be. This is the failure the signature exists to catch.
            PlinkoLayout moved = layout.Clone();
            moved.Pegs[0] += new Vector2(0.001f, 0f);

            Assert.IsFalse(asset.Matches(moved, sim, bounce, generation));

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Asset_SignatureRejectsChangedKinematicsAndBounce()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            PlinkoGenerationConfig generation = FastGeneration();

            PlinkoPathSetSO asset = StoreRoundTrip(layout, sim, bounce, generation, out _);

            Assert.IsFalse(
                asset.Matches(layout, new PlinkoSimConfig { Gravity = sim.Gravity + 0.01f }, bounce, generation),
                "A gravity change must invalidate baked paths.");

            Assert.IsFalse(
                asset.Matches(layout, sim, new PlinkoBounceConfig { Restitution = 0.71f }, generation),
                "A restitution change must invalidate baked paths.");

            Assert.IsFalse(
                asset.Matches(layout, sim, bounce, new PlinkoGenerationConfig
                {
                    SimulationsPerEntry = generation.SimulationsPerEntry,
                    PathsPerPair = generation.PathsPerPair,
                    Seed = generation.Seed + 1,
                }),
                "A seed change must invalidate baked paths.");

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Asset_CarriesTheLayoutSoAsceneNeedsOnlyTheAsset()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            PlinkoPathSetSO asset = StoreRoundTrip(
                layout, new PlinkoSimConfig(), new PlinkoBounceConfig(), FastGeneration(), out _);

            Assert.AreEqual(layout.Pegs.Count, asset.Layout.Pegs.Count);
            Assert.AreEqual(layout.EntryCount, asset.Layout.EntryCount);
            Assert.AreEqual(layout.CatchPointCount, asset.Layout.CatchPointCount);
            Assert.AreEqual(layout.Walls.Count, asset.Layout.Walls.Count);

            for (int i = 0; i < layout.Pegs.Count; i++)
            {
                Assert.AreEqual(layout.Pegs[i], asset.Layout.Pegs[i]);
            }

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Solver_DropsAreIndependentOfHowManyWereSimulatedBefore()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            var simulator = new PlinkoDropSimulator(layout, sim, bounce);

            const int Seed = 4242;

            // Simulating drop 7 straight away must give the same drop as reaching it after six others,
            // which is what per-drop seeding buys and what a parallel bake would rely on.
            var direct = new DeterministicRandom(PlinkoSolver.DeriveDropSeed(Seed, 0, 7));
            PlinkoPath alone = simulator.Simulate(0, direct);

            var reused = new DeterministicRandom(0);
            PlinkoPath afterOthers = null;
            for (int i = 0; i <= 7; i++)
            {
                reused.Reseed(PlinkoSolver.DeriveDropSeed(Seed, 0, i));
                PlinkoPath path = simulator.Simulate(0, reused);
                if (i == 7)
                {
                    afterOthers = path;
                }
            }

            Assert.IsNotNull(alone);
            Assert.IsNotNull(afterOthers);
            Assert.AreEqual(alone.Signature, afterOthers.Signature);
            Assert.AreEqual(alone.CatchPointIndex, afterOthers.CatchPointIndex);
            Assert.AreEqual(alone.Duration, afterOthers.Duration, 0f);
        }
    }
}
