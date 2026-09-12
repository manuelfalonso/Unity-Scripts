using NUnit.Framework;
using SombraStudios.Shared.Gameplay.Plinko;
using SombraStudios.Shared.Gameplay.Plinko.Editor;
using UnityEditor;
using UnityEngine;

namespace SombraStudios.Shared.Tests.Plinko
{
    /// <summary>
    /// Covers the Editor bake: writing a path set to a real asset, reading it back off disk, and playing it
    /// through a board without simulating.
    /// </summary>
    /// <remarks>
    /// This is the shipping path — a build loads paths rather than simulating them — so it is worth proving
    /// end to end rather than only proving that the in-memory objects round trip.
    /// </remarks>
    public class PlinkoPathSetBakerTests
    {
        private const string Folder = "Assets/PlinkoBakerTestsTemp";
        private const string AssetPath = Folder + "/TestPaths.asset";

        [SetUp]
        public void CreateFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "PlinkoBakerTestsTemp");
            }
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void Baker_WritesAnAssetThatLoadsAndReplaysWithoutSimulating()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            var generation = new PlinkoGenerationConfig { SimulationsPerEntry = 400, PathsPerPair = 4 };

            PlinkoBakeReport report = PlinkoPathSetBaker.Bake(layout, sim, bounce, generation, AssetPath);

            Assert.IsNull(report.Error, report.ToString());
            Assert.Greater(report.TotalPaths, 0, report.ToString());

            // Reload from disk, not from the instance the bake happened to return.
            AssetDatabase.SaveAssets();
            var loaded = AssetDatabase.LoadAssetAtPath<PlinkoPathSetSO>(AssetPath);

            Assert.IsNotNull(loaded, "The bake did not write an asset.");
            Assert.IsTrue(loaded.HasData);
            Assert.AreEqual(report.TotalPaths, loaded.PathCount);
            Assert.IsTrue(
                loaded.Matches(layout, sim, bounce, generation),
                "The written signature does not match the board it was baked from.");

            PlinkoPathSet pathSet = loaded.ToPathSet();
            Assert.IsNotNull(pathSet);
            Assert.AreEqual(report.TotalPaths, pathSet.TotalPathCount);

            // Every stored drop must still end where it says, geometrically, straight off disk.
            var random = new DeterministicRandom(1);
            int checkedPaths = 0;

            for (int entry = 0; entry < pathSet.EntryCount; entry++)
            {
                for (int catchPoint = 0; catchPoint < pathSet.CatchPointCount; catchPoint++)
                {
                    if (!pathSet.IsReachable(entry, catchPoint))
                    {
                        continue;
                    }

                    PlinkoPath path = pathSet.Draw(entry, catchPoint, random);
                    Assert.IsNotNull(path);
                    Assert.AreEqual(catchPoint, path.CatchPointIndex);

                    PlinkoCatchPoint mouth = loaded.Layout.CatchPoints[catchPoint];
                    Vector2 landing = path.Evaluate(path.Duration);

                    Assert.IsTrue(
                        mouth.ContainsX(landing.x),
                        $"E{entry}->C{catchPoint} loaded from disk landed at x = {landing.x}.");
                    Assert.AreEqual(mouth.Y, landing.y, 1e-2f);

                    checkedPaths++;
                }
            }

            Assert.Greater(checkedPaths, 0);
        }

        [Test]
        public void Baker_RebakingUpdatesTheSameAssetRatherThanCreatingAnother()
        {
            PlinkoLayout layout = PlinkoLayoutGenerator.CreateStaircase();
            var sim = new PlinkoSimConfig();
            var bounce = new PlinkoBounceConfig();
            var generation = new PlinkoGenerationConfig { SimulationsPerEntry = 300, PathsPerPair = 3 };

            PlinkoPathSetBaker.Bake(layout, sim, bounce, generation, AssetPath);
            var first = AssetDatabase.LoadAssetAtPath<PlinkoPathSetSO>(AssetPath);
            string firstGuid = AssetDatabase.AssetPathToGUID(AssetPath);
            string firstSignature = first.Signature;

            // Change the board, re-bake to the same path, and the asset must follow rather than fork — a new
            // GUID would silently orphan every scene reference to it.
            PlinkoLayout moved = layout.Clone();
            moved.Pegs[0] += new Vector2(0.25f, 0f);

            PlinkoPathSetBaker.Bake(moved, sim, bounce, generation, AssetPath);
            var second = AssetDatabase.LoadAssetAtPath<PlinkoPathSetSO>(AssetPath);

            Assert.AreEqual(firstGuid, AssetDatabase.AssetPathToGUID(AssetPath));
            Assert.AreNotEqual(firstSignature, second.Signature, "The re-bake did not take.");
            Assert.IsTrue(second.Matches(moved, sim, bounce, generation));
            Assert.IsFalse(second.Matches(layout, sim, bounce, generation));
        }
    }
}
