using UnityEditor;
using UnityEngine;

namespace SombraStudios.Shared.Gameplay.Plinko.Editor
{
    /// <summary>
    /// Adds a bake button to a Plinko board config, so a designer can turn a layout into a path asset without
    /// entering Play mode or writing a script.
    /// </summary>
    [CustomEditor(typeof(PlinkoBoardConfigSO))]
    public class PlinkoBoardConfigSOEditor : UnityEditor.Editor
    {
        private string _lastReport;

        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (PlinkoBoardConfigSO)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Baking", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Simulates the drops now and writes them to a Plinko Path Set asset beside this one. " +
                "Assign that asset to a Plinko Board so a build loads the paths instead of simulating them.",
                MessageType.Info);

            if (!config.Layout.Validate(out string error))
            {
                EditorGUILayout.HelpBox($"The layout is not bakeable: {error}", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Bake Path Set Asset"))
            {
                BakeWithProgress(config);
            }

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Last bake", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(
                    _lastReport, EditorStyles.textArea, GUILayout.MinHeight(140f));
            }
        }

        private void BakeWithProgress(PlinkoBoardConfigSO config)
        {
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Baking Plinko paths",
                    $"Simulating {config.Generation.SimulationsPerEntry} drops per entry...", 0.5f);

                PlinkoBakeReport report = PlinkoPathSetBaker.BakeBesideConfig(config);
                _lastReport = report.ToString();

                // Coverage gaps are a layout fact rather than a bake failure, so surface them without
                // pretending the bake went wrong.
                if (report.Error != null)
                {
                    Debug.LogError(_lastReport, config);
                }
                else if (!report.IsComplete)
                {
                    Debug.LogWarning(_lastReport, config);
                }
                else
                {
                    Debug.Log(_lastReport, config);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
