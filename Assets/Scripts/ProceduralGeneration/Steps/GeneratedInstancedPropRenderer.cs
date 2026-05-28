using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    public enum GeneratedPropVisibilityCategory
    {
        Trees,
        Bushes,
        Rocks,
        Plants,
        Logs,
        Other
    }

    /// <summary>
    /// Renders generated props through explicit GPU instancing instead of thousands of
    /// individual MeshRenderer submissions.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeneratedInstancedPropRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;

        [SerializeField] private ForestQualityLevel qualityPreset = ForestQualityLevel.High;
        [SerializeField] private ForestLodSettings forestLodSettings = ForestLodSettings.CreatePreset(ForestQualityLevel.High);
        [SerializeField] private bool renderTrees = true;
        [SerializeField] private bool renderBushes = true;
        [SerializeField] private bool renderRocks = true;
        [SerializeField] private bool renderPlants = true;
        [SerializeField] private bool renderLogs = true;
        [SerializeField] private bool renderOtherProps = true;

        private readonly Dictionary<int, PropRenderPrototype> prototypeCache = new();
        private readonly Dictionary<Material, Material> instancedMaterialCache = new();
        private readonly Dictionary<DrawKey, DrawGroup> drawGroupsByKey = new();
        private readonly List<DrawGroup> drawGroups = new();
        private readonly Plane[] frustumPlanes = new Plane[6];

        private int instanceCount;

        public int InstanceCount => instanceCount;
        public int DrawGroupCount => drawGroups.Count;
        public ForestQualityLevel QualityPreset => qualityPreset;
        public ForestLodSettings CurrentForestLodSettings => forestLodSettings;

        public void ConfigureForestRendering(ForestRenderingSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            qualityPreset = settings.QualityPreset;
            forestLodSettings = settings.ResolveLodSettings();
        }

        public void ApplyForestQualityPreset(ForestQualityLevel preset)
        {
            qualityPreset = preset;
            forestLodSettings = ForestLodSettings.CreatePreset(preset);
        }

        public void ApplyRuntimeLodSettings(ForestLodSettings settings)
        {
            forestLodSettings = settings != null
                ? settings.Clone()
                : ForestLodSettings.CreatePreset(qualityPreset);
        }

        public bool IsRuntimeCategoryVisible(GeneratedPropVisibilityCategory category)
        {
            return category switch
            {
                GeneratedPropVisibilityCategory.Trees => renderTrees,
                GeneratedPropVisibilityCategory.Bushes => renderBushes,
                GeneratedPropVisibilityCategory.Rocks => renderRocks,
                GeneratedPropVisibilityCategory.Plants => renderPlants,
                GeneratedPropVisibilityCategory.Logs => renderLogs,
                _ => renderOtherProps
            };
        }

        public void SetRuntimeCategoryVisible(GeneratedPropVisibilityCategory category, bool value)
        {
            switch (category)
            {
                case GeneratedPropVisibilityCategory.Trees:
                    renderTrees = value;
                    break;
                case GeneratedPropVisibilityCategory.Bushes:
                    renderBushes = value;
                    break;
                case GeneratedPropVisibilityCategory.Rocks:
                    renderRocks = value;
                    break;
                case GeneratedPropVisibilityCategory.Plants:
                    renderPlants = value;
                    break;
                case GeneratedPropVisibilityCategory.Logs:
                    renderLogs = value;
                    break;
                default:
                    renderOtherProps = value;
                    break;
            }
        }

        public int EstimateVisibleBatchCount(Camera camera)
        {
            if (camera == null)
            {
                return EstimateTotalBatchCount();
            }

            var cameraPosition = camera.transform.position;
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            var batchCount = 0;
            var settings = ResolveForestLodSettings();
            for (var i = 0; i < drawGroups.Count; i++)
            {
                batchCount += drawGroups[i].EstimateBatchCount(
                    cameraPosition,
                    frustumPlanes,
                    settings,
                    IsRuntimeCategoryVisible(drawGroups[i].VisibilityCategory));
            }

            return batchCount;
        }

        public int EstimateTotalBatchCount()
        {
            var batchCount = 0;
            for (var i = 0; i < drawGroups.Count; i++)
            {
                batchCount += drawGroups[i].TotalBatchCount;
            }

            return batchCount;
        }

        public void AddDiagnostics(Camera camera, IList<GeneratedInstancedPropDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            var settings = ResolveForestLodSettings();
            var hasCamera = camera != null;
            var cameraPosition = hasCamera ? camera.transform.position : Vector3.zero;
            var frustumPlanes = hasCamera ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            for (var i = 0; i < drawGroups.Count; i++)
            {
                drawGroups[i].AddDiagnostic(
                    cameraPosition,
                    hasCamera,
                    frustumPlanes,
                    settings,
                    IsRuntimeCategoryVisible(drawGroups[i].VisibilityCategory),
                    diagnostics);
            }
        }

        public bool TryGetPrefabLocalBounds(GameObject prefab, ProceduralPropRole role, out Bounds localBounds)
        {
            var prototype = GetOrCreatePrototype(prefab, ResolvePrototypeMode(role));
            if (prototype == null || !prototype.HasRenderable)
            {
                localBounds = default;
                return false;
            }

            localBounds = prototype.LocalBounds;
            return true;
        }

        public bool RegisterPrefabInstance(
            GameObject prefab,
            ProceduralPropRole role,
            Transform instanceTransform,
            float maxDrawDistance)
        {
            if (prefab == null || instanceTransform == null)
            {
                return false;
            }

            var prototype = GetOrCreatePrototype(prefab, ResolvePrototypeMode(role));
            if (prototype == null || !prototype.HasRenderable)
            {
                return false;
            }

            var rootMatrix = instanceTransform.localToWorldMatrix;
            var instanceCenter = instanceTransform.TransformPoint(prototype.LocalBounds.center);
            var drawDistance = Mathf.Max(0f, maxDrawDistance);
            var sourcePrefabName = prefab.name;

            for (var i = 0; i < prototype.Elements.Length; i++)
            {
                var element = prototype.Elements[i];
                var group = GetOrCreateGroup(element, role);
                var matrix = rootMatrix * element.LocalMatrix;
                group.Add(matrix, instanceCenter, drawDistance, prototype.MaxLodIndex, sourcePrefabName);
            }

            instanceCount++;
            return true;
        }

        private void OnEnable()
        {
            ResolveForestLodSettings();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Camera.onPreCull += OnCameraPreCull;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Camera.onPreCull -= OnCameraPreCull;
        }

        private void OnDestroy()
        {
            foreach (var pair in instancedMaterialCache)
            {
                if (pair.Value == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(pair.Value);
                    continue;
                }
#endif
                Destroy(pair.Value);
            }

            instancedMaterialCache.Clear();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            DrawForCamera(camera);
        }

        private void OnCameraPreCull(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null)
            {
                DrawForCamera(camera);
            }
        }

        private void DrawForCamera(Camera camera)
        {
            if (!isActiveAndEnabled || camera == null || drawGroups.Count == 0)
            {
                if (isActiveAndEnabled && camera != null && drawGroups.Count == 0)
                {
                    RebuildRuntimeGroupsFromMarkers();
                }
            }

            if (!isActiveAndEnabled || camera == null || drawGroups.Count == 0)
            {
                return;
            }

            var settings = ResolveForestLodSettings();
            var cameraPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            for (var i = 0; i < drawGroups.Count; i++)
            {
                drawGroups[i].Draw(
                    camera,
                    cameraPosition,
                    frustumPlanes,
                    settings,
                    IsRuntimeCategoryVisible(drawGroups[i].VisibilityCategory));
            }
        }

        private void RebuildRuntimeGroupsFromMarkers()
        {
            var markers = GetComponentsInChildren<GeneratedInstancedPropInstance>(true);
            if (markers == null || markers.Length == 0)
            {
                return;
            }

            prototypeCache.Clear();
            drawGroupsByKey.Clear();
            drawGroups.Clear();
            instanceCount = 0;

            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.SourcePrefab == null)
                {
                    continue;
                }

                RegisterPrefabInstance(marker.SourcePrefab, marker.Role, marker.transform, marker.MaxDrawDistance);
            }
        }

        private ForestLodSettings ResolveForestLodSettings()
        {
            if (forestLodSettings == null)
            {
                forestLodSettings = ForestLodSettings.CreatePreset(qualityPreset);
            }

            return forestLodSettings;
        }

        private DrawGroup GetOrCreateGroup(RenderElement element, ProceduralPropRole role)
        {
            var visibilityCategory = ResolveVisibilityCategory(role);
            var key = new DrawKey(
                element.Mesh,
                element.Material,
                element.SubmeshIndex,
                element.Layer,
                element.RenderingLayerMask,
                element.SourceShadowCastingMode,
                element.ReceiveShadows,
                element.LodIndex,
                element.UsesDistanceLod,
                element.IsLeafLike,
                visibilityCategory);

            if (drawGroupsByKey.TryGetValue(key, out var group))
            {
                return group;
            }

            group = new DrawGroup(element, visibilityCategory);
            drawGroupsByKey.Add(key, group);
            drawGroups.Add(group);
            return group;
        }

        private static PrototypeMode ResolvePrototypeMode(ProceduralPropRole role)
        {
            if (role == ProceduralPropRole.Tree ||
                role == ProceduralPropRole.ForestCoreTrees ||
                role == ProceduralPropRole.ForestAccentTrees)
            {
                return PrototypeMode.LodOnly;
            }

            if (role == ProceduralPropRole.Rock ||
                role == ProceduralPropRole.RocksSmallMedium ||
                role == ProceduralPropRole.RocksLarge ||
                role == ProceduralPropRole.Cliff)
            {
                return PrototypeMode.RockOnly;
            }

            return PrototypeMode.Full;
        }

        private static GeneratedPropVisibilityCategory ResolveVisibilityCategory(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Tree => GeneratedPropVisibilityCategory.Trees,
                ProceduralPropRole.ForestCoreTrees => GeneratedPropVisibilityCategory.Trees,
                ProceduralPropRole.ForestAccentTrees => GeneratedPropVisibilityCategory.Trees,
                ProceduralPropRole.Bushes => GeneratedPropVisibilityCategory.Bushes,
                ProceduralPropRole.Rock => GeneratedPropVisibilityCategory.Rocks,
                ProceduralPropRole.RocksSmallMedium => GeneratedPropVisibilityCategory.Rocks,
                ProceduralPropRole.RocksLarge => GeneratedPropVisibilityCategory.Rocks,
                ProceduralPropRole.Cliff => GeneratedPropVisibilityCategory.Rocks,
                ProceduralPropRole.GroundGrass => GeneratedPropVisibilityCategory.Plants,
                ProceduralPropRole.GroundPlants => GeneratedPropVisibilityCategory.Plants,
                ProceduralPropRole.ShorePlants => GeneratedPropVisibilityCategory.Plants,
                ProceduralPropRole.GroundCover => GeneratedPropVisibilityCategory.Plants,
                ProceduralPropRole.Log => GeneratedPropVisibilityCategory.Logs,
                _ => GeneratedPropVisibilityCategory.Other
            };
        }

        private PropRenderPrototype GetOrCreatePrototype(GameObject prefab, PrototypeMode mode)
        {
            if (prefab == null)
            {
                return null;
            }

            var key = unchecked(prefab.GetInstanceID() * 397 ^ (int)mode);
            if (prototypeCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var prototype = BuildPrototype(prefab, mode);
            prototypeCache.Add(key, prototype);
            return prototype;
        }

        private PropRenderPrototype BuildPrototype(GameObject prefab, PrototypeMode mode)
        {
            var root = prefab.transform;
            var elements = new List<RenderElement>();
            var lodManagedRenderers = new HashSet<Renderer>();
            var hasBounds = false;
            var localBounds = default(Bounds);
            var maxLodIndex = 0;
            var hasLodElements = false;

            var lodGroups = prefab.GetComponentsInChildren<LODGroup>(true);
            for (var groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
            {
                var lods = lodGroups[groupIndex].GetLODs();
                for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    var renderers = lods[lodIndex].renderers;
                    if (renderers == null)
                    {
                        continue;
                    }

                    for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        var renderer = renderers[rendererIndex];
                        if (renderer == null)
                        {
                            continue;
                        }

                        lodManagedRenderers.Add(renderer);
                        if (mode != PrototypeMode.LodOnly && lodIndex > 0)
                        {
                            continue;
                        }

                        if (renderer is not MeshRenderer meshRenderer ||
                            ShouldSkipRenderer(meshRenderer, mode))
                        {
                            continue;
                        }

                        AddRendererElements(
                            root,
                            meshRenderer,
                            lodIndex,
                            mode == PrototypeMode.LodOnly,
                            elements,
                            ref localBounds,
                            ref hasBounds);

                        maxLodIndex = Mathf.Max(maxLodIndex, lodIndex);
                        hasLodElements = true;
                    }
                }
            }

            if (mode != PrototypeMode.LodOnly || !hasLodElements)
            {
                var meshRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                for (var i = 0; i < meshRenderers.Length; i++)
                {
                    var renderer = meshRenderers[i];
                    if (renderer == null ||
                        lodManagedRenderers.Contains(renderer) ||
                        ShouldSkipRenderer(renderer, mode))
                    {
                        continue;
                    }

                    AddRendererElements(
                        root,
                        renderer,
                        0,
                        false,
                        elements,
                        ref localBounds,
                        ref hasBounds);
                }
            }

            return new PropRenderPrototype(
                elements.ToArray(),
                maxLodIndex,
                hasBounds ? localBounds : new Bounds(Vector3.zero, Vector3.one));
        }

        private void AddRendererElements(
            Transform root,
            MeshRenderer renderer,
            int lodIndex,
            bool usesDistanceLod,
            List<RenderElement> elements,
            ref Bounds localBounds,
            ref bool hasBounds)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                return;
            }

            var localMatrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            EncapsulateTransformedBounds(mesh.bounds, localMatrix, ref localBounds, ref hasBounds);

            var materials = renderer.sharedMaterials;
            var submeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
            for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
            {
                var sourceMaterial = materials[submeshIndex];
                var material = ResolveInstancedMaterial(sourceMaterial);
                if (material == null)
                {
                    continue;
                }

                elements.Add(new RenderElement(
                    mesh,
                    material,
                    sourceMaterial != null ? sourceMaterial.name : material.name,
                    submeshIndex,
                    localMatrix,
                    renderer.gameObject.layer,
                    renderer.renderingLayerMask,
                    renderer.shadowCastingMode,
                    renderer.receiveShadows,
                    lodIndex,
                    usesDistanceLod,
                    IsLeafLike(renderer, mesh, sourceMaterial)));
            }
        }

        private static bool ShouldSkipRenderer(MeshRenderer renderer, PrototypeMode mode)
        {
            if (mode != PrototypeMode.RockOnly || renderer == null)
            {
                return false;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            var meshName = filter != null && filter.sharedMesh != null ? filter.sharedMesh.name : string.Empty;
            var rendererName = renderer.name;
            var materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            var key = $"{rendererName} {meshName} {materialName}".ToLowerInvariant();
            return key.Contains("ivy") ||
                   key.Contains("weed") ||
                   key.Contains("plant") ||
                   key.Contains("grass") ||
                   key.Contains("flower") ||
                   key.Contains("river");
        }

        private static bool IsLeafLike(Renderer renderer, Mesh mesh, Material material)
        {
            var rendererName = renderer != null ? renderer.name : string.Empty;
            var meshName = mesh != null ? mesh.name : string.Empty;
            var materialName = material != null ? material.name : string.Empty;
            var key = $"{rendererName} {meshName} {materialName}".ToLowerInvariant();
            return key.Contains("leaf") ||
                   key.Contains("leav") ||
                   key.Contains("foliage") ||
                   key.Contains("leave") ||
                   key.Contains("billboard");
        }

        private Material ResolveInstancedMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (instancedMaterialCache.TryGetValue(source, out var cached) && cached != null)
            {
                return cached;
            }

            var material = new Material(source)
            {
                name = $"{source.name} (Generated Instanced)",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true
            };

            instancedMaterialCache[source] = material;
            return material;
        }

        private static void EncapsulateTransformedBounds(
            Bounds source,
            Matrix4x4 matrix,
            ref Bounds target,
            ref bool hasBounds)
        {
            var min = source.min;
            var max = source.max;
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, min.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z)), ref target, ref hasBounds);
            Encapsulate(matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, max.z)), ref target, ref hasBounds);
        }

        private static void Encapsulate(Vector3 point, ref Bounds bounds, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = new Bounds(point, Vector3.zero);
                hasBounds = true;
                return;
            }

            bounds.Encapsulate(point);
        }

        public static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
        {
            var hasBounds = false;
            var worldBounds = default(Bounds);
            EncapsulateTransformedBounds(localBounds, matrix, ref worldBounds, ref hasBounds);
            return hasBounds ? worldBounds : new Bounds(matrix.MultiplyPoint3x4(localBounds.center), Vector3.zero);
        }

        public readonly struct GeneratedInstancedPropDiagnostic
        {
            public GeneratedInstancedPropDiagnostic(
                string sourcePrefabs,
                string meshName,
                string materialName,
                int lodIndex,
                bool usesDistanceLod,
                int totalInstances,
                int visibleInstances,
                int trianglesPerInstance,
                int verticesPerInstance,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                GeneratedPropVisibilityCategory visibilityCategory)
            {
                SourcePrefabs = sourcePrefabs;
                MeshName = meshName;
                MaterialName = materialName;
                LodIndex = lodIndex;
                UsesDistanceLod = usesDistanceLod;
                TotalInstances = totalInstances;
                VisibleInstances = visibleInstances;
                TrianglesPerInstance = trianglesPerInstance;
                VerticesPerInstance = verticesPerInstance;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                VisibilityCategory = visibilityCategory;
            }

            public string SourcePrefabs { get; }
            public string MeshName { get; }
            public string MaterialName { get; }
            public int LodIndex { get; }
            public bool UsesDistanceLod { get; }
            public int TotalInstances { get; }
            public int VisibleInstances { get; }
            public int TrianglesPerInstance { get; }
            public int VerticesPerInstance { get; }
            public long TotalTriangles => (long)TotalInstances * TrianglesPerInstance;
            public long VisibleTriangles => (long)VisibleInstances * TrianglesPerInstance;
            public long VisibleVertices => (long)VisibleInstances * VerticesPerInstance;
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public GeneratedPropVisibilityCategory VisibilityCategory { get; }
        }

        private sealed class PropRenderPrototype
        {
            public PropRenderPrototype(RenderElement[] elements, int maxLodIndex, Bounds localBounds)
            {
                Elements = elements ?? Array.Empty<RenderElement>();
                MaxLodIndex = Mathf.Max(0, maxLodIndex);
                LocalBounds = localBounds;
            }

            public RenderElement[] Elements { get; }
            public int MaxLodIndex { get; }
            public Bounds LocalBounds { get; }
            public bool HasRenderable => Elements.Length > 0;
        }

        private enum PrototypeMode
        {
            Full = 0,
            LodOnly = 1,
            RockOnly = 2
        }

        private readonly struct RenderElement
        {
            public RenderElement(
                Mesh mesh,
                Material material,
                string sourceMaterialName,
                int submeshIndex,
                Matrix4x4 localMatrix,
                int layer,
                uint renderingLayerMask,
                ShadowCastingMode sourceShadowCastingMode,
                bool receiveShadows,
                int lodIndex,
                bool usesDistanceLod,
                bool isLeafLike)
            {
                Mesh = mesh;
                Material = material;
                SourceMaterialName = sourceMaterialName;
                SubmeshIndex = submeshIndex;
                LocalMatrix = localMatrix;
                Layer = layer;
                RenderingLayerMask = renderingLayerMask;
                SourceShadowCastingMode = sourceShadowCastingMode;
                ReceiveShadows = receiveShadows;
                LodIndex = Mathf.Max(0, lodIndex);
                UsesDistanceLod = usesDistanceLod;
                IsLeafLike = isLeafLike;
            }

            public Mesh Mesh { get; }
            public Material Material { get; }
            public string SourceMaterialName { get; }
            public int SubmeshIndex { get; }
            public Matrix4x4 LocalMatrix { get; }
            public int Layer { get; }
            public uint RenderingLayerMask { get; }
            public ShadowCastingMode SourceShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public int LodIndex { get; }
            public bool UsesDistanceLod { get; }
            public bool IsLeafLike { get; }
        }

        private readonly struct DrawKey : IEquatable<DrawKey>
        {
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly int submeshIndex;
            private readonly int layer;
            private readonly uint renderingLayerMask;
            private readonly ShadowCastingMode sourceShadowCastingMode;
            private readonly bool receiveShadows;
            private readonly int lodIndex;
            private readonly bool usesDistanceLod;
            private readonly bool isLeafLike;
            private readonly GeneratedPropVisibilityCategory visibilityCategory;

            public DrawKey(
                Mesh mesh,
                Material material,
                int submeshIndex,
                int layer,
                uint renderingLayerMask,
                ShadowCastingMode sourceShadowCastingMode,
                bool receiveShadows,
                int lodIndex,
                bool usesDistanceLod,
                bool isLeafLike,
                GeneratedPropVisibilityCategory visibilityCategory)
            {
                this.mesh = mesh;
                this.material = material;
                this.submeshIndex = submeshIndex;
                this.layer = layer;
                this.renderingLayerMask = renderingLayerMask;
                this.sourceShadowCastingMode = sourceShadowCastingMode;
                this.receiveShadows = receiveShadows;
                this.lodIndex = lodIndex;
                this.usesDistanceLod = usesDistanceLod;
                this.isLeafLike = isLeafLike;
                this.visibilityCategory = visibilityCategory;
            }

            public bool Equals(DrawKey other)
            {
                return mesh == other.mesh &&
                       material == other.material &&
                       submeshIndex == other.submeshIndex &&
                       layer == other.layer &&
                       renderingLayerMask == other.renderingLayerMask &&
                       sourceShadowCastingMode == other.sourceShadowCastingMode &&
                       receiveShadows == other.receiveShadows &&
                       lodIndex == other.lodIndex &&
                       usesDistanceLod == other.usesDistanceLod &&
                       isLeafLike == other.isLeafLike &&
                       visibilityCategory == other.visibilityCategory;
            }

            public override bool Equals(object obj)
            {
                return obj is DrawKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = mesh != null ? mesh.GetInstanceID() : 0;
                    hash = (hash * 397) ^ (material != null ? material.GetInstanceID() : 0);
                    hash = (hash * 397) ^ submeshIndex;
                    hash = (hash * 397) ^ layer;
                    hash = (hash * 397) ^ (int)renderingLayerMask;
                    hash = (hash * 397) ^ (int)sourceShadowCastingMode;
                    hash = (hash * 397) ^ receiveShadows.GetHashCode();
                    hash = (hash * 397) ^ lodIndex;
                    hash = (hash * 397) ^ usesDistanceLod.GetHashCode();
                    hash = (hash * 397) ^ isLeafLike.GetHashCode();
                    hash = (hash * 397) ^ (int)visibilityCategory;
                    return hash;
                }
            }
        }

        private sealed class DrawGroup
        {
            private const float SpatialBucketSize = 64f;

            private readonly Mesh mesh;
            private readonly Material material;
            private readonly string sourceMaterialName;
            private readonly int submeshIndex;
            private readonly int layer;
            private readonly uint renderingLayerMask;
            private readonly ShadowCastingMode sourceShadowCastingMode;
            private readonly bool receiveShadows;
            private readonly int lodIndex;
            private readonly bool usesDistanceLod;
            private readonly bool isLeafLike;
            private readonly GeneratedPropVisibilityCategory visibilityCategory;
            private readonly int trianglesPerInstance;
            private readonly int verticesPerInstance;
            private readonly List<Matrix4x4> matrices = new();
            private readonly List<Vector3> centers = new();
            private readonly List<float> maxDrawDistances = new();
            private readonly List<int> maxLodIndices = new();
            private readonly Dictionary<Vector2Int, DrawBucket> bucketsByCoord = new();
            private readonly List<DrawBucket> buckets = new();
            private readonly HashSet<string> sourcePrefabs = new(StringComparer.Ordinal);

            private Matrix4x4[] visibleMatrices = Array.Empty<Matrix4x4>();
            private Matrix4x4[] sourceShadowMatrices = Array.Empty<Matrix4x4>();
            private Matrix4x4[] noShadowMatrices = Array.Empty<Matrix4x4>();

            public DrawGroup(RenderElement element, GeneratedPropVisibilityCategory category)
            {
                mesh = element.Mesh;
                material = element.Material;
                sourceMaterialName = element.SourceMaterialName;
                submeshIndex = element.SubmeshIndex;
                layer = element.Layer;
                renderingLayerMask = element.RenderingLayerMask;
                sourceShadowCastingMode = element.SourceShadowCastingMode;
                receiveShadows = element.ReceiveShadows;
                lodIndex = element.LodIndex;
                usesDistanceLod = element.UsesDistanceLod;
                isLeafLike = element.IsLeafLike;
                visibilityCategory = category;
                trianglesPerInstance = CountSubmeshTriangles(mesh, submeshIndex);
                verticesPerInstance = mesh != null ? mesh.vertexCount : 0;
            }

            public GeneratedPropVisibilityCategory VisibilityCategory => visibilityCategory;
            public int TotalBatchCount => Mathf.CeilToInt(matrices.Count / (float)MaxInstancesPerBatch);

            public void Add(
                Matrix4x4 matrix,
                Vector3 center,
                float maxDrawDistance,
                int maxLodIndex,
                string sourcePrefabName)
            {
                var index = matrices.Count;
                matrices.Add(matrix);
                centers.Add(center);
                maxDrawDistances.Add(maxDrawDistance);
                maxLodIndices.Add(Mathf.Max(0, maxLodIndex));
                AddToSpatialBucket(index, center, matrix, maxDrawDistance);
                if (!string.IsNullOrWhiteSpace(sourcePrefabName))
                {
                    sourcePrefabs.Add(sourcePrefabName);
                }
            }

            public int EstimateBatchCount(
                Vector3 cameraPosition,
                Plane[] frustumPlanes,
                ForestLodSettings settings,
                bool categoryVisible)
            {
                if (!categoryVisible)
                {
                    return 0;
                }

                var visibleCount = CountVisible(cameraPosition, true, frustumPlanes, settings);
                return Mathf.CeilToInt(visibleCount / (float)MaxInstancesPerBatch);
            }

            public void AddDiagnostic(
                Vector3 cameraPosition,
                bool hasCamera,
                Plane[] frustumPlanes,
                ForestLodSettings settings,
                bool categoryVisible,
                IList<GeneratedInstancedPropDiagnostic> diagnostics)
            {
                var visibleCount = categoryVisible ? CountVisible(cameraPosition, hasCamera, frustumPlanes, settings) : 0;
                diagnostics.Add(new GeneratedInstancedPropDiagnostic(
                    SourcePrefabsText,
                    mesh != null ? mesh.name : string.Empty,
                    sourceMaterialName,
                    lodIndex,
                    usesDistanceLod,
                    matrices.Count,
                    visibleCount,
                    trianglesPerInstance,
                    verticesPerInstance,
                    ResolveShadowCastingMode(settings),
                    receiveShadows,
                    visibilityCategory));
            }

            public void Draw(
                Camera camera,
                Vector3 cameraPosition,
                Plane[] frustumPlanes,
                ForestLodSettings settings,
                bool categoryVisible)
            {
                if (!categoryVisible || mesh == null || material == null || matrices.Count == 0)
                {
                    return;
                }

                EnsureVisibleCapacity(matrices.Count);

                var visibleCount = 0;
                var sourceShadowCount = 0;
                var noShadowCount = 0;
                var hasBounds = false;
                var hasSourceShadowBounds = false;
                var hasNoShadowBounds = false;
                var visibleBounds = default(Bounds);
                var sourceShadowBounds = default(Bounds);
                var noShadowBounds = default(Bounds);
                var splitShadowsByDistance = UsesShadowDistanceSplit(settings);
                for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
                {
                    var bucket = buckets[bucketIndex];
                    if (!CanBucketContainVisibleInstances(bucket, cameraPosition, frustumPlanes, settings))
                    {
                        continue;
                    }

                    for (var indexIndex = 0; indexIndex < bucket.Indices.Count; indexIndex++)
                    {
                        var i = bucket.Indices[indexIndex];
                        var distance = Vector3.Distance(centers[i], cameraPosition);
                        var activeLod = ResolveActiveLod(i, cameraPosition, true, settings, distance);
                        var matrix = matrices[i];

                        if (activeLod == lodIndex)
                        {
                            var instanceBounds = TransformBounds(mesh.bounds, matrix);
                            if (!IsInsideFrustum(instanceBounds, frustumPlanes))
                            {
                                continue;
                            }

                            visibleMatrices[visibleCount++] = matrix;
                            Encapsulate(instanceBounds, ref visibleBounds, ref hasBounds);

                            if (splitShadowsByDistance)
                            {
                                if (ShouldUseSourceShadow(activeLod, distance, settings))
                                {
                                    sourceShadowMatrices[sourceShadowCount++] = matrix;
                                    Encapsulate(instanceBounds, ref sourceShadowBounds, ref hasSourceShadowBounds);
                                }
                                else
                                {
                                    noShadowMatrices[noShadowCount++] = matrix;
                                    Encapsulate(instanceBounds, ref noShadowBounds, ref hasNoShadowBounds);
                                }
                            }
                        }
                    }
                }

                if (visibleCount == 0)
                {
                    return;
                }

                if (!hasBounds)
                {
                    visibleBounds = new Bounds(cameraPosition, Vector3.one);
                }

                if (splitShadowsByDistance)
                {
                    if (sourceShadowCount > 0)
                    {
                        DrawMatrices(
                            camera,
                            sourceShadowMatrices,
                            sourceShadowCount,
                            hasSourceShadowBounds ? sourceShadowBounds : visibleBounds,
                            sourceShadowCastingMode,
                            receiveShadows);
                    }

                    if (noShadowCount > 0)
                    {
                        DrawMatrices(
                            camera,
                            noShadowMatrices,
                            noShadowCount,
                            hasNoShadowBounds ? noShadowBounds : visibleBounds,
                            ShadowCastingMode.Off,
                            receiveShadows);
                    }

                    return;
                }

                DrawMatrices(
                    camera,
                    visibleMatrices,
                    visibleCount,
                    visibleBounds,
                    ResolveShadowCastingMode(settings),
                    receiveShadows);
            }

            private void DrawMatrices(
                Camera camera,
                Matrix4x4[] source,
                int visibleCount,
                Bounds bounds,
                ShadowCastingMode shadowCastingMode,
                bool shouldReceiveShadows)
            {
                if (visibleCount <= 0)
                {
                    return;
                }

                var renderParams = new RenderParams(material)
                {
                    camera = camera,
                    layer = layer,
                    renderingLayerMask = renderingLayerMask,
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = shouldReceiveShadows,
                    worldBounds = bounds
                };

                for (var start = 0; start < visibleCount; start += MaxInstancesPerBatch)
                {
                    var count = Mathf.Min(MaxInstancesPerBatch, visibleCount - start);
                    Graphics.RenderMeshInstanced(renderParams, mesh, submeshIndex, source, count, start);
                }
            }

            private int CountVisible(
                Vector3 cameraPosition,
                bool hasCamera,
                Plane[] frustumPlanes,
                ForestLodSettings settings)
            {
                if (!hasCamera)
                {
                    return matrices.Count;
                }

                var visibleCount = 0;
                for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
                {
                    var bucket = buckets[bucketIndex];
                    if (!CanBucketContainVisibleInstances(bucket, cameraPosition, frustumPlanes, settings))
                    {
                        continue;
                    }

                    for (var indexIndex = 0; indexIndex < bucket.Indices.Count; indexIndex++)
                    {
                        var i = bucket.Indices[indexIndex];
                        if (ResolveActiveLod(i, cameraPosition, true, settings) != lodIndex)
                        {
                            continue;
                        }

                        if (!IsInsideFrustum(TransformBounds(mesh.bounds, matrices[i]), frustumPlanes))
                        {
                            continue;
                        }

                        visibleCount++;
                    }
                }

                return visibleCount;
            }

            private void AddToSpatialBucket(int index, Vector3 center, Matrix4x4 matrix, float maxDrawDistance)
            {
                var coord = ResolveBucketCoord(center);
                if (!bucketsByCoord.TryGetValue(coord, out var bucket))
                {
                    bucket = new DrawBucket();
                    bucketsByCoord.Add(coord, bucket);
                    buckets.Add(bucket);
                }

                bucket.Add(index, TransformBounds(mesh.bounds, matrix), maxDrawDistance);
            }

            private bool CanBucketContainVisibleInstances(
                DrawBucket bucket,
                Vector3 cameraPosition,
                Plane[] frustumPlanes,
                ForestLodSettings settings)
            {
                if (bucket == null || bucket.Indices.Count == 0)
                {
                    return false;
                }

                if (!IsInsideFrustum(bucket.Bounds, frustumPlanes))
                {
                    return false;
                }

                var cullDistance = ResolveBucketCullDistance(bucket, settings);
                return cullDistance <= 0f || SqrDistanceToBounds(bucket.Bounds, cameraPosition) <= cullDistance * cullDistance;
            }

            private float ResolveBucketCullDistance(DrawBucket bucket, ForestLodSettings settings)
            {
                if (!usesDistanceLod)
                {
                    return bucket.HasInfiniteDrawDistance ? 0f : bucket.MaxDrawDistance;
                }

                settings ??= ForestLodSettings.CreatePreset(ForestQualityLevel.High);
                var globalCullDistance = settings.CullDistance > 0f ? settings.CullDistance : settings.Lod2Distance;
                if (bucket.HasInfiniteDrawDistance || bucket.MaxDrawDistance <= 0f)
                {
                    return globalCullDistance;
                }

                return globalCullDistance > 0f
                    ? Mathf.Min(globalCullDistance, bucket.MaxDrawDistance)
                    : bucket.MaxDrawDistance;
            }

            private static Vector2Int ResolveBucketCoord(Vector3 center)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(center.x / SpatialBucketSize),
                    Mathf.FloorToInt(center.z / SpatialBucketSize));
            }

            private static float SqrDistanceToBounds(Bounds bounds, Vector3 point)
            {
                var closestX = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
                var closestY = Mathf.Clamp(point.y, bounds.min.y, bounds.max.y);
                var closestZ = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);
                var dx = point.x - closestX;
                var dy = point.y - closestY;
                var dz = point.z - closestZ;
                return dx * dx + dy * dy + dz * dz;
            }

            private static bool IsInsideFrustum(Bounds bounds, Plane[] frustumPlanes)
            {
                if (frustumPlanes == null || frustumPlanes.Length == 0)
                {
                    return true;
                }

                return GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
            }

            private int ResolveActiveLod(int index, Vector3 cameraPosition, bool hasCamera, ForestLodSettings settings)
            {
                var distance = hasCamera ? Vector3.Distance(centers[index], cameraPosition) : 0f;
                return ResolveActiveLod(index, cameraPosition, hasCamera, settings, distance);
            }

            private int ResolveActiveLod(int index, Vector3 cameraPosition, bool hasCamera, ForestLodSettings settings, float distance)
            {
                if (!hasCamera)
                {
                    return lodIndex;
                }

                var maxDrawDistance = maxDrawDistances[index];
                if (!usesDistanceLod)
                {
                    return maxDrawDistance <= 0f || distance <= maxDrawDistance ? 0 : -1;
                }

                settings ??= ForestLodSettings.CreatePreset(ForestQualityLevel.High);
                return settings.ResolveLodIndex(distance, maxDrawDistance, maxLodIndices[index]);
            }

            private ShadowCastingMode ResolveShadowCastingMode(ForestLodSettings settings)
            {
                if (!usesDistanceLod || sourceShadowCastingMode == ShadowCastingMode.Off)
                {
                    return sourceShadowCastingMode;
                }

                settings ??= ForestLodSettings.CreatePreset(ForestQualityLevel.High);
                if (!IsLodAllowedToCastShadows(settings))
                {
                    return ShadowCastingMode.Off;
                }

                if (settings.ShadowDistance <= 0f)
                {
                    return ShadowCastingMode.Off;
                }

                return sourceShadowCastingMode;
            }

            private bool UsesShadowDistanceSplit(ForestLodSettings settings)
            {
                return sourceShadowCastingMode != ShadowCastingMode.Off &&
                       settings != null &&
                       settings.ShadowDistance >= 0f;
            }

            private bool ShouldUseSourceShadow(int activeLod, float distance, ForestLodSettings settings)
            {
                if (activeLod != lodIndex || settings == null)
                {
                    return false;
                }

                if (!IsLodAllowedToCastShadows(settings))
                {
                    return false;
                }

                return distance <= settings.ShadowDistance;
            }

            private bool IsLodAllowedToCastShadows(ForestLodSettings settings)
            {
                if (settings == null)
                {
                    return false;
                }

                if (settings.DisableAllShadowsAfterLod1 && lodIndex > 1)
                {
                    return false;
                }

                if (settings.DisableLeafShadowsAfterLod0 && lodIndex > 0 && isLeafLike)
                {
                    return false;
                }

                return true;
            }

            private string SourcePrefabsText
            {
                get
                {
                    if (sourcePrefabs.Count == 0)
                    {
                        return string.Empty;
                    }

                    return string.Join(", ", sourcePrefabs);
                }
            }

            private static int CountSubmeshTriangles(Mesh sourceMesh, int sourceSubmesh)
            {
                if (sourceMesh == null || sourceSubmesh < 0 || sourceSubmesh >= sourceMesh.subMeshCount)
                {
                    return 0;
                }

                return (int)(sourceMesh.GetIndexCount(sourceSubmesh) / 3);
            }

            private void EnsureVisibleCapacity(int capacity)
            {
                if (visibleMatrices.Length >= capacity)
                {
                    return;
                }

                var arraySize = Mathf.NextPowerOfTwo(capacity);
                visibleMatrices = new Matrix4x4[arraySize];
                sourceShadowMatrices = new Matrix4x4[arraySize];
                noShadowMatrices = new Matrix4x4[arraySize];
            }

            private static void Encapsulate(Bounds source, ref Bounds target, ref bool hasBounds)
            {
                if (!hasBounds)
                {
                    target = source;
                    hasBounds = true;
                    return;
                }

                target.Encapsulate(source);
            }

            private sealed class DrawBucket
            {
                public readonly List<int> Indices = new();

                private bool hasBounds;

                public Bounds Bounds { get; private set; }
                public float MaxDrawDistance { get; private set; }
                public bool HasInfiniteDrawDistance { get; private set; }

                public void Add(int index, Bounds bounds, float maxDrawDistance)
                {
                    Indices.Add(index);
                    if (!hasBounds)
                    {
                        Bounds = bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        var merged = Bounds;
                        merged.Encapsulate(bounds);
                        Bounds = merged;
                    }

                    if (maxDrawDistance <= 0f)
                    {
                        HasInfiniteDrawDistance = true;
                    }
                    else
                    {
                        MaxDrawDistance = Mathf.Max(MaxDrawDistance, maxDrawDistance);
                    }
                }
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class GeneratedInstancedPropInstance : MonoBehaviour
    {
        [SerializeField] private GameObject sourcePrefab;
        [SerializeField] private ProceduralPropRole role;
        [SerializeField] private float maxDrawDistance;

        public GameObject SourcePrefab => sourcePrefab;
        public ProceduralPropRole Role => role;
        public float MaxDrawDistance => maxDrawDistance;

        public void Initialize(GameObject prefab, ProceduralPropRole propRole, float drawDistance)
        {
            sourcePrefab = prefab;
            role = propRole;
            maxDrawDistance = Mathf.Max(0f, drawDistance);
        }
    }
}
