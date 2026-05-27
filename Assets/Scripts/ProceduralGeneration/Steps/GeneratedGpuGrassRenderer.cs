using System;
using System.Collections.Generic;
using System.Diagnostics;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GeneratedGpuGrassRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;
        private const string GrassSourceMaterialPath = "Materials/Plants/Fristy_Plant_Stylized_Art_03";
        private const string GrassFallbackMaterialPath = "Materials/Grass/Fristy_Grass_Common";
        private const string GrassFallbackTexturePath = "Textures/Plants/Fristy_Plant_Stylized_Art_03";
        private const int GrassLayer = 1;
        private const int GrassVariationLayer = 2;
        private const int ShoreLayer = 0;
        private const int RockLayer = 3;
        private const int HardVisibleGrassTriangleLimit = 300000;
        private const int NearBladeCount = 4;
        private const int MidBladeCount = 2;

        private static readonly int GrassTintId = Shader.PropertyToID("_GrassTint");
        private static readonly int GrassInstanceDataId = Shader.PropertyToID("_GrassInstanceData");
        private static readonly int AtlasColumnsId = Shader.PropertyToID("_AtlasColumns");
        private static readonly int AtlasRowsId = Shader.PropertyToID("_AtlasRows");
        private static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");
        private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");
        private static readonly int WindScaleId = Shader.PropertyToID("_WindScale");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BottomColorId = Shader.PropertyToID("_BottomColor");
        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");

        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private GpuGrassSettings grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
        [SerializeField] private int seed;
        [SerializeField] private float waterLevel;

        private readonly List<GrassCluster> clusters = new();
        private readonly List<GrassInstancePacked> packedInstances = new();
        private readonly List<VisibleCluster> visibleClusters = new();
        private readonly List<Matrix4x4> visibleNearMatrices = new(32768);
        private readonly List<Matrix4x4> visibleMidMatrices = new(32768);
        private readonly List<Vector4> visibleNearTints = new(32768);
        private readonly List<Vector4> visibleMidTints = new(32768);
        private readonly List<Vector4> visibleNearInstanceData = new(32768);
        private readonly List<Vector4> visibleMidInstanceData = new(32768);
        private readonly Plane[] frustumPlanes = new Plane[6];
        private readonly Matrix4x4[] batchMatrices = new Matrix4x4[MaxInstancesPerBatch];
        private readonly Vector4[] batchTints = new Vector4[MaxInstancesPerBatch];
        private readonly Vector4[] batchInstanceData = new Vector4[MaxInstancesPerBatch];
        private readonly Stopwatch cullingStopwatch = new();

        private Mesh nearGrassMesh;
        private Mesh midGrassMesh;
        private Material grassMaterial;
        private MaterialPropertyBlock materialProperties;
        private float[] densityValues = Array.Empty<float>();
        private Texture2D densityDebugTexture;
        private float[,,] alphamaps;
        private int densityResolution;
        private int alphamapWidth;
        private int alphamapHeight;
        private int alphamapLayers;
        private int resolvedGrassSeed;
        private Bounds terrainBounds;
        private bool initialized;
        private bool terrainTintApplied;

        public int ChunkCount => clusters.Count;
        public int ClusterCount => clusters.Count;
        public int GeneratedChunkCount { get; private set; }
        public int GeneratedClusterCount { get; private set; }
        public int GeneratedClumpCount { get; private set; }
        public int RuntimeGeneratedInRenderCount { get; private set; }
        public int LastVisibleClumps { get; private set; }
        public int LastVisibleNearInstances { get; private set; }
        public int LastVisibleMidInstances { get; private set; }
        public int LastVisibleClustersTotal { get; private set; }
        public int LastVisibleClustersNear { get; private set; }
        public int LastVisibleClustersMid { get; private set; }
        public int LastVisibleClustersFar { get; private set; }
        public int LastVisibleGrassTriangles { get; private set; }
        public int LastVisibleNearTriangles { get; private set; }
        public int LastVisibleMidTriangles { get; private set; }
        public int LastBatchCount { get; private set; }
        public int LastMaterialBucketCount { get; private set; }
        public int LastShadowCasterInstances { get; private set; }
        public double LastCullingMilliseconds { get; private set; }
        public double LastBuildMilliseconds { get; private set; }
        public Texture2D DensityDebugTexture => densityDebugTexture;

        public void Initialize(Terrain terrain, int generationSeed, GpuGrassSettings settings, float terrainWaterLevel)
        {
            targetTerrain = terrain;
            seed = generationSeed;
            grassSettings = settings != null ? settings.Clone() : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            waterLevel = terrainWaterLevel;
            resolvedGrassSeed = unchecked(seed + grassSettings.GrassSeed * 1009);
            terrainTintApplied = false;

            ReleaseRuntimeResources();

            var stopwatch = Stopwatch.StartNew();
            CreateRuntimeResources();
            CacheTerrainData();
            BuildDensityGrid();
            ApplyTerrainDensityTint();
            BuildClusterInstances();
            stopwatch.Stop();

            LastBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            initialized = targetTerrain != null &&
                          targetTerrain.terrainData != null &&
                          clusters.Count > 0 &&
                          nearGrassMesh != null &&
                          midGrassMesh != null &&
                          grassMaterial != null;
        }

        public void AddDiagnostics(Camera camera, IList<GpuGrassDiagnostic> diagnostics)
        {
            if (diagnostics == null || !EnsureInitialized())
            {
                return;
            }

            PrepareVisibleInstances(camera);
            diagnostics.Add(new GpuGrassDiagnostic(
                "GPU Grass Near",
                LastVisibleNearInstances,
                CountTriangles(nearGrassMesh),
                CountVertices(nearGrassMesh),
                EstimateBatchCount(visibleNearMatrices.Count),
                LastVisibleClustersNear,
                LastMaterialBucketCount,
                RuntimeGeneratedInRenderCount,
                LastShadowCasterInstances,
                LastCullingMilliseconds,
                LastBuildMilliseconds));
            diagnostics.Add(new GpuGrassDiagnostic(
                "GPU Grass Mid",
                LastVisibleMidInstances,
                CountTriangles(midGrassMesh),
                CountVertices(midGrassMesh),
                EstimateBatchCount(visibleMidMatrices.Count),
                LastVisibleClustersMid,
                LastMaterialBucketCount,
                RuntimeGeneratedInRenderCount,
                0,
                LastCullingMilliseconds,
                LastBuildMilliseconds));
            diagnostics.Add(new GpuGrassDiagnostic(
                "GPU Grass Far Terrain Tint",
                0,
                0,
                0,
                0,
                LastVisibleClustersFar,
                LastMaterialBucketCount,
                RuntimeGeneratedInRenderCount,
                0,
                LastCullingMilliseconds,
                LastBuildMilliseconds));
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

        private void OnDrawGizmosSelected()
        {
            if (!initialized || clusters.Count == 0)
            {
                return;
            }

            var step = Mathf.Max(1, clusters.Count / 512);
            for (var i = 0; i < clusters.Count; i += step)
            {
                var cluster = clusters[i];
                var density = Mathf.Clamp01(cluster.DensitySummary);
                Gizmos.color = Color.Lerp(new Color(0.18f, 0.18f, 0.18f, 0.18f), new Color(0.2f, 0.9f, 0.16f, 0.55f), density);
                Gizmos.DrawWireCube(cluster.Bounds.center, new Vector3(cluster.Bounds.size.x, 0.12f + density * 0.35f, cluster.Bounds.size.z));
            }
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

            PrepareVisibleInstances(camera);
            DrawInstances(camera, nearGrassMesh, visibleNearMatrices, visibleNearTints, visibleNearInstanceData, ResolveSettings().EnableGrassShadows);
            DrawInstances(camera, midGrassMesh, visibleMidMatrices, visibleMidTints, visibleMidInstanceData, false);
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

            resolvedGrassSeed = unchecked(seed + grassSettings.GrassSeed * 1009);

            var stopwatch = Stopwatch.StartNew();
            CreateRuntimeResources();
            CacheTerrainData();
            BuildDensityGrid();
            ApplyTerrainDensityTint();
            BuildClusterInstances();
            stopwatch.Stop();

            LastBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            initialized = clusters.Count > 0 && nearGrassMesh != null && midGrassMesh != null && grassMaterial != null;
            return initialized;
        }

        private void PrepareVisibleInstances(Camera camera)
        {
            ClearVisibleData();
            RuntimeGeneratedInRenderCount = 0;

            if (camera == null)
            {
                return;
            }

            var settings = ResolveSettings();
            var triangleBudget = ResolveTriangleBudget(settings);
            if (!settings.Enabled || !settings.UseOptimizedClusterRenderer || triangleBudget <= 0)
            {
                return;
            }

            cullingStopwatch.Restart();

            var cameraPosition = camera.transform.position;
            var farDistance = settings.FarVisualDistance + settings.ClusterSize;
            var farSqrDistance = farDistance * farDistance;
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);

            for (var i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                var sqrDistance = SqrDistanceXZ(cluster.Center, cameraPosition);
                if (sqrDistance > farSqrDistance || !GeometryUtility.TestPlanesAABB(frustumPlanes, cluster.Bounds))
                {
                    continue;
                }

                var distance = Mathf.Sqrt(sqrDistance);
                var ring = ClassifyRing(distance, settings);
                if (ring == GrassRing.None)
                {
                    continue;
                }

                LastVisibleClustersTotal++;
                switch (ring)
                {
                    case GrassRing.Near:
                        LastVisibleClustersNear++;
                        break;
                    case GrassRing.Mid:
                        LastVisibleClustersMid++;
                        break;
                    case GrassRing.Far:
                        LastVisibleClustersFar++;
                        break;
                }

                visibleClusters.Add(new VisibleCluster(i, sqrDistance, ring));
            }

            visibleClusters.Sort((left, right) => left.SqrDistance.CompareTo(right.SqrDistance));

            for (var i = 0; i < visibleClusters.Count; i++)
            {
                if (visibleClusters[i].Ring == GrassRing.Near)
                {
                    AddClusterInstances(clusters[visibleClusters[i].ClusterIndex], GrassRing.Near, cameraPosition, settings, triangleBudget);
                }
            }

            for (var i = 0; i < visibleClusters.Count; i++)
            {
                if (visibleClusters[i].Ring == GrassRing.Mid)
                {
                    AddClusterInstances(clusters[visibleClusters[i].ClusterIndex], GrassRing.Mid, cameraPosition, settings, triangleBudget);
                }
            }

            LastVisibleClumps = LastVisibleNearInstances + LastVisibleMidInstances;
            LastVisibleNearTriangles = LastVisibleNearInstances * CountTriangles(nearGrassMesh);
            LastVisibleMidTriangles = LastVisibleMidInstances * CountTriangles(midGrassMesh);
            LastVisibleGrassTriangles = LastVisibleNearTriangles + LastVisibleMidTriangles;
            LastBatchCount = EstimateBatchCount(visibleNearMatrices.Count) + EstimateBatchCount(visibleMidMatrices.Count);
            LastMaterialBucketCount = LastVisibleClumps > 0 ? 1 : 0;
            LastShadowCasterInstances = settings.EnableGrassShadows ? LastVisibleNearInstances : 0;

            cullingStopwatch.Stop();
            LastCullingMilliseconds = cullingStopwatch.Elapsed.TotalMilliseconds;
        }

        private void ClearVisibleData()
        {
            visibleClusters.Clear();
            visibleNearMatrices.Clear();
            visibleMidMatrices.Clear();
            visibleNearTints.Clear();
            visibleMidTints.Clear();
            visibleNearInstanceData.Clear();
            visibleMidInstanceData.Clear();
            LastVisibleClumps = 0;
            LastVisibleNearInstances = 0;
            LastVisibleMidInstances = 0;
            LastVisibleClustersTotal = 0;
            LastVisibleClustersNear = 0;
            LastVisibleClustersMid = 0;
            LastVisibleClustersFar = 0;
            LastVisibleGrassTriangles = 0;
            LastVisibleNearTriangles = 0;
            LastVisibleMidTriangles = 0;
            LastBatchCount = 0;
            LastMaterialBucketCount = 0;
            LastShadowCasterInstances = 0;
            LastCullingMilliseconds = 0d;
        }

        private void AddClusterInstances(
            GrassCluster cluster,
            GrassRing ring,
            Vector3 cameraPosition,
            GpuGrassSettings settings,
            int triangleBudget)
        {
            if (cluster.InstanceCount <= 0)
            {
                return;
            }

            var meshTriangles = ring == GrassRing.Near ? CountTriangles(nearGrassMesh) : CountTriangles(midGrassMesh);
            if (meshTriangles <= 0)
            {
                return;
            }

            var maxInstances = ring == GrassRing.Near
                ? settings.MaxVisibleNearInstances
                : settings.MaxVisibleMidInstances;
            var targetMatrices = ring == GrassRing.Near ? visibleNearMatrices : visibleMidMatrices;
            var targetTints = ring == GrassRing.Near ? visibleNearTints : visibleMidTints;
            var targetInstanceData = ring == GrassRing.Near ? visibleNearInstanceData : visibleMidInstanceData;

            for (var i = 0; i < cluster.InstanceCount; i++)
            {
                if (targetMatrices.Count >= maxInstances ||
                    LastVisibleNearTriangles + LastVisibleMidTriangles + meshTriangles > triangleBudget)
                {
                    return;
                }

                var instance = packedInstances[cluster.InstanceStart + i];
                var selection = instance.Selection01;
                if (ring == GrassRing.Mid && selection > settings.MidDensityMultiplier)
                {
                    continue;
                }

                var distance = Mathf.Sqrt(SqrDistanceXZ(instance.Position, cameraPosition));
                var fade = ResolveDistanceFade(distance, ring, settings);
                if (fade <= 0.01f)
                {
                    continue;
                }

                var scale = instance.Scale;
                if (ring == GrassRing.Mid)
                {
                    scale *= Mathf.Lerp(0.94f, 1.08f, selection);
                }

                var matrix = Matrix4x4.TRS(
                    instance.Position,
                    Quaternion.Euler(0f, instance.YawDegrees, 0f),
                    new Vector3(scale, scale, scale));

                targetMatrices.Add(matrix);
                targetTints.Add(ResolveTint(instance.Tint));
                targetInstanceData.Add(new Vector4(instance.Variant, fade, selection, 0f));

                if (ring == GrassRing.Near)
                {
                    LastVisibleNearInstances++;
                    LastVisibleNearTriangles += meshTriangles;
                }
                else
                {
                    LastVisibleMidInstances++;
                    LastVisibleMidTriangles += meshTriangles;
                }
            }
        }

        private static GrassRing ClassifyRing(float distance, GpuGrassSettings settings)
        {
            if (distance <= settings.NearDistance)
            {
                return GrassRing.Near;
            }

            if (distance <= settings.MidDistance)
            {
                return GrassRing.Mid;
            }

            return distance <= settings.FarVisualDistance ? GrassRing.Far : GrassRing.None;
        }

        private static float ResolveDistanceFade(float distance, GrassRing ring, GpuGrassSettings settings)
        {
            if (ring != GrassRing.Mid || settings.LodFadeDistance <= 0.01f)
            {
                return 1f;
            }

            var fadeStart = Mathf.Max(settings.NearDistance, settings.MidDistance - settings.LodFadeDistance);
            return 1f - SmoothRange(fadeStart, settings.MidDistance, distance);
        }

        private static int ResolveTriangleBudget(GpuGrassSettings settings)
        {
            return Mathf.Clamp(settings.MaxVisibleGrassTriangles, 0, HardVisibleGrassTriangleLimit);
        }

        private void DrawInstances(
            Camera camera,
            Mesh mesh,
            List<Matrix4x4> matrices,
            List<Vector4> tints,
            List<Vector4> instanceData,
            bool castShadows)
        {
            if (mesh == null || grassMaterial == null || matrices.Count == 0)
            {
                return;
            }

            materialProperties ??= new MaterialPropertyBlock();

            var settings = ResolveSettings();
            var renderParams = new RenderParams(grassMaterial)
            {
                camera = camera,
                layer = gameObject.layer,
                renderingLayerMask = 1u,
                receiveShadows = settings.ReceiveShadows,
                shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                worldBounds = terrainBounds
            };

            for (var start = 0; start < matrices.Count; start += MaxInstancesPerBatch)
            {
                var count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
                matrices.CopyTo(start, batchMatrices, 0, count);
                tints.CopyTo(start, batchTints, 0, count);
                instanceData.CopyTo(start, batchInstanceData, 0, count);

                materialProperties.Clear();
                materialProperties.SetVectorArray(GrassTintId, batchTints);
                materialProperties.SetVectorArray(GrassInstanceDataId, batchInstanceData);
                renderParams.matProps = materialProperties;
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, batchMatrices, count);
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
                terrainBounds = default;
                return;
            }

            var data = targetTerrain.terrainData;
            terrainBounds = new Bounds(
                targetTerrain.transform.position + data.size * 0.5f,
                data.size + new Vector3(0f, 4f, 0f));

            if (data.alphamapLayers <= 0 || data.alphamapWidth <= 0 || data.alphamapHeight <= 0)
            {
                return;
            }

            alphamapWidth = data.alphamapWidth;
            alphamapHeight = data.alphamapHeight;
            alphamapLayers = data.alphamapLayers;
            alphamaps = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
        }

        private void BuildDensityGrid()
        {
            densityValues = Array.Empty<float>();
            densityResolution = 0;
            DestroyRuntimeObject(densityDebugTexture);
            densityDebugTexture = null;

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return;
            }

            var settings = ResolveSettings();
            densityResolution = settings.DensityGridResolution;
            densityValues = new float[densityResolution * densityResolution];

            var pixels = new Color[densityValues.Length];
            for (var z = 0; z < densityResolution; z++)
            {
                for (var x = 0; x < densityResolution; x++)
                {
                    var nx = (x + 0.5f) / densityResolution;
                    var nz = (z + 0.5f) / densityResolution;
                    var density = ComputeDensity01(nx, nz, settings);
                    var index = z * densityResolution + x;
                    densityValues[index] = density;
                    pixels[index] = new Color(density, density * 0.82f, density * 0.28f, 1f);
                }
            }

            densityDebugTexture = new Texture2D(densityResolution, densityResolution, TextureFormat.RGBA32, false, true)
            {
                name = "Generated Grass Density Debug",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            densityDebugTexture.SetPixels(pixels);
            densityDebugTexture.Apply(false, false);
        }

        private float ComputeDensity01(float nx, float nz, GpuGrassSettings settings)
        {
            var data = targetTerrain.terrainData;
            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = data.size;
            var normalizedHeight = data.GetInterpolatedHeight(nx, nz) / Mathf.Max(1f, terrainSize.y);
            var slope = Vector3.Angle(data.GetInterpolatedNormal(nx, nz), Vector3.up);
            var aboveWater = normalizedHeight - Mathf.Clamp01(waterLevel / Mathf.Max(1f, terrainSize.y));
            var worldX = terrainPosition.x + nx * terrainSize.x;
            var worldZ = terrainPosition.z + nz * terrainSize.z;

            var layerMask = SampleGrassLayer(nx, nz);
            var slopeMask = 1f - SmoothRange(20f, 44f, slope);
            var waterMask = SmoothRange(0.018f, 0.08f, aboveWater);
            var heightMask = 1f - SmoothRange(0.78f, 0.96f, normalizedHeight);
            var macro = FbmNoise(worldX / settings.MacroNoiseScale, worldZ / settings.MacroNoiseScale, resolvedGrassSeed, settings);
            var micro = FbmNoise(worldX / settings.MicroNoiseScale, worldZ / settings.MicroNoiseScale, resolvedGrassSeed + 137, settings);
            var noise = RemapContrast(macro * 0.75f + micro * 0.25f, settings.NoiseContrast);
            var noiseMask = SmoothRange(settings.NoiseThresholdLow, settings.NoiseThresholdHigh, noise);

            return Mathf.Clamp01(layerMask * slopeMask * waterMask * heightMask * noiseMask);
        }

        private void ApplyTerrainDensityTint()
        {
            var settings = ResolveSettings();
            if (terrainTintApplied ||
                !settings.EnableTerrainDensityTint ||
                targetTerrain == null ||
                targetTerrain.terrainData == null ||
                alphamaps == null ||
                alphamapLayers <= GrassLayer)
            {
                return;
            }

            var terrainData = targetTerrain.terrainData;
            var modified = new float[alphamapHeight, alphamapWidth, alphamapLayers];
            for (var z = 0; z < alphamapHeight; z++)
            {
                for (var x = 0; x < alphamapWidth; x++)
                {
                    var nx = alphamapWidth > 1 ? x / (float)(alphamapWidth - 1) : 0f;
                    var nz = alphamapHeight > 1 ? z / (float)(alphamapHeight - 1) : 0f;
                    var density = SampleDensity01(nx, nz);
                    var grassSemantic = alphamaps[z, x, GrassLayer];
                    if (alphamapLayers > GrassVariationLayer)
                    {
                        grassSemantic += alphamaps[z, x, GrassVariationLayer] * 0.75f;
                    }

                    var tint = density * Mathf.Clamp01(grassSemantic);
                    var total = 0f;
                    for (var layer = 0; layer < alphamapLayers; layer++)
                    {
                        var value = alphamaps[z, x, layer];
                        if (layer == GrassLayer)
                        {
                            value *= 1f + tint * 0.28f;
                        }
                        else if (layer == GrassVariationLayer)
                        {
                            value += tint * 0.18f;
                        }
                        else if (layer == RockLayer || layer == ShoreLayer)
                        {
                            value *= 1f - tint * 0.08f;
                        }

                        modified[z, x, layer] = Mathf.Max(0f, value);
                        total += modified[z, x, layer];
                    }

                    if (total <= 0f)
                    {
                        modified[z, x, GrassLayer] = 1f;
                        continue;
                    }

                    for (var layer = 0; layer < alphamapLayers; layer++)
                    {
                        modified[z, x, layer] /= total;
                    }
                }
            }

            terrainData.SetAlphamaps(0, 0, modified);
            targetTerrain.Flush();
            alphamaps = modified;
            terrainTintApplied = true;
        }

        private void BuildClusterInstances()
        {
            clusters.Clear();
            packedInstances.Clear();
            GeneratedChunkCount = 0;
            GeneratedClusterCount = 0;
            GeneratedClumpCount = 0;

            if (targetTerrain == null || targetTerrain.terrainData == null || densityValues.Length == 0)
            {
                return;
            }

            var settings = ResolveSettings();
            var data = targetTerrain.terrainData;
            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = data.size;
            var clusterSize = settings.ClusterSize;
            var clusterCountX = Mathf.CeilToInt(terrainSize.x / clusterSize);
            var clusterCountZ = Mathf.CeilToInt(terrainSize.z / clusterSize);

            for (var z = 0; z < clusterCountZ; z++)
            {
                for (var x = 0; x < clusterCountX; x++)
                {
                    var minX = terrainPosition.x + x * clusterSize;
                    var minZ = terrainPosition.z + z * clusterSize;
                    var maxX = Mathf.Min(terrainPosition.x + terrainSize.x, minX + clusterSize);
                    var maxZ = Mathf.Min(terrainPosition.z + terrainSize.z, minZ + clusterSize);
                    var center = new Vector3((minX + maxX) * 0.5f, terrainPosition.y, (minZ + maxZ) * 0.5f);
                    var densitySummary = SampleClusterDensity(minX, minZ, maxX, maxZ);
                    if (densitySummary <= 0.015f)
                    {
                        continue;
                    }

                    var start = packedInstances.Count;
                    GenerateClusterInstances(x, z, minX, minZ, maxX, maxZ, settings);
                    var count = packedInstances.Count - start;
                    if (count <= 0 && densitySummary <= 0.08f)
                    {
                        continue;
                    }

                    var bounds = new Bounds(
                        new Vector3(center.x, terrainPosition.y + terrainSize.y * 0.5f, center.z),
                        new Vector3(Mathf.Max(0.1f, maxX - minX), terrainSize.y + 3f, Mathf.Max(0.1f, maxZ - minZ)));
                    clusters.Add(new GrassCluster(center, bounds, start, count, densitySummary));
                }
            }

            GeneratedChunkCount = clusters.Count;
            GeneratedClusterCount = clusters.Count;
            GeneratedClumpCount = packedInstances.Count;
        }

        private void GenerateClusterInstances(
            int clusterX,
            int clusterZ,
            float minX,
            float minZ,
            float maxX,
            float maxZ,
            GpuGrassSettings settings)
        {
            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = targetTerrain.terrainData.size;
            var spacing = settings.PlacementSpacing;
            var cellsX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / spacing));
            var cellsZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / spacing));
            var cellWidth = (maxX - minX) / cellsX;
            var cellDepth = (maxZ - minZ) / cellsZ;

            for (var z = 0; z < cellsZ; z++)
            {
                for (var x = 0; x < cellsX; x++)
                {
                    var saltX = clusterX * 73856093 + x;
                    var saltZ = clusterZ * 19349663 + z;
                    var jitterX = Hash01(resolvedGrassSeed, saltX, saltZ, 11);
                    var jitterZ = Hash01(resolvedGrassSeed, saltX, saltZ, 23);
                    var worldX = minX + (x + jitterX) * cellWidth;
                    var worldZ = minZ + (z + jitterZ) * cellDepth;
                    if (worldX >= maxX || worldZ >= maxZ)
                    {
                        continue;
                    }

                    var density = Mathf.Clamp01(SampleDensityWorld(worldX, worldZ) * settings.DensityMultiplier);
                    if (density <= 0.02f || Hash01(resolvedGrassSeed, saltX, saltZ, 29) > density)
                    {
                        continue;
                    }

                    var nx = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, worldX);
                    var nz = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, worldZ);
                    var height = targetTerrain.terrainData.GetInterpolatedHeight(nx, nz) + terrainPosition.y;
                    if (height <= waterLevel + 0.08f)
                    {
                        continue;
                    }

                    var yaw = Mathf.RoundToInt(Hash01(resolvedGrassSeed, saltX, saltZ, 37) * ushort.MaxValue);
                    var scale01 = Mathf.Lerp(0.72f, 1.12f, Hash01(resolvedGrassSeed, saltX, saltZ, 41)) *
                                  Mathf.Lerp(0.9f, 1.08f, density);
                    var packedScale = Mathf.RoundToInt(Mathf.Clamp01((scale01 - 0.45f) / 0.95f) * ushort.MaxValue);
                    var variantCount = Mathf.Max(1, settings.AtlasColumns * settings.AtlasRows);
                    var variant = Mathf.Clamp(
                        Mathf.FloorToInt(Hash01(resolvedGrassSeed, saltX, saltZ, 47) * variantCount),
                        0,
                        variantCount - 1);
                    var tint = Mathf.RoundToInt(Hash01(resolvedGrassSeed, saltX, saltZ, 53) * byte.MaxValue);
                    var selector = Mathf.RoundToInt(Hash01(resolvedGrassSeed, saltX, saltZ, 59) * byte.MaxValue);

                    packedInstances.Add(new GrassInstancePacked(
                        new Vector3(worldX, height + 0.02f, worldZ),
                        (ushort)Mathf.Clamp(yaw, 0, ushort.MaxValue),
                        (ushort)Mathf.Clamp(packedScale, 0, ushort.MaxValue),
                        (byte)variant,
                        (byte)Mathf.Clamp(tint, 0, byte.MaxValue),
                        (byte)Mathf.Clamp(selector, 0, byte.MaxValue)));
                }
            }
        }

        private float SampleClusterDensity(float minX, float minZ, float maxX, float maxZ)
        {
            var centerX = (minX + maxX) * 0.5f;
            var centerZ = (minZ + maxZ) * 0.5f;
            var center = SampleDensityWorld(centerX, centerZ);
            var corners =
                SampleDensityWorld(minX, minZ) +
                SampleDensityWorld(maxX, minZ) +
                SampleDensityWorld(minX, maxZ) +
                SampleDensityWorld(maxX, maxZ);
            return Mathf.Clamp01(Mathf.Max(center, (center * 2f + corners) / 6f));
        }

        private float SampleDensityWorld(float worldX, float worldZ)
        {
            if (targetTerrain == null || targetTerrain.terrainData == null || densityValues.Length == 0)
            {
                return 0f;
            }

            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = targetTerrain.terrainData.size;
            var nx = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, worldX);
            var nz = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, worldZ);
            return SampleDensity01(nx, nz);
        }

        private float SampleDensity01(float nx, float nz)
        {
            if (densityValues.Length == 0 || densityResolution <= 0)
            {
                return 0f;
            }

            var px = Mathf.Clamp01(nx) * (densityResolution - 1);
            var pz = Mathf.Clamp01(nz) * (densityResolution - 1);
            var x0 = Mathf.Clamp(Mathf.FloorToInt(px), 0, densityResolution - 1);
            var z0 = Mathf.Clamp(Mathf.FloorToInt(pz), 0, densityResolution - 1);
            var x1 = Mathf.Min(x0 + 1, densityResolution - 1);
            var z1 = Mathf.Min(z0 + 1, densityResolution - 1);
            var tx = px - x0;
            var tz = pz - z0;
            var a = densityValues[z0 * densityResolution + x0];
            var b = densityValues[z0 * densityResolution + x1];
            var c = densityValues[z1 * densityResolution + x0];
            var d = densityValues[z1 * densityResolution + x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private float SampleGrassLayer(float nx, float nz)
        {
            if (alphamaps == null || alphamapLayers == 0)
            {
                return 1f;
            }

            var x = Mathf.Clamp(Mathf.RoundToInt(nx * (alphamapWidth - 1)), 0, alphamapWidth - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(nz * (alphamapHeight - 1)), 0, alphamapHeight - 1);
            var grass = alphamapLayers > GrassLayer ? alphamaps[z, x, GrassLayer] : 1f;
            var variation = alphamapLayers > GrassVariationLayer ? alphamaps[z, x, GrassVariationLayer] : 0f;
            var shore = alphamapLayers > ShoreLayer ? alphamaps[z, x, ShoreLayer] : 0f;
            var rock = alphamapLayers > RockLayer ? alphamaps[z, x, RockLayer] : 0f;
            return Mathf.Clamp01((grass + variation * 0.85f) * (1f - shore * 0.7f) * (1f - rock * 0.9f));
        }

        private void CreateRuntimeResources()
        {
            nearGrassMesh ??= CreateGrassMesh("Generated GPU Grass Near Cards", NearBladeCount);
            midGrassMesh ??= CreateGrassMesh("Generated GPU Grass Mid Cards", MidBladeCount);
            grassMaterial ??= CreateGrassMaterial();
            ApplyMaterialSettings();
        }

        private Material CreateGrassMaterial()
        {
            var sourceMaterial = Resources.Load<Material>(GrassSourceMaterialPath) ??
                                 Resources.Load<Material>(GrassFallbackMaterialPath);
            var shader = Shader.Find("Legends/Procedural GPU Grass") ??
                         Shader.Find("HDRP/Unlit") ??
                         Shader.Find("HDRP/Lit") ??
                         Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Color");
            var material = shader != null
                ? new Material(shader)
                : sourceMaterial != null
                    ? new Material(sourceMaterial)
                    : null;

            if (material == null)
            {
                Debug.LogWarning("GPU grass material could not be created: no compatible shader or source material was found.");
                return null;
            }

            material.name = "Generated GPU Grass Atlas";
            material.hideFlags = HideFlags.DontSave;
            material.enableInstancing = true;
            material.renderQueue = sourceMaterial != null && sourceMaterial.renderQueue > 0
                ? sourceMaterial.renderQueue
                : (int)RenderQueue.AlphaTest;

            var sourceTexture = ResolveSourceTexture(sourceMaterial) ??
                                Resources.Load<Texture>(GrassFallbackTexturePath);
            SetTextureIfPresent(material, BaseMapId, sourceTexture);
            SetTextureIfPresent(material, BaseColorMapId, sourceTexture);
            SetTextureIfPresent(material, MainTexId, sourceTexture);
            SetColorIfPresent(material, BottomColorId, new Color(0.12f, 0.28f, 0.08f, 1f));
            SetColorIfPresent(material, TopColorId, new Color(0.44f, 0.74f, 0.22f, 1f));
            SetFloatIfPresent(material, CutoffId, 0.08f);
            return material;
        }

        private void ApplyMaterialSettings()
        {
            if (grassMaterial == null)
            {
                return;
            }

            var settings = ResolveSettings();
            SetFloatIfPresent(grassMaterial, WindStrengthId, settings.WindStrength);
            SetFloatIfPresent(grassMaterial, WindSpeedId, settings.WindSpeed);
            SetFloatIfPresent(grassMaterial, WindScaleId, settings.WindScale);
            SetFloatIfPresent(grassMaterial, AtlasColumnsId, settings.AtlasColumns);
            SetFloatIfPresent(grassMaterial, AtlasRowsId, settings.AtlasRows);
        }

        private static Mesh CreateGrassMesh(string meshName, int bladeCount)
        {
            var vertices = new List<Vector3>(bladeCount * 4);
            var normals = new List<Vector3>(bladeCount * 4);
            var uvs = new List<Vector2>(bladeCount * 4);
            var triangles = new List<int>(bladeCount * 6);

            for (var i = 0; i < bladeCount; i++)
            {
                var angle = (360f / bladeCount) * i + (i % 2) * 13f;
                var height = Mathf.Lerp(0.34f, 0.58f, Hash01(97, i, bladeCount, 13));
                var width = Mathf.Lerp(0.035f, 0.068f, Hash01(101, i, bladeCount, 29));
                var offset = new Vector3(
                    (Hash01(103, i, bladeCount, 31) - 0.5f) * 0.08f,
                    0f,
                    (Hash01(107, i, bladeCount, 37) - 0.5f) * 0.08f);
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
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(0.6f, 0.4f, 0.6f));
            mesh.bounds = bounds;
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

        private void ReleaseRuntimeResources()
        {
            DestroyRuntimeObject(nearGrassMesh);
            DestroyRuntimeObject(midGrassMesh);
            DestroyRuntimeObject(grassMaterial);
            DestroyRuntimeObject(densityDebugTexture);
            nearGrassMesh = null;
            midGrassMesh = null;
            grassMaterial = null;
            densityDebugTexture = null;
            materialProperties = null;
            densityValues = Array.Empty<float>();
            initialized = false;
            clusters.Clear();
            packedInstances.Clear();
            visibleClusters.Clear();
            ClearVisibleData();
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

        private static int EstimateBatchCount(int instanceCount)
        {
            return Mathf.CeilToInt(instanceCount / (float)MaxInstancesPerBatch);
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

        private static float SqrDistanceXZ(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }

        private static float RemapContrast(float value, float contrast)
        {
            return Mathf.Clamp01((value - 0.5f) * contrast + 0.5f);
        }

        private static float FbmNoise(float x, float z, int sourceSeed, GpuGrassSettings settings)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var sum = 0f;
            var weight = 0f;
            var seedA = (sourceSeed & 0xFFFF) * 0.00073f;
            var seedB = ((sourceSeed >> 8) & 0xFFFF) * 0.00091f;

            for (var octave = 0; octave < settings.NoiseOctaves; octave++)
            {
                var sample = Mathf.PerlinNoise(
                    x * frequency + seedA + octave * 19.37f,
                    z * frequency + seedB + octave * 31.91f);
                sum += sample * amplitude;
                weight += amplitude;
                amplitude *= settings.NoisePersistence;
                frequency *= settings.NoiseLacunarity;
            }

            return weight > 0f ? Mathf.Clamp01(sum / weight) : 0f;
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

        private static Vector4 ResolveTint(byte tint)
        {
            var t = tint / 255f;
            var dry = new Color(0.74f, 0.68f, 0.38f, 1f);
            var lush = new Color(0.62f, 0.92f, 0.32f, 1f);
            var shade = new Color(0.42f, 0.72f, 0.24f, 1f);
            var color = t < 0.5f
                ? Color.Lerp(dry, shade, t * 2f)
                : Color.Lerp(shade, lush, (t - 0.5f) * 2f);
            return new Vector4(color.r, color.g, color.b, 0.62f);
        }

        private static Texture ResolveSourceTexture(Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return null;
            }

            return GetTextureIfPresent(sourceMaterial, "_BaseColorMap") ??
                   GetTextureIfPresent(sourceMaterial, "_BaseMap") ??
                   GetTextureIfPresent(sourceMaterial, "_MainTex");
        }

        private static Texture GetTextureIfPresent(Material material, string propertyName)
        {
            return material != null && material.HasProperty(propertyName)
                ? material.GetTexture(propertyName)
                : null;
        }

        private static void SetTextureIfPresent(Material material, int propertyId, Texture value)
        {
            if (material != null && value != null && material.HasProperty(propertyId))
            {
                material.SetTexture(propertyId, value);
            }
        }

        private static void SetColorIfPresent(Material material, int propertyId, Color value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetColor(propertyId, value);
            }
        }

        private static void SetFloatIfPresent(Material material, int propertyId, float value)
        {
            if (material != null && material.HasProperty(propertyId))
            {
                material.SetFloat(propertyId, value);
            }
        }

        public readonly struct GpuGrassDiagnostic
        {
            public GpuGrassDiagnostic(
                string lodName,
                int visibleClumps,
                int trianglesPerClump,
                int verticesPerClump,
                int estimatedBatches,
                int visibleClusters,
                int materialBuckets,
                int runtimeGeneratedInRender,
                int shadowCasterInstances,
                double cullingMilliseconds,
                double buildMilliseconds)
            {
                LodName = lodName;
                VisibleClumps = visibleClumps;
                TrianglesPerClump = trianglesPerClump;
                VerticesPerClump = verticesPerClump;
                EstimatedBatches = estimatedBatches;
                VisibleClusters = visibleClusters;
                MaterialBuckets = materialBuckets;
                RuntimeGeneratedInRender = runtimeGeneratedInRender;
                ShadowCasterInstances = shadowCasterInstances;
                CullingMilliseconds = cullingMilliseconds;
                BuildMilliseconds = buildMilliseconds;
            }

            public string LodName { get; }
            public int VisibleClumps { get; }
            public int TrianglesPerClump { get; }
            public int VerticesPerClump { get; }
            public long VisibleTriangles => (long)VisibleClumps * TrianglesPerClump;
            public long VisibleVertices => (long)VisibleClumps * VerticesPerClump;
            public int EstimatedBatches { get; }
            public int VisibleClusters { get; }
            public int MaterialBuckets { get; }
            public int RuntimeGeneratedInRender { get; }
            public int ShadowCasterInstances { get; }
            public double CullingMilliseconds { get; }
            public double BuildMilliseconds { get; }
        }

        private readonly struct GrassInstancePacked
        {
            public GrassInstancePacked(Vector3 position, ushort yaw, ushort packedScale, byte variant, byte tint, byte selector)
            {
                Position = position;
                Yaw = yaw;
                PackedScale = packedScale;
                Variant = variant;
                Tint = tint;
                Selector = selector;
            }

            public Vector3 Position { get; }
            public ushort Yaw { get; }
            public ushort PackedScale { get; }
            public byte Variant { get; }
            public byte Tint { get; }
            public byte Selector { get; }
            public float YawDegrees => Yaw * (360f / ushort.MaxValue);
            public float Scale => 0.45f + PackedScale * (0.95f / ushort.MaxValue);
            public float Selection01 => Selector / 255f;
        }

        private readonly struct GrassCluster
        {
            public GrassCluster(Vector3 center, Bounds bounds, int instanceStart, int instanceCount, float densitySummary)
            {
                Center = center;
                Bounds = bounds;
                InstanceStart = instanceStart;
                InstanceCount = instanceCount;
                DensitySummary = densitySummary;
            }

            public Vector3 Center { get; }
            public Bounds Bounds { get; }
            public int InstanceStart { get; }
            public int InstanceCount { get; }
            public float DensitySummary { get; }
        }

        private readonly struct VisibleCluster
        {
            public VisibleCluster(int clusterIndex, float sqrDistance, GrassRing ring)
            {
                ClusterIndex = clusterIndex;
                SqrDistance = sqrDistance;
                Ring = ring;
            }

            public int ClusterIndex { get; }
            public float SqrDistance { get; }
            public GrassRing Ring { get; }
        }

        private enum GrassRing
        {
            None,
            Near,
            Mid,
            Far
        }
    }
}
