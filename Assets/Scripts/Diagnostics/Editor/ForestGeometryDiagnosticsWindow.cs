using System;
using System.IO;
using LegendsOfWarAndMagic.Diagnostics;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LegendsOfWarAndMagic.Diagnostics.Editor
{
    public sealed class ForestGeometryDiagnosticsWindow : EditorWindow
    {
        private const string DiagnosticsFolder = "Assets/Diagnostics";

        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxDistance = 0f;
        [SerializeField] private bool frustumOnly = true;
        [SerializeField] private ForestQualityLevel qualityPreset = ForestQualityLevel.High;

        private Vector2 scrollPosition;
        private string lastReport = string.Empty;

        [MenuItem("Tools/Legends of War and Magic/Diagnostics/Forest Geometry Diagnostics")]
        public static void Open()
        {
            GetWindow<ForestGeometryDiagnosticsWindow>("Forest Geometry");
        }

        [MenuItem("Tools/Legends of War and Magic/Diagnostics/Write Forest Geometry Report")]
        public static void WriteReportMenu()
        {
            var snapshot = ForestGeometryDiagnostics.Capture(ResolveDefaultCamera(), 0f, true);
            WriteReports(snapshot);
            Debug.Log("Forest geometry diagnostics report written to Assets/Diagnostics.");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Capture", EditorStyles.boldLabel);
            targetCamera = (Camera)EditorGUILayout.ObjectField("Camera", targetCamera, typeof(Camera), true);
            maxDistance = EditorGUILayout.FloatField("Max Distance", Mathf.Max(0f, maxDistance));
            frustumOnly = EditorGUILayout.Toggle("Frustum Only", frustumOnly);

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture"))
                {
                    Capture(false);
                }

                if (GUILayout.Button("Capture and Write Files"))
                {
                    Capture(true);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Forest Quality Preset", EditorStyles.boldLabel);
            qualityPreset = (ForestQualityLevel)EditorGUILayout.EnumPopup("Preset", qualityPreset);
            if (GUILayout.Button("Apply Preset to Generated Forest Renderers"))
            {
                ApplyPresetToSceneRenderers(qualityPreset);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Last Report Preview", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            EditorGUILayout.TextArea(lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Capture(bool writeFiles)
        {
            var camera = targetCamera != null ? targetCamera : ResolveDefaultCamera();
            var snapshot = ForestGeometryDiagnostics.Capture(camera, maxDistance, frustumOnly);
            lastReport = ForestGeometryDiagnostics.BuildMarkdown(snapshot, 20);
            Debug.Log(lastReport);

            if (writeFiles)
            {
                WriteReports(snapshot);
            }
        }

        private static void ApplyPresetToSceneRenderers(ForestQualityLevel preset)
        {
            var renderers = Object.FindObjectsByType<GeneratedInstancedPropRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].ApplyForestQualityPreset(preset);
                EditorUtility.SetDirty(renderers[i]);
            }

            Debug.Log($"Applied forest quality preset '{preset}' to {renderers.Length} generated instanced prop renderer(s).");
        }

        private static Camera ResolveDefaultCamera()
        {
            if (Camera.main != null)
            {
                return Camera.main;
            }

            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                return SceneView.lastActiveSceneView.camera;
            }

            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return cameras.Length > 0 ? cameras[0] : null;
        }

        private static void WriteReports(ForestGeometrySnapshot snapshot)
        {
            Directory.CreateDirectory(DiagnosticsFolder);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var markdownPath = Path.Combine(DiagnosticsFolder, $"ForestGeometryDiagnostics-{timestamp}.md");
            var csvPath = Path.Combine(DiagnosticsFolder, $"ForestGeometryDiagnostics-{timestamp}.csv");

            File.WriteAllText(markdownPath, ForestGeometryDiagnostics.BuildMarkdown(snapshot, 20));
            File.WriteAllText(csvPath, ForestGeometryDiagnostics.BuildCsv(snapshot));
            AssetDatabase.Refresh();
        }
    }
}
