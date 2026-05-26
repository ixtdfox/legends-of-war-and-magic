using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeneratedGpuGrassRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;
        private const int PaletteCount = 4;
        private const string GrassPrefabResourcePath = "Prefabs/Grass/Fristy_Grass_02_Ver_00";
        private const string GrassSourceMaterialPath = "Materials/Plants/Fristy_Plant_Stylized_Art_03";
        private const int HighDetailPrefabRendererCount = 9;
        private const int LowDetailPrefabRendererCount = 1;

        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private GpuGrassSettings grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
        [SerializeField] private int seed;
        [SerializeField] private float waterLevel;

        private readonly List<GrassChunk> chunks = new();
        private readonly List<VisibleChunk> visibleChunks = new();
        private readonly List<Matrix4x4>[] visibleHighMatrices = CreateMatrixBuckets();
        private readonly List<Matrix4x4>[] visibleLowMatrices = CreateMatrixBuckets();

        private Mesh highDetailMesh;
        private Mesh lowDetailMesh;
        private Material[] paletteMaterials;
        private Matrix4x4[] batchMatrices = Array.Empty<Matrix4x4>();
        private float[,,] alphamaps;
        private int alphamapWidth;
        private int alphamapHeight;
        private int alphamapLayers;
        private Bounds terrainBounds;
        private bool initialized;

        public int ChunkCount => chunks.Count;
        public int GeneratedChunkCount { get; private set; }
        public int LastVisibleClumps { get; private set; }
        public int LastBatchCount { get; private set; }
        public int GeneratedClumpCount { get; private set; }

        public void Initialize(Terrain terrain, int generationSeed, GpuGrassSettings settings, float terrainWaterLevel)
        {
            targetTerrain = terrain;
            seed = generationSeed;
            grassSettings = settings != null ? settings.Clone() : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            waterLevel = terrainWaterLevel;

            ReleaseRuntimeResources();
            CreateRuntimeResources();
            CacheTerrainData();
            BuildChunkIndex();
            initialized = targetTerrain != null && targetTerrain.terrainData != null && chunks.Count > 0;
        }

        public void AddDiagnostics(Camera camera, IList<GpuGrassDiagnostic> diagnostics)
        {
            if (diagnostics == null || !EnsureInitialized())
            {
                return;
            }

            PrepareVisibleMatrices(camera);
            diagnostics.Add(new GpuGrassDiagnostic(
                "GPU Grass High",
                CountBuckets(visibleHighMatrices),
                CountTriangles(highDetailMesh),
                CountVertices(highDetailMesh),
                LastBatchCount));
            diagnostics.Add(new GpuGrassDiagnostic(
                "GPU Grass Low",
                CountBuckets(visibleLowMatrices),
                CountTriangles(lowDetailMesh),
                CountVertices(lowDetailMesh),
                LastBatchCount));
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
            ReleaseRuntimeResources();
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
            if (camera == null || !isActiveAndEnabled || !EnsureInitialized())
            {
                return;
            }

            PrepareVisibleMatrices(camera);
            DrawBuckets(camera, highDetailMesh, visibleHighMatrices);
            DrawBuckets(camera, lowDetailMesh, visibleLowMatrices);
        }

        private bool EnsureInitialized()
        {
            if (initialized)
            {
                return true;
            }

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return false;
            }

            if (grassSettings == null)
            {
                grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            }

            CreateRuntimeResources();
            CacheTerrainData();
            BuildChunkIndex();
            initialized = chunks.Count > 0;
            return initialized;
        }

        private void PrepareVisibleMatrices(Camera camera)
        {
            ClearBuckets(visibleHighMatrices);
            ClearBuckets(visibleLowMatrices);
            visibleChunks.Clear();
            LastVisibleClumps = 0;
            LastBatchCount = 0;

            if (camera == null)
            {
                return;
            }

            var settings = ResolveSettings();
            if (!settings.Enabled || settings.DrawDistance <= 0f || settings.MaxVisibleClumps <= 0)
            {
                return;
            }

            var cameraPosition = camera.transform.position;
            var maxDistance = settings.DrawDistance + settings.ChunkSize;
            var maxSqrDistance = maxDistance * maxDistance;
            var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var sqrDistance = (chunk.Center - cameraPosition).sqrMagnitude;
                if (sqrDistance > maxSqrDistance || !GeometryUtility.TestPlanesAABB(frustumPlanes, chunk.Bounds))
                {
                    continue;
                }

                visibleChunks.Add(new VisibleChunk(chunk, sqrDistance));
            }

            visibleChunks.Sort((left, right) => left.SqrDistance.CompareTo(right.SqrDistance));

            for (var i = 0; i < visibleChunks.Count && LastVisibleClumps < settings.MaxVisibleClumps; i++)
            {
                var chunk = visibleChunks[i].Chunk;
                if (!chunk.Generated)
                {
                    GenerateChunk(chunk);
                }

                var distance = Mathf.Sqrt(visibleChunks[i].SqrDistance);
                var targetBuckets = distance <= settings.HighDetailDistance ? visibleHighMatrices : visibleLowMatrices;
                AddChunkMatrices(chunk, targetBuckets, settings.MaxVisibleClumps - LastVisibleClumps);
            }

            LastBatchCount = EstimateBatchCount(visibleHighMatrices) + EstimateBatchCount(visibleLowMatrices);
        }

        private void AddChunkMatrices(GrassChunk chunk, List<Matrix4x4>[] targetBuckets, int remainingBudget)
        {
            for (var paletteIndex = 0; paletteIndex < PaletteCount && remainingBudget > 0; paletteIndex++)
            {
                var source = chunk.MatricesByPalette[paletteIndex];
                var count = Mathf.Min(source.Count, remainingBudget);
                for (var i = 0; i < count; i++)
                {
                    targetBuckets[paletteIndex].Add(source[i]);
                }

                remainingBudget -= count;
                LastVisibleClumps += count;
            }
        }

        private void DrawBuckets(Camera camera, Mesh mesh, List<Matrix4x4>[] buckets)
        {
            if (mesh == null || paletteMaterials == null)
            {
                return;
            }

            var settings = ResolveSettings();
            for (var paletteIndex = 0; paletteIndex < PaletteCount; paletteIndex++)
            {
                var matrices = buckets[paletteIndex];
                if (matrices.Count == 0)
                {
                    continue;
                }

                var material = paletteMaterials[Mathf.Min(paletteIndex, paletteMaterials.Length - 1)];
                if (material == null)
                {
                    continue;
                }

                var renderParams = new RenderParams(material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    renderingLayerMask = 1u,
                    receiveShadows = settings.ReceiveShadows,
                    shadowCastingMode = ShadowCastingMode.Off,
                    worldBounds = terrainBounds
                };

                for (var start = 0; start < matrices.Count; start += MaxInstancesPerBatch)
                {
                    var count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
                    EnsureBatchCapacity(count);
                    matrices.CopyTo(start, batchMatrices, 0, count);
                    Graphics.RenderMeshInstanced(renderParams, mesh, 0, batchMatrices, count);
                }
            }
        }

        private void CacheTerrainData()
        {
            alphamaps = null;
            alphamapWidth = 0;
            alphamapHeight = 0;
            alphamapLayers = 0;

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return;
            }

            var data = targetTerrain.terrainData;
            terrainBounds = new Bounds(
                targetTerrain.transform.position + data.size * 0.5f,
                data.size);

            if (data.alphamapLayers <= 0 || data.alphamapWidth <= 0 || data.alphamapHeight <= 0)
            {
                return;
            }

            alphamapWidth = data.alphamapWidth;
            alphamapHeight = data.alphamapHeight;
            alphamapLayers = data.alphamapLayers;
            alphamaps = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        }

        private void BuildChunkIndex()
        {
            chunks.Clear();
            GeneratedChunkCount = 0;
            GeneratedClumpCount = 0;

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return;
            }

            var settings = ResolveSettings();
            var terrainPosition = targetTerrain.transform.position;
            var size = targetTerrain.terrainData.size;
            var chunkSize = settings.ChunkSize;
            var chunkCountX = Mathf.CeilToInt(size.x / chunkSize);
            var chunkCountZ = Mathf.CeilToInt(size.z / chunkSize);

            for (var z = 0; z < chunkCountZ; z++)
            {
                for (var x = 0; x < chunkCountX; x++)
                {
                    var minX = terrainPosition.x + x * chunkSize;
                    var minZ = terrainPosition.z + z * chunkSize;
                    var maxX = Mathf.Min(terrainPosition.x + size.x, minX + chunkSize);
                    var maxZ = Mathf.Min(terrainPosition.z + size.z, minZ + chunkSize);
                    var center = new Vector3((minX + maxX) * 0.5f, terrainPosition.y, (minZ + maxZ) * 0.5f);
                    var bounds = new Bounds(
                        new Vector3(center.x, terrainPosition.y + size.y * 0.5f, center.z),
                        new Vector3(Mathf.Max(0.1f, maxX - minX), size.y + 2f, Mathf.Max(0.1f, maxZ - minZ)));
                    chunks.Add(new GrassChunk(x, z, minX, minZ, maxX, maxZ, center, bounds));
                }
            }
        }

        private void GenerateChunk(GrassChunk chunk)
        {
            chunk.EnsureBuckets();
            var settings = ResolveSettings();
            var spacing = settings.PlacementSpacing / Mathf.Sqrt(Mathf.Max(0.05f, settings.DensityScale));
            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = targetTerrain.terrainData.size;
            var cellsX = Mathf.Max(1, Mathf.CeilToInt((chunk.MaxX - chunk.MinX) / spacing));
            var cellsZ = Mathf.Max(1, Mathf.CeilToInt((chunk.MaxZ - chunk.MinZ) / spacing));

            for (var z = 0; z < cellsZ; z++)
            {
                for (var x = 0; x < cellsX; x++)
                {
                    var hash = Hash01(seed, chunk.IndexX * 73856093 + x, chunk.IndexZ * 19349663 + z, 0);
                    var jitterX = Hash01(seed, x, z, 11);
                    var jitterZ = Hash01(seed, x, z, 23);
                    var worldX = chunk.MinX + (x + jitterX) * spacing;
                    var worldZ = chunk.MinZ + (z + jitterZ) * spacing;
                    if (worldX >= chunk.MaxX || worldZ >= chunk.MaxZ)
                    {
                        continue;
                    }

                    var nx = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, worldX);
                    var nz = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, worldZ);
                    var mask = SampleGrassMask(nx, nz);
                    var spawnProbability = Mathf.Lerp(0.78f, 1f, Mathf.Sqrt(mask));
                    if (mask <= 0.025f || hash > spawnProbability)
                    {
                        continue;
                    }

                    var height = targetTerrain.terrainData.GetInterpolatedHeight(nx, nz) + terrainPosition.y;
                    if (height <= waterLevel + 0.08f)
                    {
                        continue;
                    }

                    var yaw = Hash01(seed, x, z, 37) * 360f;
                    var widthScale = Mathf.Lerp(0.84f, 1.34f, Hash01(seed, x, z, 41));
                    var heightScale = Mathf.Lerp(0.92f, 1.42f, Hash01(seed, x, z, 43)) * Mathf.Lerp(0.92f, 1.18f, mask);
                    var position = new Vector3(worldX, height + 0.02f, worldZ);
                    var matrix = Matrix4x4.TRS(
                        position,
                        Quaternion.Euler(0f, yaw, 0f),
                        new Vector3(widthScale, heightScale, widthScale));
                    var paletteIndex = Mathf.Clamp(Mathf.FloorToInt(Hash01(seed, x, z, 47) * PaletteCount), 0, PaletteCount - 1);
                    chunk.MatricesByPalette[paletteIndex].Add(matrix);
                    chunk.ClumpCount++;
                }
            }

            chunk.Generated = true;
            GeneratedChunkCount++;
            GeneratedClumpCount += chunk.ClumpCount;
        }

        private float SampleGrassMask(float nx, float nz)
        {
            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return 0f;
            }

            var data = targetTerrain.terrainData;
            var normalizedHeight = data.GetInterpolatedHeight(nx, nz) / Mathf.Max(1f, data.size.y);
            var slope = Vector3.Angle(data.GetInterpolatedNormal(nx, nz), Vector3.up);
            var aboveWater = normalizedHeight - Mathf.Clamp01(waterLevel / Mathf.Max(1f, data.size.y));
            var slopeMask = 1f - SmoothRange(20f, 44f, slope);
            var waterMask = SmoothRange(0.018f, 0.08f, aboveWater);
            var highMask = 1f - SmoothRange(0.78f, 0.96f, normalizedHeight);
            var layerMask = SampleGrassLayer(nx, nz);
            var patchNoise = Mathf.PerlinNoise(nx * 45f + seed * 0.00019f, nz * 45f + seed * 0.00023f);
            var meadowNoise = Mathf.PerlinNoise(nx * 13f + seed * 0.00031f, nz * 13f + seed * 0.00029f);
            var patch = Mathf.Lerp(0.58f, 1.25f, patchNoise) * Mathf.Lerp(0.72f, 1.16f, meadowNoise);
            return Mathf.Clamp01(layerMask * slopeMask * waterMask * highMask * patch);
        }

        private float SampleGrassLayer(float nx, float nz)
        {
            if (alphamaps == null || alphamapLayers == 0)
            {
                return 1f;
            }

            var x = Mathf.Clamp(Mathf.RoundToInt(nx * (alphamapWidth - 1)), 0, alphamapWidth - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(nz * (alphamapHeight - 1)), 0, alphamapHeight - 1);
            var grass = alphamapLayers > 1 ? alphamaps[z, x, 1] : 1f;
            var variation = alphamapLayers > 2 ? alphamaps[z, x, 2] : 0f;
            var shore = alphamapLayers > 0 ? alphamaps[z, x, 0] : 0f;
            var rock = alphamapLayers > 3 ? alphamaps[z, x, 3] : 0f;
            return Mathf.Clamp01((grass + variation * 0.85f) * (1f - shore * 0.7f) * (1f - rock * 0.9f));
        }

        private void CreateRuntimeResources()
        {
            if (highDetailMesh == null)
            {
                highDetailMesh = CreateGrassMeshFromPrefab(
                    "Fristy_Grass_02_Ver_00 GPU High",
                    HighDetailPrefabRendererCount) ?? CreateGrassMesh("Generated GPU Grass High", 7);
            }

            if (lowDetailMesh == null)
            {
                lowDetailMesh = CreateGrassMeshFromPrefab(
                    "Fristy_Grass_02_Ver_00 GPU Low",
                    LowDetailPrefabRendererCount) ?? CreateGrassMesh("Generated GPU Grass Low", 3);
            }

            if (paletteMaterials == null || paletteMaterials.Length != PaletteCount)
            {
                paletteMaterials = CreatePaletteMaterials();
            }

            ApplyWindSettings();
        }

        private static Mesh CreateGrassMeshFromPrefab(string meshName, int maxRendererCount)
        {
            var prefab = Resources.Load<GameObject>(GrassPrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"GPU grass prefab '{GrassPrefabResourcePath}' was not found. Falling back to generated blades.");
                return null;
            }

            var root = prefab.transform;
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                Debug.LogWarning($"GPU grass prefab '{prefab.name}' has no MeshRenderer children. Falling back to generated blades.");
                return null;
            }

            Array.Sort(renderers, CompareGrassRenderers);
            var combineCount = Mathf.Clamp(maxRendererCount, 1, renderers.Length);
            var combines = new List<CombineInstance>(combineCount);
            for (var i = 0; i < renderers.Length && combines.Count < combineCount; i++)
            {
                var renderer = renderers[i];
                var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null)
                {
                    continue;
                }

                combines.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = 0,
                    transform = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix
                });
            }

            if (combines.Count == 0)
            {
                return null;
            }

            var combinedMesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave,
                indexFormat = IndexFormat.UInt32
            };
            combinedMesh.CombineMeshes(combines.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            NormalizePivotToGround(combinedMesh);
            return combinedMesh;
        }

        private static void NormalizePivotToGround(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            var bounds = mesh.bounds;
            if (bounds.min.y >= -0.001f && bounds.min.y <= 0.001f)
            {
                return;
            }

            var vertices = new List<Vector3>(mesh.vertexCount);
            mesh.GetVertices(vertices);
            var yOffset = -bounds.min.y;
            for (var i = 0; i < vertices.Count; i++)
            {
                vertices[i] = vertices[i] + Vector3.up * yOffset;
            }

            mesh.SetVertices(vertices);
            mesh.RecalculateBounds();
        }

        private static int CompareGrassRenderers(MeshRenderer left, MeshRenderer right)
        {
            var leftDistance = left != null ? left.transform.localPosition.sqrMagnitude : float.PositiveInfinity;
            var rightDistance = right != null ? right.transform.localPosition.sqrMagnitude : float.PositiveInfinity;
            return leftDistance.CompareTo(rightDistance);
        }

        private void ApplyWindSettings()
        {
            if (paletteMaterials == null)
            {
                return;
            }

            var settings = ResolveSettings();
            for (var i = 0; i < paletteMaterials.Length; i++)
            {
                var material = paletteMaterials[i];
                if (material == null)
                {
                    continue;
                }

                SetFloatIfPresent(material, "_WindStrength", settings.WindStrength);
                SetFloatIfPresent(material, "_WindSpeed", settings.WindSpeed);
                SetFloatIfPresent(material, "_WindScale", settings.WindScale);
            }
        }

        private static Mesh CreateGrassMesh(string meshName, int bladeCount)
        {
            var vertices = new List<Vector3>(bladeCount * 4);
            var normals = new List<Vector3>(bladeCount * 4);
            var uvs = new List<Vector2>(bladeCount * 4);
            var triangles = new List<int>(bladeCount * 6);

            for (var i = 0; i < bladeCount; i++)
            {
                var angle = (360f / bladeCount) * i + (i % 2) * 17f;
                var height = Mathf.Lerp(0.72f, 1.18f, Hash01(97, i, bladeCount, 13));
                var width = Mathf.Lerp(0.08f, 0.14f, Hash01(101, i, bladeCount, 29));
                var offset = new Vector3(
                    (Hash01(103, i, bladeCount, 31) - 0.5f) * 0.18f,
                    0f,
                    (Hash01(107, i, bladeCount, 37) - 0.5f) * 0.18f);
                AddBlade(vertices, normals, uvs, triangles, angle, width, height, offset);
            }

            var mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.DontSave
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddBlade(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            float yaw,
            float width,
            float height,
            Vector3 offset)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var bend = rotation * new Vector3(0f, 0f, width * 0.55f);
            var right = rotation * Vector3.right;
            var normal = rotation * Vector3.forward;
            var start = vertices.Count;

            vertices.Add(offset - right * width);
            vertices.Add(offset + right * width);
            vertices.Add(offset - right * width * 0.18f + Vector3.up * height + bend);
            vertices.Add(offset + right * width * 0.18f + Vector3.up * height + bend);

            for (var i = 0; i < 4; i++)
            {
                normals.Add(normal);
            }

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Material[] CreatePaletteMaterials()
        {
            var sourceMaterial = Resources.Load<Material>(GrassSourceMaterialPath);
            if (sourceMaterial != null)
            {
                return CreateSourceMaterialCopies(sourceMaterial);
            }

            var shader = Shader.Find("Legends/Procedural GPU Grass") ??
                         Shader.Find("HDRP/Unlit") ??
                         Shader.Find("HDRP/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            var sourceTexture = ResolveSourceTexture(sourceMaterial);

            var materials = new Material[PaletteCount];
            for (var i = 0; i < PaletteCount; i++)
            {
                var material = new Material(shader)
                {
                    name = $"Generated GPU Grass Palette {i + 1}",
                    hideFlags = HideFlags.DontSave,
                    enableInstancing = true,
                    renderQueue = (int)RenderQueue.AlphaTest
                };

                SetColorIfPresent(material, "_BottomColor", Color.white);
                SetColorIfPresent(material, "_TopColor", Color.white);
                SetColorIfPresent(material, "_BaseColor", Color.white);
                SetColorIfPresent(material, "_Color", Color.white);
                SetTextureIfPresent(material, "_BaseMap", sourceTexture);
                SetTextureIfPresent(material, "_BaseColorMap", sourceTexture);
                SetTextureIfPresent(material, "_MainTex", sourceTexture);
                SetFloatIfPresent(material, "_Ambient", 1f);
                SetFloatIfPresent(material, "_Cutoff", 0.18f);
                SetFloatIfPresent(material, "_AlphaCutoff", 0.18f);
                SetFloatIfPresent(material, "_AlphaClip", 1f);
                SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
                SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
                SetFloatIfPresent(material, "_CullMode", 0f);
                materials[i] = material;
            }

            return materials;
        }

        private static Material[] CreateSourceMaterialCopies(Material sourceMaterial)
        {
            var materials = new Material[PaletteCount];
            for (var i = 0; i < PaletteCount; i++)
            {
                var material = new Material(sourceMaterial)
                {
                    name = $"{sourceMaterial.name} GPU Grass {i + 1}",
                    hideFlags = HideFlags.DontSave,
                    enableInstancing = true,
                    renderQueue = sourceMaterial.renderQueue
                };
                materials[i] = material;
            }

            return materials;
        }

        private static Texture ResolveSourceTexture(Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            var texture = GetTextureIfPresent(sourceMaterial, "_BaseColorMap") ??
                          GetTextureIfPresent(sourceMaterial, "_BaseMap") ??
                          GetTextureIfPresent(sourceMaterial, "_MainTex");
            return texture;
        }

        private void ReleaseRuntimeResources()
        {
            DestroyRuntimeObject(highDetailMesh);
            DestroyRuntimeObject(lowDetailMesh);
            highDetailMesh = null;
            lowDetailMesh = null;

            if (paletteMaterials != null)
            {
                for (var i = 0; i < paletteMaterials.Length; i++)
                {
                    DestroyRuntimeObject(paletteMaterials[i]);
                }
            }

            paletteMaterials = null;
            initialized = false;
            chunks.Clear();
            visibleChunks.Clear();
            ClearBuckets(visibleHighMatrices);
            ClearBuckets(visibleLowMatrices);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private GpuGrassSettings ResolveSettings()
        {
            if (grassSettings == null)
            {
                grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            }

            return grassSettings;
        }

        private void EnsureBatchCapacity(int capacity)
        {
            if (batchMatrices.Length >= capacity)
            {
                return;
            }

            batchMatrices = new Matrix4x4[Mathf.NextPowerOfTwo(capacity)];
        }

        private static int EstimateBatchCount(List<Matrix4x4>[] buckets)
        {
            var batches = 0;
            for (var i = 0; i < buckets.Length; i++)
            {
                batches += Mathf.CeilToInt(buckets[i].Count / (float)MaxInstancesPerBatch);
            }

            return batches;
        }

        private static int CountBuckets(List<Matrix4x4>[] buckets)
        {
            var count = 0;
            for (var i = 0; i < buckets.Length; i++)
            {
                count += buckets[i].Count;
            }

            return count;
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

        private static int CountVertices(Mesh mesh)
        {
            return mesh != null ? mesh.vertexCount : 0;
        }

        private static List<Matrix4x4>[] CreateMatrixBuckets()
        {
            var buckets = new List<Matrix4x4>[PaletteCount];
            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i] = new List<Matrix4x4>(2048);
            }

            return buckets;
        }

        private static void ClearBuckets(List<Matrix4x4>[] buckets)
        {
            for (var i = 0; i < buckets.Length; i++)
            {
                buckets[i].Clear();
            }
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }

        private static float Hash01(int sourceSeed, int x, int z, int salt)
        {
            unchecked
            {
                var hash = (uint)sourceSeed;
                hash ^= (uint)(x * 374761393);
                hash ^= (uint)(z * 668265263);
                hash ^= (uint)(salt * 224682251);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static Texture GetTextureIfPresent(Material material, string propertyName)
        {
            return material != null && material.HasProperty(propertyName)
                ? material.GetTexture(propertyName)
                : null;
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (material != null && value != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        public readonly struct GpuGrassDiagnostic
        {
            public GpuGrassDiagnostic(
                string lodName,
                int visibleClumps,
                int trianglesPerClump,
                int verticesPerClump,
                int estimatedBatches)
            {
                LodName = lodName;
                VisibleClumps = visibleClumps;
                TrianglesPerClump = trianglesPerClump;
                VerticesPerClump = verticesPerClump;
                EstimatedBatches = estimatedBatches;
            }

            public string LodName { get; }
            public int VisibleClumps { get; }
            public int TrianglesPerClump { get; }
            public int VerticesPerClump { get; }
            public long VisibleTriangles => (long)VisibleClumps * TrianglesPerClump;
            public long VisibleVertices => (long)VisibleClumps * VerticesPerClump;
            public int EstimatedBatches { get; }
        }

        private sealed class GrassChunk
        {
            public GrassChunk(int indexX, int indexZ, float minX, float minZ, float maxX, float maxZ, Vector3 center, Bounds bounds)
            {
                IndexX = indexX;
                IndexZ = indexZ;
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
                Center = center;
                Bounds = bounds;
            }

            public int IndexX { get; }
            public int IndexZ { get; }
            public float MinX { get; }
            public float MinZ { get; }
            public float MaxX { get; }
            public float MaxZ { get; }
            public Vector3 Center { get; }
            public Bounds Bounds { get; }
            public bool Generated { get; set; }
            public int ClumpCount { get; set; }
            public List<Matrix4x4>[] MatricesByPalette { get; private set; }

            public void EnsureBuckets()
            {
                MatricesByPalette ??= CreateMatrixBuckets();
            }
        }

        private readonly struct VisibleChunk
        {
            public VisibleChunk(GrassChunk chunk, float sqrDistance)
            {
                Chunk = chunk;
                SqrDistance = sqrDistance;
            }

            public GrassChunk Chunk { get; }
            public float SqrDistance { get; }
        }
    }
}
