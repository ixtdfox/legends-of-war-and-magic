using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
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
        private const int HardVisibleGrassTriangleLimit = 2500000;
        private const float GrassAnchorSampleRadius = 0.34f;
        private const float MaxGrassAnchorSlope = 26f;
        private const float MaxGrassAnchorHeightSpan = 0.16f;
        private const int MaxRuntimeInitialBuildsPerFrame = 1;
        private const int MaxConcurrentRuntimeBuilds = 1;
        private const int MaxClusterInstanceSchedulesPerFrame = 8;
        private const double RuntimeBuildFrameBudgetMilliseconds = 4d;

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
        private static readonly int AmbientId = Shader.PropertyToID("_Ambient");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static int runtimeInitialBuildFrame = -1;
        private static int runtimeInitialBuildCount;
        private static int activeRuntimeBuilds;

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
        private readonly Queue<int> pendingClusterInstanceBuilds = new();
        private readonly HashSet<int> pendingClusterInstanceBuildSet = new();
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
        private float[,,] sourceAlphamaps;
        private float[,] cachedHeights;
        private int densityResolution;
        private int alphamapWidth;
        private int alphamapHeight;
        private int alphamapLayers;
        private int cachedHeightResolution;
        private float cachedHeightStepX;
        private float cachedHeightStepZ;
        private int resolvedGrassSeed;
        private Bounds terrainBounds;
        private bool initialized;
        private bool terrainTintApplied;
        private bool runtimeInitializationQueued;
        private Coroutine runtimeInitializationRoutine;
        private Coroutine clusterInstanceBuildRoutine;
        private int runtimeBuildVersion;
        private int scheduledClusterInstanceBuildsThisFrame;
        private WorldGenerationMaskSet worldMasks;

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
        public Texture2D DensityDebugTexture
        {
            get
            {
                if (densityDebugTexture != null)
                {
                    return densityDebugTexture;
                }

                if ((densityValues.Length == 0 || densityResolution <= 0) &&
                    targetTerrain != null &&
                    targetTerrain.terrainData != null)
                {
                    using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.BuildDensityGridForDebugTexture"))
                    {
                        BuildDensityGrid();
                    }
                }

                if (densityValues.Length == 0 || densityResolution <= 0)
                {
                    return null;
                }

                var pixels = new Color[densityValues.Length];
                for (var i = 0; i < densityValues.Length; i++)
                {
                    var density = densityValues[i];
                    pixels[i] = new Color(density, density * 0.82f, density * 0.28f, 1f);
                }

                densityDebugTexture = new Texture2D(densityResolution, densityResolution, TextureFormat.RGBA32, false, true)
                {
                    name = "Generated Grass Density Debug",
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };
                densityDebugTexture.SetPixels(pixels);
                densityDebugTexture.Apply(false, false);
                return densityDebugTexture;
            }
        }
        public GpuGrassSettings RuntimeSettings => ResolvedGrassSettings;

        public void Initialize(Terrain terrain, int generationSeed, GpuGrassSettings settings, float terrainWaterLevel, WorldGenerationMaskSet masks = null)
        {
            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.Initialize", new
            {
                terrain = terrain != null ? terrain.name : string.Empty,
                generationSeed
            }))
            {
                ConfigureState(terrain, generationSeed, settings, terrainWaterLevel, masks);
                BuildRuntimeData();
            }
        }

        public void ConfigureLazy(Terrain terrain, int generationSeed, GpuGrassSettings settings, float terrainWaterLevel, WorldGenerationMaskSet masks = null)
        {
            ConfigureState(terrain, generationSeed, settings, terrainWaterLevel, masks);
            QueueRuntimeInitialization();
        }

        public void QueueRuntimeInitialization()
        {
            if (initialized || runtimeInitializationQueued || runtimeInitializationRoutine != null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                BuildRuntimeData();
                return;
            }

            runtimeInitializationQueued = true;
        }

        private void ConfigureState(Terrain terrain, int generationSeed, GpuGrassSettings settings, float terrainWaterLevel, WorldGenerationMaskSet masks)
        {
            if (terrainTintApplied)
            {
                RestoreTerrainDensityTint();
            }

            targetTerrain = terrain;
            seed = generationSeed;
            grassSettings = settings != null ? settings.Clone() : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            waterLevel = terrainWaterLevel;
            worldMasks = masks;
            resolvedGrassSeed = unchecked(seed + grassSettings.GrassSeed * 1009);
            terrainTintApplied = false;
            sourceAlphamaps = null;
            initialized = false;

            ReleaseRuntimeResources();
        }

        private bool BuildRuntimeData()
        {
            runtimeInitializationQueued = false;
            var stopwatch = Stopwatch.StartNew();
            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.CreateRuntimeResources"))
            {
                CreateRuntimeResources();
            }

            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.CacheTerrainData"))
            {
                CacheTerrainData();
            }

            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.ApplyTerrainDensityTint"))
            {
                ApplyTerrainDensityTint();
            }

            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.BuildClusterIndex"))
            {
                BuildClusterInstances();
            }
            stopwatch.Stop();

            return CompleteRuntimeDataBuild(stopwatch.Elapsed.TotalMilliseconds);
        }

        private IEnumerator BuildRuntimeDataRoutine(int buildVersion)
        {
            activeRuntimeBuilds++;
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.CreateRuntimeResources"))
                {
                    CreateRuntimeResources();
                }

                yield return null;
                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.CacheTerrainData"))
                {
                    CacheTerrainData();
                }

                yield return null;
                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.ApplyTerrainDensityTint"))
                {
                    ApplyTerrainDensityTint();
                }

                yield return null;
                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                yield return BuildClusterInstancesRoutine(buildVersion);
                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                stopwatch.Stop();
                CompleteRuntimeDataBuild(stopwatch.Elapsed.TotalMilliseconds);
            }
            finally
            {
                activeRuntimeBuilds = Mathf.Max(0, activeRuntimeBuilds - 1);
                if (runtimeBuildVersion == buildVersion)
                {
                    runtimeInitializationRoutine = null;
                }
            }
        }

        private bool CompleteRuntimeDataBuild(double elapsedMilliseconds)
        {
            LastBuildMilliseconds = elapsedMilliseconds;
            initialized = targetTerrain != null &&
                          targetTerrain.terrainData != null &&
                          clusters.Count > 0 &&
                          nearGrassMesh != null &&
                          midGrassMesh != null &&
                          grassMaterial != null;
            runtimeInitializationQueued = false;
            DebugSessionManager.Current?.Counters.Set("grass.generatedClusters", GeneratedClusterCount);
            DebugSessionManager.Current?.Counters.Set("grass.generatedClumps", GeneratedClumpCount);
            return initialized;
        }

        private bool CanContinueRuntimeBuild(int buildVersion)
        {
            return runtimeBuildVersion == buildVersion &&
                   isActiveAndEnabled &&
                   targetTerrain != null &&
                   targetTerrain.terrainData != null;
        }

        public void ApplyRuntimeSettings(GpuGrassSettings settings, bool rebuild)
        {
            grassSettings = settings != null
                ? settings.Clone()
                : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            resolvedGrassSeed = unchecked(seed + grassSettings.GrassSeed * 1009);

            if (rebuild)
            {
                RebuildRuntimeResources();
                return;
            }

            ApplyMaterialSettings();
            if (!grassSettings.EnableTerrainDensityTint)
            {
                RestoreTerrainDensityTint();
            }
            else
            {
                ApplyTerrainDensityTint();
            }
        }

        public void RebuildRuntimeResources()
        {
            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.RebuildRuntimeResources", new
            {
                terrain = targetTerrain != null ? targetTerrain.name : string.Empty
            }))
            {
                if (targetTerrain == null || targetTerrain.terrainData == null)
                {
                    initialized = false;
                    return;
                }

                ReleaseRuntimeResources();
                BuildRuntimeData();
            }
        }

        public void AddDiagnostics(Camera camera, IList<GpuGrassDiagnostic> diagnostics)
        {
            if (diagnostics == null || !EnsureInitialized(false))
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

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (runtimeInitializationQueued &&
                runtimeInitializationRoutine == null &&
                !initialized &&
                targetTerrain != null &&
                targetTerrain.terrainData != null &&
                activeRuntimeBuilds < MaxConcurrentRuntimeBuilds &&
                TryReserveRuntimeInitialBuild())
            {
                runtimeInitializationQueued = false;
                runtimeInitializationRoutine = StartCoroutine(BuildRuntimeDataRoutine(runtimeBuildVersion));
            }

            if (initialized &&
                clusterInstanceBuildRoutine == null &&
                pendingClusterInstanceBuilds.Count > 0)
            {
                clusterInstanceBuildRoutine = StartCoroutine(ProcessQueuedClusterInstanceBuildsRoutine(runtimeBuildVersion));
            }
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
            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.DrawForCamera", new
            {
                camera = camera != null ? camera.name : string.Empty,
                clusters = clusters.Count
            }))
            {
                if (camera == null || !isActiveAndEnabled || !EnsureInitialized(false))
                {
                    return;
                }

                PrepareVisibleInstances(camera);
                DrawInstances(camera, nearGrassMesh, visibleNearMatrices, visibleNearTints, visibleNearInstanceData, ResolvedGrassSettings.EnableGrassShadows);
                DrawInstances(camera, midGrassMesh, visibleMidMatrices, visibleMidTints, visibleMidInstanceData, false);
                DebugSessionManager.Current?.Counters.Set("grass.visibleNearInstances", LastVisibleNearInstances);
                DebugSessionManager.Current?.Counters.Set("grass.visibleMidInstances", LastVisibleMidInstances);
                DebugSessionManager.Current?.Counters.Set("grass.visibleTriangles", LastVisibleGrassTriangles);
            }
        }

        private bool EnsureInitialized(bool allowBuild = true)
        {
            if (initialized)
            {
                return true;
            }

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return false;
            }

            if (Application.isPlaying && !allowBuild)
            {
                return false;
            }

            if (grassSettings == null)
            {
                grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            }

            resolvedGrassSeed = unchecked(seed + grassSettings.GrassSeed * 1009);

            if (!TryReserveRuntimeInitialBuild())
            {
                return false;
            }

            return BuildRuntimeData();
        }

        private static bool TryReserveRuntimeInitialBuild()
        {
            if (!Application.isPlaying)
            {
                return true;
            }

            var frame = Time.frameCount;
            if (runtimeInitialBuildFrame != frame)
            {
                runtimeInitialBuildFrame = frame;
                runtimeInitialBuildCount = 0;
            }

            if (runtimeInitialBuildCount >= MaxRuntimeInitialBuildsPerFrame)
            {
                return false;
            }

            runtimeInitialBuildCount++;
            return true;
        }

        private void PrepareVisibleInstances(Camera camera)
        {
            ClearVisibleData();
            RuntimeGeneratedInRenderCount = 0;
            scheduledClusterInstanceBuildsThisFrame = 0;

            if (camera == null)
            {
                return;
            }

            var settings = ResolvedGrassSettings;
            var triangleBudget = ResolveTriangleBudget(settings);
            if (!settings.Enabled || !settings.UseOptimizedClusterRenderer || triangleBudget <= 0)
            {
                return;
            }

            var cameraPosition = camera.transform.position;
            var farDistance = settings.FarVisualDistance + settings.ClusterSize;
            var farSqrDistance = farDistance * farDistance;
            if (SqrDistanceXZ(terrainBounds, cameraPosition) > farSqrDistance)
            {
                return;
            }

            cullingStopwatch.Restart();

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
                    AddClusterInstances(visibleClusters[i].ClusterIndex, GrassRing.Near, cameraPosition, settings, triangleBudget);
                }
            }

            for (var i = 0; i < visibleClusters.Count; i++)
            {
                if (visibleClusters[i].Ring == GrassRing.Mid)
                {
                    AddClusterInstances(visibleClusters[i].ClusterIndex, GrassRing.Mid, cameraPosition, settings, triangleBudget);
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

        private GrassCluster EnsureClusterInstances(int clusterIndex, GpuGrassSettings settings)
        {
            if (clusterIndex < 0 || clusterIndex >= clusters.Count)
            {
                return default;
            }

            var cluster = clusters[clusterIndex];
            if (cluster.InstanceStart >= 0)
            {
                return cluster;
            }

            var start = packedInstances.Count;
            using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.GenerateClusterInstances", new
            {
                cluster.GridX,
                cluster.GridZ,
                cluster.MinX,
                cluster.MinZ,
                cluster.MaxX,
                cluster.MaxZ
            }))
            {
                GenerateClusterInstances(
                    cluster.GridX,
                    cluster.GridZ,
                    cluster.MinX,
                    cluster.MinZ,
                    cluster.MaxX,
                    cluster.MaxZ,
                    settings);
            }

            var count = packedInstances.Count - start;
            cluster = cluster.WithInstances(start, count);
            clusters[clusterIndex] = cluster;
            GeneratedClumpCount = packedInstances.Count;
            RuntimeGeneratedInRenderCount += count;
            DebugSessionManager.Current?.Counters.Set("grass.generatedClumps", GeneratedClumpCount);
            return cluster;
        }

        private void AddClusterInstances(
            int clusterIndex,
            GrassRing ring,
            Vector3 cameraPosition,
            GpuGrassSettings settings,
            int triangleBudget)
        {
            if (!TryGetGeneratedClusterInstances(clusterIndex, out var cluster))
            {
                return;
            }

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

        private bool TryGetGeneratedClusterInstances(int clusterIndex, out GrassCluster cluster)
        {
            cluster = default;
            if (clusterIndex < 0 || clusterIndex >= clusters.Count)
            {
                return false;
            }

            cluster = clusters[clusterIndex];
            if (cluster.InstanceStart >= 0)
            {
                return true;
            }

            ScheduleClusterInstanceBuild(clusterIndex);
            return false;
        }

        private void ScheduleClusterInstanceBuild(int clusterIndex)
        {
            if (clusterIndex < 0 ||
                clusterIndex >= clusters.Count ||
                pendingClusterInstanceBuildSet.Contains(clusterIndex) ||
                scheduledClusterInstanceBuildsThisFrame >= MaxClusterInstanceSchedulesPerFrame)
            {
                return;
            }

            pendingClusterInstanceBuilds.Enqueue(clusterIndex);
            pendingClusterInstanceBuildSet.Add(clusterIndex);
            scheduledClusterInstanceBuildsThisFrame++;
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

            var settings = ResolvedGrassSettings;
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
            cachedHeights = null;
            cachedHeightResolution = 0;
            cachedHeightStepX = 0f;
            cachedHeightStepZ = 0f;

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                terrainBounds = default;
                return;
            }

            var data = targetTerrain.terrainData;
            terrainBounds = new Bounds(
                targetTerrain.transform.position + data.size * 0.5f,
                data.size + new Vector3(0f, 4f, 0f));
            cachedHeightResolution = data.heightmapResolution;
            if (cachedHeightResolution > 1)
            {
                cachedHeights = data.GetHeights(0, 0, cachedHeightResolution, cachedHeightResolution);
                cachedHeightStepX = data.size.x / (cachedHeightResolution - 1);
                cachedHeightStepZ = data.size.z / (cachedHeightResolution - 1);
            }

            if (data.alphamapLayers <= 0 || data.alphamapWidth <= 0 || data.alphamapHeight <= 0)
            {
                return;
            }

            alphamapWidth = data.alphamapWidth;
            alphamapHeight = data.alphamapHeight;
            alphamapLayers = data.alphamapLayers;
            if (sourceAlphamaps == null ||
                sourceAlphamaps.GetLength(0) != alphamapHeight ||
                sourceAlphamaps.GetLength(1) != alphamapWidth ||
                sourceAlphamaps.GetLength(2) != alphamapLayers)
            {
                sourceAlphamaps = data.GetAlphamaps(0, 0, alphamapWidth, alphamapHeight);
            }

            alphamaps = CloneAlphamaps(sourceAlphamaps);
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

            var settings = ResolvedGrassSettings;
            densityResolution = settings.DensityGridResolution;
            densityValues = new float[densityResolution * densityResolution];

            for (var z = 0; z < densityResolution; z++)
            {
                for (var x = 0; x < densityResolution; x++)
                {
                    var nx = (x + 0.5f) / densityResolution;
                    var nz = (z + 0.5f) / densityResolution;
                    var density = ComputeDensity01(nx, nz, settings);
                    var index = z * densityResolution + x;
                    densityValues[index] = density;
                }
            }
        }

        private float ComputeDensity01(float nx, float nz, GpuGrassSettings settings)
        {
            var data = targetTerrain.terrainData;
            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = data.size;
            var normalizedHeight = SampleCachedHeight01(nx, nz);
            var slope = SampleCachedSlopeDegrees(nx, nz);
            var aboveWater = normalizedHeight - Mathf.Clamp01(waterLevel / Mathf.Max(1f, terrainSize.y));
            var worldX = terrainPosition.x + nx * terrainSize.x;
            var worldZ = terrainPosition.z + nz * terrainSize.z;

            var layerMask = SampleGrassLayer(nx, nz);
            if (worldMasks != null)
            {
                var worldPoint = new Vector2(worldX, worldZ);
                if (worldMasks.IsNoSpawn(worldPoint))
                {
                    return 0f;
                }

                layerMask *= 1f - worldMasks.Evaluate(worldPoint, GenerationZoneKind.ReducedVegetation) * 0.85f;
            }

            var slopeMask = 1f - SmoothRange(settings.SlopeFadeStart, settings.SlopeFadeEnd, slope);
            var waterMask = SmoothRange(settings.WaterFadeStart, settings.WaterFadeEnd, aboveWater);
            var heightMask = 1f - SmoothRange(settings.HeightFadeStart, settings.HeightFadeEnd, normalizedHeight);
            var macro = FbmNoise(worldX / settings.MacroNoiseScale, worldZ / settings.MacroNoiseScale, resolvedGrassSeed, settings);
            var micro = FbmNoise(worldX / settings.MicroNoiseScale, worldZ / settings.MicroNoiseScale, resolvedGrassSeed + 137, settings);
            var noise = RemapContrast(macro * 0.75f + micro * 0.25f, settings.NoiseContrast);
            var noiseMask = SmoothRange(settings.NoiseThresholdLow, settings.NoiseThresholdHigh, noise);

            return Mathf.Clamp01(layerMask * slopeMask * waterMask * heightMask * noiseMask);
        }

        private void ApplyTerrainDensityTint()
        {
            var settings = ResolvedGrassSettings;
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
                            value *= 1f + tint * settings.TerrainGrassBoost;
                        }
                        else if (layer == GrassVariationLayer)
                        {
                            value += tint * settings.TerrainVariationBoost;
                        }
                        else if (layer == RockLayer || layer == ShoreLayer)
                        {
                            value *= 1f - tint * settings.TerrainRockSuppression;
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

        private void RestoreTerrainDensityTint()
        {
            if (targetTerrain == null ||
                targetTerrain.terrainData == null ||
                sourceAlphamaps == null)
            {
                terrainTintApplied = false;
                return;
            }

            targetTerrain.terrainData.SetAlphamaps(0, 0, sourceAlphamaps);
            targetTerrain.Flush();
            alphamaps = CloneAlphamaps(sourceAlphamaps);
            terrainTintApplied = false;
        }

        private void BuildClusterInstances()
        {
            if (!TryBeginClusterBuild(
                    out var terrainPosition,
                    out var terrainSize,
                    out var clusterSize,
                    out var clusterCountX,
                    out var clusterCountZ))
            {
                return;
            }

            for (var z = 0; z < clusterCountZ; z++)
            {
                for (var x = 0; x < clusterCountX; x++)
                {
                    AddClusterIfDense(x, z, terrainPosition, terrainSize, clusterSize);
                }
            }

            GeneratedChunkCount = clusters.Count;
            GeneratedClusterCount = clusters.Count;
            GeneratedClumpCount = 0;
        }

        private IEnumerator BuildClusterInstancesRoutine(int buildVersion)
        {
            if (!TryBeginClusterBuild(
                    out var terrainPosition,
                    out var terrainSize,
                    out var clusterSize,
                    out var clusterCountX,
                    out var clusterCountZ))
            {
                yield break;
            }

            var total = clusterCountX * clusterCountZ;
            var index = 0;
            while (index < total)
            {
                using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.BuildClusterIndexSlice", new
                {
                    start = index,
                    total,
                    existingClusters = clusters.Count
                }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (index < total)
                    {
                        var x = index % clusterCountX;
                        var z = index / clusterCountX;
                        AddClusterIfDense(x, z, terrainPosition, terrainSize, clusterSize);
                        index++;

                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeBuildFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    yield break;
                }

                if (index < total)
                {
                    yield return null;
                }
            }

            GeneratedChunkCount = clusters.Count;
            GeneratedClusterCount = clusters.Count;
            GeneratedClumpCount = 0;
        }

        private bool TryBeginClusterBuild(
            out Vector3 terrainPosition,
            out Vector3 terrainSize,
            out float clusterSize,
            out int clusterCountX,
            out int clusterCountZ)
        {
            clusters.Clear();
            packedInstances.Clear();
            pendingClusterInstanceBuilds.Clear();
            pendingClusterInstanceBuildSet.Clear();
            GeneratedChunkCount = 0;
            GeneratedClusterCount = 0;
            GeneratedClumpCount = 0;

            terrainPosition = default;
            terrainSize = default;
            clusterSize = 1f;
            clusterCountX = 0;
            clusterCountZ = 0;

            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return false;
            }

            var settings = ResolvedGrassSettings;
            var data = targetTerrain.terrainData;
            terrainPosition = targetTerrain.transform.position;
            terrainSize = data.size;
            clusterSize = settings.ClusterSize;
            clusterCountX = Mathf.CeilToInt(terrainSize.x / clusterSize);
            clusterCountZ = Mathf.CeilToInt(terrainSize.z / clusterSize);
            return clusterCountX > 0 && clusterCountZ > 0;
        }

        private void AddClusterIfDense(int x, int z, Vector3 terrainPosition, Vector3 terrainSize, float clusterSize)
        {
            var minX = terrainPosition.x + x * clusterSize;
            var minZ = terrainPosition.z + z * clusterSize;
            var maxX = Mathf.Min(terrainPosition.x + terrainSize.x, minX + clusterSize);
            var maxZ = Mathf.Min(terrainPosition.z + terrainSize.z, minZ + clusterSize);
            var center = new Vector3((minX + maxX) * 0.5f, terrainPosition.y, (minZ + maxZ) * 0.5f);
            var densitySummary = SampleClusterDensity(minX, minZ, maxX, maxZ);
            if (densitySummary <= 0.015f)
            {
                return;
            }

            var bounds = new Bounds(
                new Vector3(center.x, terrainPosition.y + terrainSize.y * 0.5f, center.z),
                new Vector3(Mathf.Max(0.1f, maxX - minX), terrainSize.y + 3f, Mathf.Max(0.1f, maxZ - minZ)));
            clusters.Add(new GrassCluster(
                center,
                bounds,
                x,
                z,
                minX,
                minZ,
                maxX,
                maxZ,
                -1,
                0,
                densitySummary));
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
            if (!TryCreateClusterGenerationContext(
                    clusterX,
                    clusterZ,
                    minX,
                    minZ,
                    maxX,
                    maxZ,
                    settings,
                    out var context))
            {
                return;
            }

            for (var z = 0; z < context.CellsZ; z++)
            {
                for (var x = 0; x < context.CellsX; x++)
                {
                    TryAddClusterInstanceCell(context, x, z);
                }
            }
        }

        private IEnumerator ProcessQueuedClusterInstanceBuildsRoutine(int buildVersion)
        {
            try
            {
                while (CanContinueRuntimeBuild(buildVersion) && pendingClusterInstanceBuilds.Count > 0)
                {
                    var clusterIndex = pendingClusterInstanceBuilds.Dequeue();
                    pendingClusterInstanceBuildSet.Remove(clusterIndex);
                    if (clusterIndex < 0 || clusterIndex >= clusters.Count)
                    {
                        continue;
                    }

                    var cluster = clusters[clusterIndex];
                    if (cluster.InstanceStart >= 0)
                    {
                        continue;
                    }

                    yield return GenerateClusterInstancesRoutine(clusterIndex, buildVersion);
                }
            }
            finally
            {
                if (runtimeBuildVersion == buildVersion)
                {
                    clusterInstanceBuildRoutine = null;
                }
            }
        }

        private IEnumerator GenerateClusterInstancesRoutine(int clusterIndex, int buildVersion)
        {
            if (clusterIndex < 0 || clusterIndex >= clusters.Count)
            {
                yield break;
            }

            var cluster = clusters[clusterIndex];
            if (cluster.InstanceStart >= 0 ||
                !TryCreateClusterGenerationContext(
                    cluster.GridX,
                    cluster.GridZ,
                    cluster.MinX,
                    cluster.MinZ,
                    cluster.MaxX,
                    cluster.MaxZ,
                    ResolvedGrassSettings,
                    out var context))
            {
                yield break;
            }

            var start = packedInstances.Count;
            var total = context.CellsX * context.CellsZ;
            var index = 0;
            while (index < total)
            {
                using (DebugSessionManager.Profiler.Scope("GpuGrassRenderer.GenerateClusterInstancesSlice", new
                {
                    cluster.GridX,
                    cluster.GridZ,
                    start = index,
                    total
                }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (index < total)
                    {
                        var x = index % context.CellsX;
                        var z = index / context.CellsX;
                        TryAddClusterInstanceCell(context, x, z);
                        index++;

                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeBuildFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (!CanContinueRuntimeBuild(buildVersion))
                {
                    if (packedInstances.Count > start)
                    {
                        packedInstances.RemoveRange(start, packedInstances.Count - start);
                    }

                    yield break;
                }

                if (index < total)
                {
                    yield return null;
                }
            }

            var count = packedInstances.Count - start;
            if (clusterIndex >= 0 && clusterIndex < clusters.Count && clusters[clusterIndex].InstanceStart < 0)
            {
                clusters[clusterIndex] = clusters[clusterIndex].WithInstances(start, count);
            }

            GeneratedClumpCount = packedInstances.Count;
            DebugSessionManager.Current?.Counters.Set("grass.generatedClumps", GeneratedClumpCount);
        }

        private bool TryCreateClusterGenerationContext(
            int clusterX,
            int clusterZ,
            float minX,
            float minZ,
            float maxX,
            float maxZ,
            GpuGrassSettings settings,
            out ClusterGenerationContext context)
        {
            context = default;
            if (targetTerrain == null || targetTerrain.terrainData == null || settings == null)
            {
                return false;
            }

            var spacing = Mathf.Max(0.01f, settings.PlacementSpacing);
            var cellsX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / spacing));
            var cellsZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / spacing));
            context = new ClusterGenerationContext(
                clusterX,
                clusterZ,
                minX,
                minZ,
                maxX,
                maxZ,
                cellsX,
                cellsZ,
                (maxX - minX) / cellsX,
                (maxZ - minZ) / cellsZ,
                settings);
            return true;
        }

        private void TryAddClusterInstanceCell(ClusterGenerationContext context, int x, int z)
        {
            var saltX = context.ClusterX * 73856093 + x;
            var saltZ = context.ClusterZ * 19349663 + z;
            var jitterX = Hash01(resolvedGrassSeed, saltX, saltZ, 11);
            var jitterZ = Hash01(resolvedGrassSeed, saltX, saltZ, 23);
            var worldX = context.MinX + (x + jitterX) * context.CellWidth;
            var worldZ = context.MinZ + (z + jitterZ) * context.CellDepth;
            if (worldX >= context.MaxX || worldZ >= context.MaxZ)
            {
                return;
            }

            var settings = context.Settings;
            var density = Mathf.Clamp01(SampleDensityWorld(worldX, worldZ) * settings.DensityMultiplier);
            if (density <= 0.02f || Hash01(resolvedGrassSeed, saltX, saltZ, 29) > density)
            {
                return;
            }

            if (!TryResolveStableGrassAnchor(worldX, worldZ, out var height) || height <= waterLevel + 0.08f)
            {
                return;
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

        private bool TryResolveStableGrassAnchor(float worldX, float worldZ, out float height)
        {
            height = 0f;
            if (targetTerrain == null || targetTerrain.terrainData == null)
            {
                return false;
            }

            var terrainPosition = targetTerrain.transform.position;
            var terrainSize = targetTerrain.terrainData.size;
            var nx = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, worldX);
            var nz = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, worldZ);
            if (nx < 0f || nx > 1f || nz < 0f || nz > 1f)
            {
                return false;
            }

            if (SampleCachedSlopeDegrees(nx, nz) > MaxGrassAnchorSlope)
            {
                return false;
            }

            height = SampleCachedHeightMeters(nx, nz) + terrainPosition.y;
            var minHeight = height;
            var maxHeight = height;
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * 0.25f;
                var sampleX = worldX + Mathf.Cos(angle) * GrassAnchorSampleRadius;
                var sampleZ = worldZ + Mathf.Sin(angle) * GrassAnchorSampleRadius;
                var sx = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + terrainSize.x, sampleX);
                var sz = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + terrainSize.z, sampleZ);
                if (sx < 0f || sx > 1f || sz < 0f || sz > 1f)
                {
                    continue;
                }

                var sampleHeight = SampleCachedHeightMeters(sx, sz) + terrainPosition.y;
                minHeight = Mathf.Min(minHeight, sampleHeight);
                maxHeight = Mathf.Max(maxHeight, sampleHeight);
            }

            return maxHeight - minHeight <= MaxGrassAnchorHeightSpan;
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
            if (targetTerrain == null || targetTerrain.terrainData == null)
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
            return ComputeDensity01(Mathf.Clamp01(nx), Mathf.Clamp01(nz), ResolvedGrassSettings);
        }

        private float SampleGrassLayer(float nx, float nz)
        {
            var source = sourceAlphamaps ?? alphamaps;
            if (source == null || alphamapLayers == 0)
            {
                return 1f;
            }

            var x = Mathf.Clamp(Mathf.RoundToInt(nx * (alphamapWidth - 1)), 0, alphamapWidth - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(nz * (alphamapHeight - 1)), 0, alphamapHeight - 1);
            var grass = alphamapLayers > GrassLayer ? source[z, x, GrassLayer] : 1f;
            var settings = ResolvedGrassSettings;
            var variation = alphamapLayers > GrassVariationLayer ? source[z, x, GrassVariationLayer] : 0f;
            var shore = alphamapLayers > ShoreLayer ? source[z, x, ShoreLayer] : 0f;
            var rock = alphamapLayers > RockLayer ? source[z, x, RockLayer] : 0f;
            return Mathf.Clamp01((grass + variation * settings.GrassVariationLayerWeight) *
                                 (1f - shore * settings.ShoreSuppression) *
                                 (1f - rock * settings.RockSuppression));
        }

        private float SampleCachedHeight01(float nx, float nz)
        {
            if (cachedHeights == null || cachedHeightResolution <= 1)
            {
                return targetTerrain != null && targetTerrain.terrainData != null
                    ? targetTerrain.terrainData.GetInterpolatedHeight(nx, nz) / Mathf.Max(1f, targetTerrain.terrainData.size.y)
                    : 0f;
            }

            var px = Mathf.Clamp01(nx) * (cachedHeightResolution - 1);
            var pz = Mathf.Clamp01(nz) * (cachedHeightResolution - 1);
            var x0 = Mathf.Clamp(Mathf.FloorToInt(px), 0, cachedHeightResolution - 1);
            var z0 = Mathf.Clamp(Mathf.FloorToInt(pz), 0, cachedHeightResolution - 1);
            var x1 = Mathf.Min(x0 + 1, cachedHeightResolution - 1);
            var z1 = Mathf.Min(z0 + 1, cachedHeightResolution - 1);
            var tx = px - x0;
            var tz = pz - z0;
            var a = cachedHeights[z0, x0];
            var b = cachedHeights[z0, x1];
            var c = cachedHeights[z1, x0];
            var d = cachedHeights[z1, x1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private float SampleCachedHeightMeters(float nx, float nz)
        {
            var terrainHeight = targetTerrain != null && targetTerrain.terrainData != null
                ? targetTerrain.terrainData.size.y
                : 0f;
            return SampleCachedHeight01(nx, nz) * terrainHeight;
        }

        private float SampleCachedSlopeDegrees(float nx, float nz)
        {
            if (cachedHeights == null || cachedHeightResolution <= 2)
            {
                return targetTerrain != null && targetTerrain.terrainData != null
                    ? Vector3.Angle(targetTerrain.terrainData.GetInterpolatedNormal(nx, nz), Vector3.up)
                    : 90f;
            }

            var px = Mathf.Clamp01(nx) * (cachedHeightResolution - 1);
            var pz = Mathf.Clamp01(nz) * (cachedHeightResolution - 1);
            var x = Mathf.Clamp(Mathf.RoundToInt(px), 0, cachedHeightResolution - 1);
            var z = Mathf.Clamp(Mathf.RoundToInt(pz), 0, cachedHeightResolution - 1);
            var leftX = Mathf.Max(0, x - 1);
            var rightX = Mathf.Min(cachedHeightResolution - 1, x + 1);
            var downZ = Mathf.Max(0, z - 1);
            var upZ = Mathf.Min(cachedHeightResolution - 1, z + 1);
            var terrainHeight = targetTerrain != null && targetTerrain.terrainData != null
                ? targetTerrain.terrainData.size.y
                : 0f;
            var left = cachedHeights[z, leftX] * terrainHeight;
            var right = cachedHeights[z, rightX] * terrainHeight;
            var down = cachedHeights[downZ, x] * terrainHeight;
            var up = cachedHeights[upZ, x] * terrainHeight;
            var horizontal = Mathf.Max(
                0.001f,
                (rightX - leftX) * cachedHeightStepX + (upZ - downZ) * cachedHeightStepZ);
            var normal = new Vector3(left - right, horizontal, down - up).normalized;
            return Vector3.Angle(normal, Vector3.up);
        }

        private void CreateRuntimeResources()
        {
            var settings = ResolvedGrassSettings;
            nearGrassMesh ??= CreateGrassMesh("Generated GPU Grass Near Cards", settings.NearBladeCount);
            midGrassMesh ??= CreateGrassMesh("Generated GPU Grass Mid Cards", settings.MidBladeCount);
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
            SetColorIfPresent(material, BottomColorId, new Color(0.24f, 0.33f, 0.12f, 1f));
            SetColorIfPresent(material, TopColorId, new Color(0.58f, 0.64f, 0.34f, 1f));
            SetFloatIfPresent(material, AmbientId, 0.92f);
            SetFloatIfPresent(material, CutoffId, 0.045f);
            return material;
        }

        private void ApplyMaterialSettings()
        {
            if (grassMaterial == null)
            {
                return;
            }

            var settings = ResolvedGrassSettings;
            SetFloatIfPresent(grassMaterial, WindStrengthId, settings.WindStrength);
            SetFloatIfPresent(grassMaterial, WindSpeedId, settings.WindSpeed);
            SetFloatIfPresent(grassMaterial, WindScaleId, settings.WindScale);
            SetFloatIfPresent(grassMaterial, AmbientId, 0.92f);
            SetFloatIfPresent(grassMaterial, AtlasColumnsId, settings.AtlasColumns);
            SetFloatIfPresent(grassMaterial, AtlasRowsId, settings.AtlasRows);
        }

        private static Mesh CreateGrassMesh(string meshName, int bladeCount)
        {
            var isMidMesh = meshName.IndexOf("Mid", StringComparison.OrdinalIgnoreCase) >= 0;
            var vertices = new List<Vector3>(bladeCount * 4);
            var normals = new List<Vector3>(bladeCount * 4);
            var uvs = new List<Vector2>(bladeCount * 4);
            var triangles = new List<int>(bladeCount * 6);

            for (var i = 0; i < bladeCount; i++)
            {
                var angle = (360f / bladeCount) * i + (i % 2) * 13f;
                var height = isMidMesh
                    ? Mathf.Lerp(0.18f, 0.36f, Hash01(97, i, bladeCount, 13))
                    : Mathf.Lerp(0.22f, 0.48f, Hash01(97, i, bladeCount, 13));
                var width = isMidMesh
                    ? Mathf.Lerp(0.010f, 0.028f, Hash01(101, i, bladeCount, 29))
                    : Mathf.Lerp(0.014f, 0.038f, Hash01(101, i, bladeCount, 29));
                var scatter = isMidMesh ? 0.20f : 0.28f;
                var offset = new Vector3(
                    (Hash01(103, i, bladeCount, 31) - 0.5f) * scatter,
                    0f,
                    (Hash01(107, i, bladeCount, 37) - 0.5f) * scatter);
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
            var bend = rotation * new Vector3(0f, 0f, height * 0.12f + width * 0.2f);
            var right = rotation * Vector3.right;
            var normal = rotation * Vector3.forward;
            var start = vertices.Count;

            vertices.Add(offset - right * width * 0.62f);
            vertices.Add(offset + right * width * 0.62f);
            vertices.Add(offset - right * width * 0.035f + Vector3.up * height + bend);
            vertices.Add(offset + right * width * 0.035f + Vector3.up * height + bend);

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
            runtimeBuildVersion++;
            runtimeInitializationQueued = false;
            runtimeInitializationRoutine = null;
            clusterInstanceBuildRoutine = null;
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
            densityResolution = 0;
            cachedHeights = null;
            cachedHeightResolution = 0;
            cachedHeightStepX = 0f;
            cachedHeightStepZ = 0f;
            initialized = false;
            clusters.Clear();
            packedInstances.Clear();
            pendingClusterInstanceBuilds.Clear();
            pendingClusterInstanceBuildSet.Clear();
            visibleClusters.Clear();
            ClearVisibleData();
        }

        private static float[,,] CloneAlphamaps(float[,,] source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new float[source.GetLength(0), source.GetLength(1), source.GetLength(2)];
            Array.Copy(source, clone, source.Length);
            return clone;
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

        private GpuGrassSettings ResolvedGrassSettings
        {
            get
            {
                if (grassSettings == null)
                {
                    grassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
                }

                return grassSettings;
            }
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

        private static float SqrDistanceXZ(Bounds bounds, Vector3 point)
        {
            var closestX = Mathf.Clamp(point.x, bounds.min.x, bounds.max.x);
            var closestZ = Mathf.Clamp(point.z, bounds.min.z, bounds.max.z);
            var dx = point.x - closestX;
            var dz = point.z - closestZ;
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
            var dry = new Color(0.66f, 0.60f, 0.34f, 1f);
            var lush = new Color(0.56f, 0.64f, 0.31f, 1f);
            var shade = new Color(0.40f, 0.52f, 0.21f, 1f);
            var color = t < 0.5f
                ? Color.Lerp(dry, shade, t * 2f)
                : Color.Lerp(shade, lush, (t - 0.5f) * 2f);
            return new Vector4(color.r, color.g, color.b, 0.18f);
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

        private readonly struct ClusterGenerationContext
        {
            public ClusterGenerationContext(
                int clusterX,
                int clusterZ,
                float minX,
                float minZ,
                float maxX,
                float maxZ,
                int cellsX,
                int cellsZ,
                float cellWidth,
                float cellDepth,
                GpuGrassSettings settings)
            {
                ClusterX = clusterX;
                ClusterZ = clusterZ;
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
                CellsX = cellsX;
                CellsZ = cellsZ;
                CellWidth = cellWidth;
                CellDepth = cellDepth;
                Settings = settings;
            }

            public int ClusterX { get; }
            public int ClusterZ { get; }
            public float MinX { get; }
            public float MinZ { get; }
            public float MaxX { get; }
            public float MaxZ { get; }
            public int CellsX { get; }
            public int CellsZ { get; }
            public float CellWidth { get; }
            public float CellDepth { get; }
            public GpuGrassSettings Settings { get; }
        }

        private readonly struct GrassCluster
        {
            public GrassCluster(
                Vector3 center,
                Bounds bounds,
                int gridX,
                int gridZ,
                float minX,
                float minZ,
                float maxX,
                float maxZ,
                int instanceStart,
                int instanceCount,
                float densitySummary)
            {
                Center = center;
                Bounds = bounds;
                GridX = gridX;
                GridZ = gridZ;
                MinX = minX;
                MinZ = minZ;
                MaxX = maxX;
                MaxZ = maxZ;
                InstanceStart = instanceStart;
                InstanceCount = instanceCount;
                DensitySummary = densitySummary;
            }

            public Vector3 Center { get; }
            public Bounds Bounds { get; }
            public int GridX { get; }
            public int GridZ { get; }
            public float MinX { get; }
            public float MinZ { get; }
            public float MaxX { get; }
            public float MaxZ { get; }
            public int InstanceStart { get; }
            public int InstanceCount { get; }
            public float DensitySummary { get; }

            public GrassCluster WithInstances(int instanceStart, int instanceCount)
            {
                return new GrassCluster(
                    Center,
                    Bounds,
                    GridX,
                    GridZ,
                    MinX,
                    MinZ,
                    MaxX,
                    MaxZ,
                    instanceStart,
                    instanceCount,
                    DensitySummary);
            }
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
