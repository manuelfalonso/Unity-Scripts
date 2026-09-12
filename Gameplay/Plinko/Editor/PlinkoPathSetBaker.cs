using System.IO;
using UnityEditor;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko.Editor
{
    /// <summary>
    /// Runs a Plinko bake and writes the result to a <see cref="PlinkoPathSetSO"/> asset.
    /// </summary>
    /// <remarks>
    /// Editor only, and deliberately not required at runtime: a board with no baked asset still simulates on
    /// Awake. This exists so that a shipped build does not have to.
    /// <para>
    /// <see cref="Bake(PlinkoLayout, PlinkoSimConfig, PlinkoBounceConfig, PlinkoGenerationConfig, string)"/>
    /// is public and static so a build script or CI step can regenerate every level's paths without opening
    /// a window.
    /// </para>
    /// </remarks>
    public static class PlinkoPathSetBaker
    {
        /// <summary>
        /// Bakes a layout and writes or updates the asset at <paramref name="assetPath"/>.
        /// </summary>
        /// <param name="layout">Board to simulate.</param>
        /// <param name="sim">Ball size, gravity and drop budget.</param>
        /// <param name="bounce">How pegs and walls throw the token back.</param>
        /// <param name="generation">How many drops to simulate and how many to keep.</param>
        /// <param name="assetPath">Project relative path ending in <c>.asset</c>.</param>
        /// <returns>The bake report, so a caller can check coverage before shipping it.</returns>
        public static PlinkoBakeReport Bake(
            PlinkoLayout layout,
            PlinkoSimConfig sim,
            PlinkoBounceConfig bounce,
            PlinkoGenerationConfig generation,
            string assetPath)
        {
            PlinkoBakeResult result = PlinkoSolver.Bake(layout, sim, bounce, generation);

            if (!result.IsUsable)
            {
                Debug.LogError($"Plinko bake failed, no asset written: {result.Report.Error}");
                return result.Report;
            }

            var asset = AssetDatabase.LoadAssetAtPath<PlinkoPathSetSO>(assetPath);
            bool isNew = asset == null;

            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<PlinkoPathSetSO>();
            }

            asset.Store(layout, result.PathSet, sim.GravityVector, result.Report.Signature,
                result.Report.ToString());

            if (isNew)
            {
                string directory = Path.GetDirectoryName(assetPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();

            return result.Report;
        }

        /// <summary>
        /// Bakes the layout and settings held by a config asset, writing the paths beside it.
        /// </summary>
        /// <param name="config">Config asset holding the layout to bake.</param>
        /// <returns>The bake report.</returns>
        public static PlinkoBakeReport BakeBesideConfig(PlinkoBoardConfigSO config)
        {
            string configPath = AssetDatabase.GetAssetPath(config);
            string directory = Path.GetDirectoryName(configPath);
            string assetPath = $"{directory}/{config.name} Paths.asset".Replace('\\', '/');

            return Bake(config.Layout, config.Sim, config.Bounce, config.Generation, assetPath);
        }
    }
}
