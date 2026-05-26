using System;
using System.Collections.Generic;
using System.Text;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LegendsOfWarAndMagic.Diagnostics
{
    public static class ForestGeometryDiagnostics
    {
        public static ForestGeometrySnapshot Capture(Camera camera, float maxDistance, bool frustumOnly)
        {
            var snapshot = new ForestGeometrySnapshot(camera, maxDistance, frustumOnly);
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var hasCamera = camera != null;
            var cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            var planes = hasCamera ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            var maxDistanceSqr = maxDistance > 0f ? maxDistance * maxDistance : float.PositiveInfinity;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var distanceSqr = hasCamera
                    ? (renderer.bounds.center - cameraPosition).sqrMagnitude
                    : 0f;
                var insideDistance = !hasCamera || distanceSqr <= maxDistanceSqr;
                var insideFrustum = !hasCamera || !frustumOnly || GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
                var visible = insideDistance && insideFrustum;
                if (!visible)
                {
                    continue;
                }

                var mesh = ResolveMesh(renderer);
                if (mesh == null)
                {
                    continue;
                }

                var lodGroup = renderer.GetComponentInParent<LODGroup>();
                var configuredLod = ResolveConfiguredLodIndex(lodGroup, renderer);
                var estimatedLod = EstimateActiveLodIndex(lodGroup, camera);
                var prefabName = ResolvePrefabName(renderer);
                var materialNames = ResolveMaterialNames(renderer);
                var record = new ForestRendererRecord(
                    renderer.name,
                    prefabName,
                    mesh.name,
                    materialNames,
                    mesh.vertexCount,
                    CountTriangles(mesh),
                    renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0,
                    true,
                    renderer.shadowCastingMode,
                    renderer.receiveShadows,
                    lodGroup != null,
                    configuredLod,
                    estimatedLod,
                    "Renderer");

                snapshot.AddRenderer(record);
            }

            var instancedRenderers = Object.FindObjectsByType<GeneratedInstancedPropRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < instancedRenderers.Length; i++)
            {
                var instanced = instancedRenderers[i];
                if (instanced == null || !instanced.isActiveAndEnabled)
                {
                    continue;
                }

                var diagnostics = new List<GeneratedInstancedPropRenderer.GeneratedInstancedPropDiagnostic>();
                instanced.AddDiagnostics(camera, diagnostics);
                for (var diagnosticIndex = 0; diagnosticIndex < diagnostics.Count; diagnosticIndex++)
                {
                    snapshot.AddInstanced(diagnostics[diagnosticIndex]);
                }
            }

            snapshot.Sort();
            return snapshot;
        }

        public static string BuildMarkdown(ForestGeometrySnapshot snapshot, int topCount)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Forest Geometry Diagnostics");
            sb.AppendLine();
            sb.AppendLine($"- Camera: {snapshot.CameraName}");
            sb.AppendLine($"- Max distance: {(snapshot.MaxDistance > 0f ? snapshot.MaxDistance.ToString("0.##") : "unlimited")}");
            sb.AppendLine($"- Frustum only: {snapshot.FrustumOnly}");
            sb.AppendLine($"- Visible renderer records: {snapshot.RendererRecords.Count}");
            sb.AppendLine($"- Instanced draw groups: {snapshot.InstancedRecords.Count}");
            sb.AppendLine($"- Visible triangles: {snapshot.VisibleTriangles:N0}");
            sb.AppendLine($"- Visible vertices: {snapshot.VisibleVertices:N0}");
            sb.AppendLine($"- Shadow casting records: {snapshot.ShadowCastingRecordCount:N0}");
            sb.AppendLine();
            sb.AppendLine("## Top Offenders");
            sb.AppendLine();
            sb.AppendLine("| Source | Mesh/Prefab | Count | Tris per instance | Total visible tris | Visible verts | LOD | Shadows | Notes |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---|---|---|");

            var groups = snapshot.Groups;
            var count = Mathf.Min(topCount, groups.Count);
            for (var i = 0; i < count; i++)
            {
                var group = groups[i];
                sb.AppendLine(
                    $"| {group.SourceType} | {Escape(group.DisplayName)} | {group.Count} | {group.TrianglesPerInstance:N0} | {group.TotalTriangles:N0} | {group.TotalVertices:N0} | {group.LodLabel} | {group.ShadowLabel} | {Escape(group.Notes)} |");
            }

            sb.AppendLine();
            sb.AppendLine("## Instanced LOD Groups");
            sb.AppendLine();
            sb.AppendLine("| Prefabs | Mesh | Material | LOD | Visible instances | Tris per instance | Visible tris | Shadows |");
            sb.AppendLine("|---|---|---|---:|---:|---:|---:|---|");

            var instancedRecords = snapshot.InstancedRecords;
            for (var i = 0; i < instancedRecords.Count; i++)
            {
                var record = instancedRecords[i];
                sb.AppendLine(
                    $"| {Escape(record.SourcePrefabs)} | {Escape(record.MeshName)} | {Escape(record.MaterialName)} | {record.LodIndex} | {record.VisibleInstances} | {record.TrianglesPerInstance:N0} | {record.VisibleTriangles:N0} | {record.ShadowCastingMode} |");
            }

            return sb.ToString();
        }

        public static string BuildCsv(ForestGeometrySnapshot snapshot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("source,display_name,count,triangles_per_instance,total_triangles,total_vertices,lod,shadows,notes");
            for (var i = 0; i < snapshot.Groups.Count; i++)
            {
                var group = snapshot.Groups[i];
                sb.AppendLine(string.Join(",",
                    Csv(group.SourceType),
                    Csv(group.DisplayName),
                    group.Count.ToString(),
                    group.TrianglesPerInstance.ToString(),
                    group.TotalTriangles.ToString(),
                    group.TotalVertices.ToString(),
                    Csv(group.LodLabel),
                    Csv(group.ShadowLabel),
                    Csv(group.Notes)));
            }

            return sb.ToString();
        }

        private static Mesh ResolveMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter != null ? meshFilter.sharedMesh : null;
        }

        private static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
            {
                return 0;
            }

            var triangles = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                triangles += (int)(mesh.GetIndexCount(i) / 3);
            }

            return triangles;
        }

        private static string ResolveMaterialNames(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                return string.Empty;
            }

            var names = new string[materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                names[i] = materials[i] != null ? materials[i].name : "null";
            }

            return string.Join("+", names);
        }

        private static int ResolveConfiguredLodIndex(LODGroup lodGroup, Renderer renderer)
        {
            if (lodGroup == null || renderer == null)
            {
                return -1;
            }

            var lods = lodGroup.GetLODs();
            for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
            {
                var renderers = lods[lodIndex].renderers;
                if (renderers == null)
                {
                    continue;
                }

                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    if (renderers[rendererIndex] == renderer)
                    {
                        return lodIndex;
                    }
                }
            }

            return -1;
        }

        private static int EstimateActiveLodIndex(LODGroup lodGroup, Camera camera)
        {
            if (lodGroup == null || camera == null)
            {
                return -1;
            }

            var distance = Vector3.Distance(camera.transform.position, lodGroup.transform.TransformPoint(lodGroup.localReferencePoint));
            if (distance <= 0.01f)
            {
                return 0;
            }

            var scale = lodGroup.transform.lossyScale;
            var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var relativeHeight = lodGroup.size * maxScale /
                                 (2f * distance * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            relativeHeight *= QualitySettings.lodBias;

            var lods = lodGroup.GetLODs();
            for (var i = 0; i < lods.Length; i++)
            {
                if (relativeHeight >= lods[i].screenRelativeTransitionHeight)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ResolvePrefabName(Renderer renderer)
        {
#if UNITY_EDITOR
            var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(renderer.gameObject);
            if (source != null)
            {
                return source.name;
            }
#endif
            return string.Empty;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("|", "\\|");
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }

    public sealed class ForestGeometrySnapshot
    {
        private readonly Dictionary<string, ForestGeometryGroup> groupsByKey = new();

        public ForestGeometrySnapshot(Camera camera, float maxDistance, bool frustumOnly)
        {
            CameraName = camera != null ? camera.name : "None";
            MaxDistance = Mathf.Max(0f, maxDistance);
            FrustumOnly = frustumOnly;
        }

        public string CameraName { get; }
        public float MaxDistance { get; }
        public bool FrustumOnly { get; }
        public List<ForestRendererRecord> RendererRecords { get; } = new();
        public List<GeneratedInstancedPropRenderer.GeneratedInstancedPropDiagnostic> InstancedRecords { get; } = new();
        public List<ForestGeometryGroup> Groups { get; } = new();
        public long VisibleTriangles { get; private set; }
        public long VisibleVertices { get; private set; }
        public int ShadowCastingRecordCount { get; private set; }

        public void AddRenderer(ForestRendererRecord record)
        {
            RendererRecords.Add(record);
            VisibleTriangles += record.TriangleCount;
            VisibleVertices += record.VertexCount;
            if (record.ShadowCastingMode != ShadowCastingMode.Off)
            {
                ShadowCastingRecordCount++;
            }

            var displayName = string.IsNullOrWhiteSpace(record.PrefabName)
                ? record.MeshName
                : $"{record.PrefabName} / {record.MeshName}";
            var key = $"Renderer|{record.PrefabName}|{record.MeshName}|{record.MaterialNames}|{record.ConfiguredLodIndex}|{record.ShadowCastingMode}";
            var group = GetOrCreateGroup(key, "Renderer", displayName, record.TriangleCount, record.VertexCount);
            group.Add(
                record.TriangleCount,
                record.VertexCount,
                record.HasLodGroup ? $"configured {record.ConfiguredLodIndex}, estimated {record.EstimatedLodIndex}" : "none",
                record.ShadowCastingMode.ToString(),
                record.HasLodGroup ? "LODGroup" : "No LODGroup");
        }

        public void AddInstanced(GeneratedInstancedPropRenderer.GeneratedInstancedPropDiagnostic record)
        {
            InstancedRecords.Add(record);
            VisibleTriangles += record.VisibleTriangles;
            VisibleVertices += record.VisibleVertices;
            if (record.ShadowCastingMode != ShadowCastingMode.Off && record.VisibleInstances > 0)
            {
                ShadowCastingRecordCount++;
            }

            var displayName = $"{record.SourcePrefabs} / {record.MeshName} / {record.MaterialName}";
            var key = $"Instanced|{record.SourcePrefabs}|{record.MeshName}|{record.MaterialName}|{record.LodIndex}|{record.ShadowCastingMode}";
            var group = GetOrCreateGroup(key, "Instanced", displayName, record.TrianglesPerInstance, record.VerticesPerInstance);
            group.Add(
                record.VisibleTriangles,
                record.VisibleVertices,
                record.UsesDistanceLod ? $"LOD{record.LodIndex}" : "none",
                record.ShadowCastingMode.ToString(),
                record.UsesDistanceLod ? "Generated instanced LOD" : "Generated instanced");
            group.SetCount(record.VisibleInstances);
        }

        public void Sort()
        {
            Groups.Clear();
            foreach (var group in groupsByKey.Values)
            {
                Groups.Add(group);
            }

            Groups.Sort((left, right) => right.TotalTriangles.CompareTo(left.TotalTriangles));
            InstancedRecords.Sort((left, right) => right.VisibleTriangles.CompareTo(left.VisibleTriangles));
        }

        private ForestGeometryGroup GetOrCreateGroup(
            string key,
            string sourceType,
            string displayName,
            int trianglesPerInstance,
            int verticesPerInstance)
        {
            if (groupsByKey.TryGetValue(key, out var group))
            {
                return group;
            }

            group = new ForestGeometryGroup(sourceType, displayName, trianglesPerInstance, verticesPerInstance);
            groupsByKey.Add(key, group);
            return group;
        }
    }

    public readonly struct ForestRendererRecord
    {
        public ForestRendererRecord(
            string objectName,
            string prefabName,
            string meshName,
            string materialNames,
            int vertexCount,
            int triangleCount,
            int materialCount,
            bool isVisible,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows,
            bool hasLodGroup,
            int configuredLodIndex,
            int estimatedLodIndex,
            string sourceType)
        {
            ObjectName = objectName;
            PrefabName = prefabName;
            MeshName = meshName;
            MaterialNames = materialNames;
            VertexCount = vertexCount;
            TriangleCount = triangleCount;
            MaterialCount = materialCount;
            IsVisible = isVisible;
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
            HasLodGroup = hasLodGroup;
            ConfiguredLodIndex = configuredLodIndex;
            EstimatedLodIndex = estimatedLodIndex;
            SourceType = sourceType;
        }

        public string ObjectName { get; }
        public string PrefabName { get; }
        public string MeshName { get; }
        public string MaterialNames { get; }
        public int VertexCount { get; }
        public int TriangleCount { get; }
        public int MaterialCount { get; }
        public bool IsVisible { get; }
        public ShadowCastingMode ShadowCastingMode { get; }
        public bool ReceiveShadows { get; }
        public bool HasLodGroup { get; }
        public int ConfiguredLodIndex { get; }
        public int EstimatedLodIndex { get; }
        public string SourceType { get; }
    }

    public sealed class ForestGeometryGroup
    {
        public ForestGeometryGroup(string sourceType, string displayName, int trianglesPerInstance, int verticesPerInstance)
        {
            SourceType = sourceType;
            DisplayName = displayName;
            TrianglesPerInstance = trianglesPerInstance;
            VerticesPerInstance = verticesPerInstance;
        }

        public string SourceType { get; }
        public string DisplayName { get; }
        public int Count { get; private set; }
        public int TrianglesPerInstance { get; }
        public int VerticesPerInstance { get; }
        public long TotalTriangles { get; private set; }
        public long TotalVertices { get; private set; }
        public string LodLabel { get; private set; } = string.Empty;
        public string ShadowLabel { get; private set; } = string.Empty;
        public string Notes { get; private set; } = string.Empty;

        public void Add(long triangles, long vertices, string lodLabel, string shadowLabel, string notes)
        {
            Count++;
            TotalTriangles += triangles;
            TotalVertices += vertices;
            LodLabel = lodLabel;
            ShadowLabel = shadowLabel;
            Notes = notes;
        }

        public void SetCount(int count)
        {
            Count = Mathf.Max(0, count);
        }
    }
}
