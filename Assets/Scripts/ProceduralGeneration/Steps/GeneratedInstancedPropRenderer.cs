using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Renders generated props through explicit GPU instancing instead of thousands of
    /// individual MeshRenderer submissions.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeneratedInstancedPropRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;

        private readonly Dictionary<int, PropRenderPrototype> prototypeCache = new();
        private readonly Dictionary<Material, Material> instancedMaterialCache = new();
        private readonly Dictionary<DrawKey, DrawGroup> drawGroupsByKey = new();
        private readonly List<DrawGroup> drawGroups = new();

        private int instanceCount;

        public int InstanceCount => instanceCount;
        public int DrawGroupCount => drawGroups.Count;

        public int EstimateVisibleBatchCount(Camera camera)
        {
            if (camera == null)
            {
                return EstimateTotalBatchCount();
            }

            var cameraPosition = camera.transform.position;
            var batchCount = 0;
            for (var i = 0; i < drawGroups.Count; i++)
            {
                batchCount += drawGroups[i].EstimateBatchCount(cameraPosition);
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

            for (var i = 0; i < prototype.Elements.Length; i++)
            {
                var element = prototype.Elements[i];
                var group = GetOrCreateGroup(element);
                var matrix = rootMatrix * element.LocalMatrix;
                group.Add(matrix, instanceCenter, drawDistance);
            }

            instanceCount++;
            return true;
        }

        private void OnEnable()
        {
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
                return;
            }

            var cameraPosition = camera.transform.position;
            for (var i = 0; i < drawGroups.Count; i++)
            {
                drawGroups[i].Draw(camera, cameraPosition);
            }
        }

        private DrawGroup GetOrCreateGroup(RenderElement element)
        {
            var key = new DrawKey(
                element.Mesh,
                element.Material,
                element.SubmeshIndex,
                element.Layer,
                element.RenderingLayerMask,
                element.ShadowCastingMode,
                element.ReceiveShadows);

            if (drawGroupsByKey.TryGetValue(key, out var group))
            {
                return group;
            }

            group = new DrawGroup(element);
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
            var selectedRenderers = new List<MeshRenderer>();
            var lodManagedRenderers = new HashSet<Renderer>();

            var lodGroups = prefab.GetComponentsInChildren<LODGroup>(true);
            for (var i = 0; i < lodGroups.Length; i++)
            {
                var lods = lodGroups[i].GetLODs();
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
                        if (lodIndex == 0 &&
                            renderer is MeshRenderer meshRenderer &&
                            !ShouldSkipRenderer(meshRenderer, mode))
                        {
                            selectedRenderers.Add(meshRenderer);
                        }
                    }
                }
            }

            if (mode != PrototypeMode.LodOnly)
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

                    selectedRenderers.Add(renderer);
                }
            }

            var elements = new List<RenderElement>();
            var hasBounds = false;
            var localBounds = default(Bounds);

            for (var i = 0; i < selectedRenderers.Count; i++)
            {
                var renderer = selectedRenderers[i];
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                var localMatrix = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                EncapsulateTransformedBounds(mesh.bounds, localMatrix, ref localBounds, ref hasBounds);

                var materials = renderer.sharedMaterials;
                var submeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
                for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    var material = ResolveInstancedMaterial(materials[submeshIndex]);
                    if (material == null)
                    {
                        continue;
                    }

                    elements.Add(new RenderElement(
                        mesh,
                        material,
                        submeshIndex,
                        localMatrix,
                        renderer.gameObject.layer,
                        renderer.renderingLayerMask,
                        renderer.shadowCastingMode,
                        renderer.receiveShadows));
                }
            }

            return new PropRenderPrototype(
                elements.ToArray(),
                hasBounds ? localBounds : new Bounds(Vector3.zero, Vector3.one));
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

        private sealed class PropRenderPrototype
        {
            public PropRenderPrototype(RenderElement[] elements, Bounds localBounds)
            {
                Elements = elements ?? Array.Empty<RenderElement>();
                LocalBounds = localBounds;
            }

            public RenderElement[] Elements { get; }
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
                int submeshIndex,
                Matrix4x4 localMatrix,
                int layer,
                uint renderingLayerMask,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows)
            {
                Mesh = mesh;
                Material = material;
                SubmeshIndex = submeshIndex;
                LocalMatrix = localMatrix;
                Layer = layer;
                RenderingLayerMask = renderingLayerMask;
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
            }

            public Mesh Mesh { get; }
            public Material Material { get; }
            public int SubmeshIndex { get; }
            public Matrix4x4 LocalMatrix { get; }
            public int Layer { get; }
            public uint RenderingLayerMask { get; }
            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
        }

        private readonly struct DrawKey : IEquatable<DrawKey>
        {
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly int submeshIndex;
            private readonly int layer;
            private readonly uint renderingLayerMask;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;

            public DrawKey(
                Mesh mesh,
                Material material,
                int submeshIndex,
                int layer,
                uint renderingLayerMask,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows)
            {
                this.mesh = mesh;
                this.material = material;
                this.submeshIndex = submeshIndex;
                this.layer = layer;
                this.renderingLayerMask = renderingLayerMask;
                this.shadowCastingMode = shadowCastingMode;
                this.receiveShadows = receiveShadows;
            }

            public bool Equals(DrawKey other)
            {
                return mesh == other.mesh &&
                       material == other.material &&
                       submeshIndex == other.submeshIndex &&
                       layer == other.layer &&
                       renderingLayerMask == other.renderingLayerMask &&
                       shadowCastingMode == other.shadowCastingMode &&
                       receiveShadows == other.receiveShadows;
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
                    hash = (hash * 397) ^ (int)shadowCastingMode;
                    hash = (hash * 397) ^ receiveShadows.GetHashCode();
                    return hash;
                }
            }
        }

        private sealed class DrawGroup
        {
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly int submeshIndex;
            private readonly int layer;
            private readonly uint renderingLayerMask;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;
            private readonly List<Matrix4x4> matrices = new();
            private readonly List<Vector3> centers = new();
            private readonly List<float> maxDrawDistances = new();

            private Matrix4x4[] visibleMatrices = Array.Empty<Matrix4x4>();

            public DrawGroup(RenderElement element)
            {
                mesh = element.Mesh;
                material = element.Material;
                submeshIndex = element.SubmeshIndex;
                layer = element.Layer;
                renderingLayerMask = element.RenderingLayerMask;
                shadowCastingMode = element.ShadowCastingMode;
                receiveShadows = element.ReceiveShadows;
            }

            public int TotalBatchCount => Mathf.CeilToInt(matrices.Count / (float)MaxInstancesPerBatch);

            public void Add(Matrix4x4 matrix, Vector3 center, float maxDrawDistance)
            {
                matrices.Add(matrix);
                centers.Add(center);
                maxDrawDistances.Add(maxDrawDistance);
            }

            public int EstimateBatchCount(Vector3 cameraPosition)
            {
                var visibleCount = 0;
                for (var i = 0; i < matrices.Count; i++)
                {
                    var maxDrawDistance = maxDrawDistances[i];
                    if (maxDrawDistance > 0f)
                    {
                        var maxSqrDistance = maxDrawDistance * maxDrawDistance;
                        if ((centers[i] - cameraPosition).sqrMagnitude > maxSqrDistance)
                        {
                            continue;
                        }
                    }

                    visibleCount++;
                }

                return Mathf.CeilToInt(visibleCount / (float)MaxInstancesPerBatch);
            }

            public void Draw(Camera camera, Vector3 cameraPosition)
            {
                if (mesh == null || material == null || matrices.Count == 0)
                {
                    return;
                }

                EnsureVisibleCapacity(matrices.Count);

                var visibleCount = 0;
                var hasBounds = false;
                var visibleBounds = default(Bounds);
                for (var i = 0; i < matrices.Count; i++)
                {
                    var maxDrawDistance = maxDrawDistances[i];
                    if (maxDrawDistance > 0f)
                    {
                        var maxSqrDistance = maxDrawDistance * maxDrawDistance;
                        if ((centers[i] - cameraPosition).sqrMagnitude > maxSqrDistance)
                        {
                            continue;
                        }
                    }

                    var matrix = matrices[i];
                    visibleMatrices[visibleCount++] = matrix;
                    var instanceBounds = TransformBounds(mesh.bounds, matrix);
                    if (!hasBounds)
                    {
                        visibleBounds = instanceBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        visibleBounds.Encapsulate(instanceBounds);
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

                var renderParams = new RenderParams(material)
                {
                    camera = camera,
                    layer = layer,
                    renderingLayerMask = renderingLayerMask,
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = receiveShadows,
                    worldBounds = visibleBounds
                };

                for (var start = 0; start < visibleCount; start += MaxInstancesPerBatch)
                {
                    var count = Mathf.Min(MaxInstancesPerBatch, visibleCount - start);
                    Graphics.RenderMeshInstanced(renderParams, mesh, submeshIndex, visibleMatrices, count, start);
                }
            }

            private void EnsureVisibleCapacity(int capacity)
            {
                if (visibleMatrices.Length >= capacity)
                {
                    return;
                }

                visibleMatrices = new Matrix4x4[Mathf.NextPowerOfTwo(capacity)];
            }
        }
    }
}
