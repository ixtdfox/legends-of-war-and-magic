using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class RuntimeGeometryDebugPanel : MonoBehaviour
    {
        private const int TypeCapacity = 8;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool visibleOnStart = true;
        [SerializeField] private float updateInterval = 0.5f;
        [SerializeField] private int topOffenderCount = 6;

        private readonly StringBuilder builder = new(4096);
        private readonly List<TypeStats> typeStats = new(TypeCapacity);
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];

        private Canvas canvas;
        private Text bodyText;
        private float nextUpdateTime;
        private bool visible;

        public static RuntimeGeometryDebugPanel Ensure(Camera camera = null)
        {
            var existing = FindFirstObjectByType<RuntimeGeometryDebugPanel>();
            if (existing != null)
            {
                if (camera != null)
                {
                    existing.targetCamera = camera;
                }

                return existing;
            }

            var panelObject = new GameObject("Runtime Geometry Debug Panel");
            var panel = panelObject.AddComponent<RuntimeGeometryDebugPanel>();
            panel.targetCamera = camera;
            return panel;
        }

        private void Awake()
        {
            visible = visibleOnStart;
            BuildUi();
            SetVisible(visible);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetVisible(!visible);
            }

            if (!visible || Time.unscaledTime < nextUpdateTime)
            {
                return;
            }

            nextUpdateTime = Time.unscaledTime + Mathf.Max(0.1f, updateInterval);
            Refresh();
        }

        private void BuildUi()
        {
            canvas = RuntimeUiFactory.CreateCanvas("Runtime Geometry Debug Canvas");
            canvas.sortingOrder = 5000;
            canvas.transform.SetParent(transform, false);

            var panelImage = RuntimeUiFactory.CreateImage(canvas.transform, "Geometry Debug Panel", new Color(0.02f, 0.025f, 0.025f, 0.86f));
            var panelRect = panelImage.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(610f, 650f);

            bodyText = RuntimeUiFactory.CreateText(
                panelImage.transform,
                "Geometry Debug Text",
                string.Empty,
                20,
                new Color(0.88f, 0.95f, 0.84f, 1f),
                TextAnchor.UpperLeft);
            bodyText.resizeTextForBestFit = false;
            bodyText.horizontalOverflow = HorizontalWrapMode.Overflow;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.lineSpacing = 0.92f;
            RuntimeUiFactory.Stretch(bodyText.gameObject, new Vector2(18f, 14f), new Vector2(-18f, -14f));
        }

        private void SetVisible(bool value)
        {
            visible = value;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }

            nextUpdateTime = 0f;
        }

        private void Refresh()
        {
            var camera = ResolveCamera();
            if (camera == null || bodyText == null)
            {
                return;
            }

            var snapshot = ForestGeometryDiagnostics.Capture(camera, 0f, true);
            BuildTypeStats(snapshot);
            var editorStats = ReadEditorUnityStats();

            FrameTimingManager.CaptureFrameTimings();
            var timingCount = FrameTimingManager.GetLatestTimings(1, frameTimings);
            var cpuMs = timingCount > 0 ? frameTimings[0].cpuFrameTime : 0d;
            var gpuMs = timingCount > 0 ? frameTimings[0].gpuFrameTime : 0d;
            var fps = Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f;

            builder.Clear();
            builder.AppendLine("GEOMETRY DEBUG  [F3]");
            builder.AppendLine($"Camera: {camera.name}");
            builder.AppendLine($"FPS: {fps:0.0}  Frame: {Time.smoothDeltaTime * 1000f:0.0} ms  CPU/GPU: {FormatMs(cpuMs)} / {FormatMs(gpuMs)}");
            builder.AppendLine($"Unique visible: {FormatCount(snapshot.VisibleTriangles)} tris   {FormatCount(snapshot.VisibleVertices)} verts   shadow groups: {snapshot.ShadowCastingRecordCount}");
            if (editorStats.Available)
            {
                builder.AppendLine($"Unity submitted: {FormatCount(editorStats.Triangles)} tris   {FormatCount(editorStats.Vertices)} verts   batches: {editorStats.Batches}   setpass: {editorStats.SetPassCalls}   casters: {editorStats.ShadowCasters}");
                builder.AppendLine($"Extra pass/terrain cost: +{FormatCount(Math.Max(0L, editorStats.Triangles - snapshot.VisibleTriangles))} tris   +{FormatCount(Math.Max(0L, editorStats.Vertices - snapshot.VisibleVertices))} verts");
            }
            else
            {
                builder.AppendLine("Unity submitted: n/a outside Editor Stats");
            }

            builder.AppendLine($"Groups: {snapshot.Groups.Count}   renderers: {snapshot.RendererRecords.Count}   terrain: {snapshot.TerrainRecords.Count}   instanced: {snapshot.InstancedRecords.Count}   grass records: {snapshot.GpuGrassRecords.Count}");
            builder.AppendLine("Unique visible counts source geometry once; terrain is estimated. Unity submitted includes depth, shadows and other render passes.");
            builder.AppendLine();
            builder.AppendLine("BY TYPE");
            builder.AppendLine("Type             Count      Tris       Verts      Shadows");
            for (var i = 0; i < typeStats.Count; i++)
            {
                var stats = typeStats[i];
                builder.AppendLine($"{stats.Name,-15} {stats.Count,5} {FormatCount(stats.Triangles),10} {FormatCount(stats.Vertices),10} {stats.ShadowGroups,7}");
            }

            AppendGrassStats(snapshot);
            AppendTopOffenders(snapshot);

            bodyText.text = builder.ToString();
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null && targetCamera.isActiveAndEnabled)
            {
                return targetCamera;
            }

            targetCamera = Camera.main != null
                ? Camera.main
                : FindFirstObjectByType<Camera>();
            return targetCamera;
        }

        private void BuildTypeStats(ForestGeometrySnapshot snapshot)
        {
            typeStats.Clear();
            for (var i = 0; i < snapshot.Groups.Count; i++)
            {
                var group = snapshot.Groups[i];
                var typeName = ClassifyGroup(group);
                var index = FindTypeIndex(typeName);
                if (index < 0)
                {
                    typeStats.Add(new TypeStats(typeName));
                    index = typeStats.Count - 1;
                }

                var stats = typeStats[index];
                stats.Count += group.Count;
                stats.Triangles += group.TotalTriangles;
                stats.Vertices += group.TotalVertices;
                if (!string.Equals(group.ShadowLabel, "Off", StringComparison.OrdinalIgnoreCase))
                {
                    stats.ShadowGroups++;
                }

                typeStats[index] = stats;
            }

            typeStats.Sort((left, right) => right.Triangles.CompareTo(left.Triangles));
        }

        private int FindTypeIndex(string typeName)
        {
            for (var i = 0; i < typeStats.Count; i++)
            {
                if (typeStats[i].Name == typeName)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ClassifyGroup(ForestGeometryGroup group)
        {
            if (group.SourceType == "GPU Grass")
            {
                return "Grass GPU";
            }

            if (group.SourceType == "Terrain")
            {
                return "Terrain";
            }

            var key = group.DisplayName.ToLowerInvariant();
            if (key.Contains("bush"))
            {
                return "Bushes";
            }

            if (key.Contains("tree") || key.Contains("leaves") || key.Contains("billboard"))
            {
                return "Trees";
            }

            if (key.Contains("rock"))
            {
                return "Rocks";
            }

            if (key.Contains("grass"))
            {
                return "Grass Props";
            }

            if (key.Contains("plant") || key.Contains("weed") || key.Contains("flower"))
            {
                return "Plants";
            }

            if (key.Contains("terrain"))
            {
                return "Terrain";
            }

            if (key.Contains("water"))
            {
                return "Water";
            }

            return group.SourceType == "Instanced" ? "Props" : "Other";
        }

        private void AppendGrassStats(ForestGeometrySnapshot snapshot)
        {
            if (snapshot.GpuGrassRecords.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("GPU GRASS");
            builder.AppendLine("LOD              Clusters  Clumps    Tris   Batches  Gen");
            for (var i = 0; i < snapshot.GpuGrassRecords.Count; i++)
            {
                var record = snapshot.GpuGrassRecords[i];
                builder.AppendLine($"{Shorten(record.LodName, 16),-16} {record.VisibleClusters,7} {record.VisibleClumps,7} {FormatCount(record.VisibleTriangles),7} {record.EstimatedBatches,7} {record.RuntimeGeneratedInRender,4}");
            }
        }

        private void AppendTopOffenders(ForestGeometrySnapshot snapshot)
        {
            builder.AppendLine();
            builder.AppendLine("TOP OFFENDERS");
            builder.AppendLine("Source           Count      Tris  Shadows");
            var count = Mathf.Min(Mathf.Max(1, topOffenderCount), snapshot.Groups.Count);
            for (var i = 0; i < count; i++)
            {
                var group = snapshot.Groups[i];
                builder.AppendLine($"{Shorten(group.DisplayName, 27),-27} {group.Count,5} {FormatCount(group.TotalTriangles),9} {group.ShadowLabel,8}");
            }
        }

        private static string FormatCount(long value)
        {
            if (value >= 1000000)
            {
                return $"{value / 1000000f:0.00}M";
            }

            if (value >= 1000)
            {
                return $"{value / 1000f:0.0}k";
            }

            return value.ToString();
        }

        private static string FormatMs(double value)
        {
            return value > 0d ? $"{value:0.0} ms" : "n/a";
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
        }

        private static EditorStats ReadEditorUnityStats()
        {
#if UNITY_EDITOR
            var statsType = Type.GetType("UnityEditor.UnityStats, UnityEditor");
            if (statsType == null)
            {
                return default;
            }

            return new EditorStats(
                ReadIntProperty(statsType, "triangles"),
                ReadIntProperty(statsType, "vertices"),
                ReadIntProperty(statsType, "batches"),
                ReadIntProperty(statsType, "setPassCalls"),
                ReadIntProperty(statsType, "shadowCasters"));
#else
            return default;
#endif
        }

#if UNITY_EDITOR
        private static long ReadIntProperty(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
            {
                return 0;
            }

            var value = property.GetValue(null);
            return value switch
            {
                int intValue => intValue,
                long longValue => longValue,
                uint uintValue => uintValue,
                ulong ulongValue => ulongValue > long.MaxValue ? long.MaxValue : (long)ulongValue,
                float floatValue => Mathf.RoundToInt(floatValue),
                double doubleValue => (long)Math.Round(doubleValue),
                _ => 0
            };
        }
#endif

        private readonly struct EditorStats
        {
            public EditorStats(long triangles, long vertices, long batches, long setPassCalls, long shadowCasters)
            {
                Triangles = triangles;
                Vertices = vertices;
                Batches = batches;
                SetPassCalls = setPassCalls;
                ShadowCasters = shadowCasters;
            }

            public bool Available => Triangles > 0 || Vertices > 0 || Batches > 0 || SetPassCalls > 0 || ShadowCasters > 0;
            public long Triangles { get; }
            public long Vertices { get; }
            public long Batches { get; }
            public long SetPassCalls { get; }
            public long ShadowCasters { get; }
        }

        private struct TypeStats
        {
            public TypeStats(string name)
            {
                Name = name;
                Count = 0;
                Triangles = 0;
                Vertices = 0;
                ShadowGroups = 0;
            }

            public string Name;
            public int Count;
            public long Triangles;
            public long Vertices;
            public int ShadowGroups;
        }
    }
}
