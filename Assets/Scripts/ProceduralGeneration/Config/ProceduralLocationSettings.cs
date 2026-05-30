using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    /// <summary>
    /// Determines how the generator seed is chosen.
    /// </summary>
    public enum SeedMode
    {
        Random,
        Fixed
    }

    /// <summary>
    /// High-level landmass shaping mode used by player-facing map presets.
    /// </summary>
    public enum TerrainLandShape
    {
        Mainland,
        Islands,
        Archipelago
    }

    /// <summary>
    /// Reusable preset asset for procedural location generation.
    /// Create multiple assets to support different location styles.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProceduralLocationSettings",
        menuName = "Legends of War and Magic/Procedural Generation/Location Settings",
        order = 0)]
    public sealed class ProceduralLocationSettings : ScriptableObject
    {
        public const float DefaultLocationSizeMeters = 4000f;

        [Serializable]
        public sealed class GlobalGenerationSettings
        {
            [Header("World Bounds (meters)")]
            [Min(1f)]
            [SerializeField] private float worldWidth = DefaultLocationSizeMeters;

            [Min(1f)]
            [SerializeField] private float worldLength = DefaultLocationSizeMeters;

            public float WorldWidth => worldWidth;
            public float WorldLength => worldLength;

            public void Configure(float width, float length)
            {
                worldWidth = Mathf.Max(1f, width);
                worldLength = Mathf.Max(1f, length);
            }
        }

        [Serializable]
        public sealed class SeedGenerationSettings
        {
            [SerializeField] private SeedMode seedMode = SeedMode.Random;
            [SerializeField] private int fixedSeed = 12345;

            public SeedMode SeedMode => seedMode;
            public int FixedSeed => fixedSeed;

            public void Configure(SeedMode mode, int seed)
            {
                seedMode = mode;
                fixedSeed = seed;
            }
        }

        [Serializable]
        public sealed class TerrainGenerationSettings
        {
            [Min(33)]
            [SerializeField] private int heightmapResolution = 1025;

            [Min(1f)]
            [SerializeField] private float terrainHeight = 220f;

            [Min(0.001f)]
            [SerializeField] private float noiseScale = 320f;

            [Min(1)]
            [SerializeField] private int octaves = 5;

            [Range(0f, 1f)]
            [SerializeField] private float persistence = 0.45f;

            [Min(1f)]
            [SerializeField] private float lacunarity = 2f;

            [Min(0f)]
            [SerializeField] private float heightMultiplier = 1f;

            [SerializeField] private TerrainLandShape landShape = TerrainLandShape.Mainland;

            [Header("Natural Features")]
            [Range(0f, 1.5f)]
            [SerializeField] private float ridgeIntensity = 0.35f;

            [Range(0f, 1.5f)]
            [SerializeField] private float valleyIntensity = 0.25f;

            [Range(0f, 1.5f)]
            [SerializeField] private float cliffIntensity = 0.25f;

            [Range(0f, 1f)]
            [SerializeField] private float terraceStrength = 0.08f;

            [Range(0f, 1f)]
            [SerializeField] private float microReliefStrength = 0.12f;

            [Header("Edge Smoothing")]
            [SerializeField] private bool useEdgeFalloff = true;

            [Range(0f, 1f)]
            [SerializeField] private float edgeFalloffStart = 0.82f;

            [Range(0.1f, 10f)]
            [SerializeField] private float edgeFalloffStrength = 2.2f;

            public int HeightmapResolution => heightmapResolution;
            public float TerrainHeight => terrainHeight;
            public float NoiseScale => noiseScale;
            public int Octaves => octaves;
            public float Persistence => persistence;
            public float Lacunarity => lacunarity;
            public float HeightMultiplier => heightMultiplier;
            public TerrainLandShape LandShape => landShape;
            public float RidgeIntensity => ridgeIntensity;
            public float ValleyIntensity => valleyIntensity;
            public float CliffIntensity => cliffIntensity;
            public float TerraceStrength => terraceStrength;
            public float MicroReliefStrength => microReliefStrength;
            public bool UseEdgeFalloff => useEdgeFalloff;
            public float EdgeFalloffStart => edgeFalloffStart;
            public float EdgeFalloffStrength => edgeFalloffStrength;

            public void Configure(
                int resolution,
                float height,
                float scale,
                int octaveCount,
                float persistenceValue,
                float lacunarityValue,
                float multiplier,
                TerrainLandShape shape,
                float ridges,
                float valleys,
                float cliffs,
                float terraces,
                float microRelief,
                bool edgeFalloff,
                float falloffStart,
                float falloffStrength)
            {
                heightmapResolution = Mathf.Max(33, resolution);
                terrainHeight = Mathf.Max(1f, height);
                noiseScale = Mathf.Max(0.001f, scale);
                octaves = Mathf.Max(1, octaveCount);
                persistence = Mathf.Clamp01(persistenceValue);
                lacunarity = Mathf.Max(1f, lacunarityValue);
                heightMultiplier = Mathf.Max(0f, multiplier);
                landShape = shape;
                ridgeIntensity = Mathf.Clamp(ridges, 0f, 1.5f);
                valleyIntensity = Mathf.Clamp(valleys, 0f, 1.5f);
                cliffIntensity = Mathf.Clamp(cliffs, 0f, 1.5f);
                terraceStrength = Mathf.Clamp01(terraces);
                microReliefStrength = Mathf.Clamp01(microRelief);
                useEdgeFalloff = edgeFalloff;
                edgeFalloffStart = Mathf.Clamp01(falloffStart);
                edgeFalloffStrength = Mathf.Clamp(falloffStrength, 0.1f, 10f);
            }
        }

        [Serializable]
        public sealed class TerrainChunkGenerationSettings
        {
            [SerializeField] private bool enabled = true;

            [Tooltip("World-space chunk size in meters. 500m creates an 8x8 grid for a 4x4 km location.")]
            [Min(32f)]
            [SerializeField] private float chunkSize = 500f;

            [Tooltip("Heightmap resolution per chunk. Use power-of-two plus one values for Unity Terrain.")]
            [Min(33)]
            [SerializeField] private int heightmapResolution = 129;

            [Tooltip("How many chunks around the player remain loaded.")]
            [Min(0)]
            [SerializeField] private int loadRadiusChunks = 3;

            [Tooltip("Extra ring kept loaded before unloading to prevent churn on chunk boundaries.")]
            [Min(0)]
            [SerializeField] private int unloadBufferChunks = 1;

            [Tooltip("Chunks loaded synchronously around the origin before the player exists.")]
            [Min(0)]
            [SerializeField] private int initialLoadRadiusChunks = 0;

            [Tooltip("Chunks around the spawn point loaded across frames before gameplay starts.")]
            [Min(0)]
            [SerializeField] private int bootstrapPreloadRadiusChunks = 3;

            [Tooltip("Maximum terrain chunks built per gameplay frame.")]
            [Min(1)]
            [SerializeField] private int maxChunkBuildsPerFrame = 1;

            [Tooltip("Maximum old terrain chunks destroyed per gameplay frame.")]
            [Min(1)]
            [SerializeField] private int maxChunkUnloadsPerFrame = 4;

            [Tooltip("Terrain heightmap pixel errors for near, mid, and far streamed chunks.")]
            [SerializeField] private Vector3 lodPixelErrors = new(4f, 14f, 34f);

            public bool Enabled => enabled;
            public float ChunkSize => chunkSize;
            public int HeightmapResolution => heightmapResolution;
            public int LoadRadiusChunks => loadRadiusChunks;
            public int UnloadBufferChunks => unloadBufferChunks;
            public int InitialLoadRadiusChunks => initialLoadRadiusChunks;
            public int BootstrapPreloadRadiusChunks => bootstrapPreloadRadiusChunks;
            public int MaxChunkBuildsPerFrame => maxChunkBuildsPerFrame;
            public int MaxChunkUnloadsPerFrame => maxChunkUnloadsPerFrame;
            public Vector3 LodPixelErrors => lodPixelErrors;

            public void Configure(
                bool isEnabled,
                float size,
                int resolution,
                int loadRadius,
                int unloadBuffer,
                int initialLoadRadius,
                int bootstrapPreloadRadius,
                int chunkBuildsPerFrame,
                int chunkUnloadsPerFrame,
                Vector3 pixelErrors)
            {
                enabled = isEnabled;
                chunkSize = Mathf.Max(32f, size);
                heightmapResolution = Mathf.Max(33, resolution);
                loadRadiusChunks = Mathf.Max(0, loadRadius);
                unloadBufferChunks = Mathf.Max(0, unloadBuffer);
                initialLoadRadiusChunks = Mathf.Max(0, initialLoadRadius);
                bootstrapPreloadRadiusChunks = Mathf.Max(0, bootstrapPreloadRadius);
                maxChunkBuildsPerFrame = Mathf.Max(1, chunkBuildsPerFrame);
                maxChunkUnloadsPerFrame = Mathf.Max(1, chunkUnloadsPerFrame);
                lodPixelErrors = new Vector3(
                    Mathf.Max(1f, pixelErrors.x),
                    Mathf.Max(1f, pixelErrors.y),
                    Mathf.Max(1f, pixelErrors.z));
            }
        }

        [Serializable]
        public sealed class PropPlacementGenerationSettings
        {
            [SerializeField] private bool enablePropPlacement = true;
            [SerializeField] private List<PropCategoryPlacementSettings> propCategories = new();

            public bool EnablePropPlacement => enablePropPlacement;
            public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => propCategories;

            public void Configure(bool enabled, IEnumerable<PropCategoryPlacementSettings> categories)
            {
                enablePropPlacement = enabled;
                propCategories = categories == null
                    ? new List<PropCategoryPlacementSettings>()
                    : new List<PropCategoryPlacementSettings>(categories);
            }
        }

        [Serializable]
        public sealed class WaterGenerationSettings
        {
            [SerializeField] private bool enabled = true;

            [Tooltip("World-space height of the water plane.")]
            [Min(0f)]
            [SerializeField] private float waterLevel = 18f;

            [Tooltip("Extra water plane padding beyond generated terrain bounds.")]
            [Min(0f)]
            [SerializeField] private float planePadding = 20f;

            [SerializeField] private Color waterColor = new(0.1f, 0.36f, 0.58f, 0.76f);

            public bool Enabled => enabled;
            public float WaterLevel => waterLevel;
            public float PlanePadding => planePadding;
            public Color WaterColor => waterColor;

            public void Configure(bool isEnabled, float level, float padding, Color color)
            {
                enabled = isEnabled;
                waterLevel = Mathf.Max(0f, level);
                planePadding = Mathf.Max(0f, padding);
                waterColor = color;
            }
        }

        [Header("Global")]
        [SerializeField] private GlobalGenerationSettings global = new();

        [Header("Seed")]
        [SerializeField] private SeedGenerationSettings seed = new();

        [Header("Terrain")]
        [SerializeField] private TerrainGenerationSettings terrain = new();

        [Header("Terrain Chunks")]
        [SerializeField] private TerrainChunkGenerationSettings terrainChunks = new();

        [Header("Props")]
        [SerializeField] private PropPlacementGenerationSettings props = new();

        [Serializable]
        public sealed class TerrainDetailGenerationSettings
        {
            [SerializeField] private bool enabled = true;

            [Range(0f, 8f)]
            [SerializeField] private float densityMultiplier = 1f;

            [Min(0)]
            [SerializeField] private int detailResolution = 512;

            [Min(8)]
            [SerializeField] private int detailResolutionPerPatch = 16;

            public bool Enabled => enabled;
            public float DensityMultiplier => densityMultiplier;
            public int DetailResolution => detailResolution;
            public int DetailResolutionPerPatch => detailResolutionPerPatch;

            public void Configure(bool isEnabled, float multiplier, int resolution, int resolutionPerPatch)
            {
                enabled = isEnabled;
                densityMultiplier = Mathf.Clamp(multiplier, 0f, 8f);
                detailResolution = Mathf.Max(0, resolution);
                detailResolutionPerPatch = Mathf.Max(8, resolutionPerPatch);
            }
        }

        [Header("Water")]
        [SerializeField] private WaterGenerationSettings water = new();

        [Header("Terrain Details")]
        [SerializeField] private TerrainDetailGenerationSettings terrainDetails = new();

        [Header("Forest Rendering")]
        [SerializeField] private ForestRenderingSettings forestRendering = new();

        [Header("Environment Assets")]
        [SerializeField] private ProceduralEnvironmentAssetCatalog assetCatalog;

        [Header("Settlements, Roads & POI")]
        [SerializeField] private SettlementGenerationConfig settlements = new();
        [SerializeField] private PointOfInterestGenerationConfig pointsOfInterest = new();
        [SerializeField] private RoadGenerationConfig roads = new();

        public GlobalGenerationSettings Global => global;
        public SeedGenerationSettings Seed => seed;
        public TerrainGenerationSettings Terrain => terrain;
        public TerrainChunkGenerationSettings TerrainChunks => terrainChunks;
        public PropPlacementGenerationSettings Props => props;
        public WaterGenerationSettings Water => water;
        public TerrainDetailGenerationSettings TerrainDetails => terrainDetails;
        public ForestRenderingSettings ForestRendering => forestRendering;
        public ProceduralEnvironmentAssetCatalog AssetCatalog => assetCatalog;

        public float WorldWidth => global.WorldWidth;
        public float WorldLength => global.WorldLength;
        public SeedMode SeedMode => seed.SeedMode;
        public int FixedSeed => seed.FixedSeed;
        public int HeightmapResolution => terrain.HeightmapResolution;
        public float TerrainHeight => terrain.TerrainHeight;
        public float NoiseScale => terrain.NoiseScale;
        public int Octaves => terrain.Octaves;
        public float Persistence => terrain.Persistence;
        public float Lacunarity => terrain.Lacunarity;
        public float HeightMultiplier => terrain.HeightMultiplier;
        public TerrainLandShape LandShape => terrain.LandShape;
        public float RidgeIntensity => terrain.RidgeIntensity;
        public float ValleyIntensity => terrain.ValleyIntensity;
        public float CliffIntensity => terrain.CliffIntensity;
        public float TerraceStrength => terrain.TerraceStrength;
        public float MicroReliefStrength => terrain.MicroReliefStrength;
        public bool UseEdgeFalloff => terrain.UseEdgeFalloff;
        public float EdgeFalloffStart => terrain.EdgeFalloffStart;
        public float EdgeFalloffStrength => terrain.EdgeFalloffStrength;
        public bool TerrainChunkStreamingEnabled => terrainChunks.Enabled;
        public float TerrainChunkSize => terrainChunks.ChunkSize;
        public int TerrainChunkHeightmapResolution => terrainChunks.HeightmapResolution;
        public int TerrainChunkLoadRadius => terrainChunks.LoadRadiusChunks;
        public int TerrainChunkUnloadBuffer => terrainChunks.UnloadBufferChunks;
        public int TerrainChunkInitialLoadRadius => terrainChunks.InitialLoadRadiusChunks;
        public int TerrainChunkBootstrapPreloadRadius => terrainChunks.BootstrapPreloadRadiusChunks;
        public int TerrainChunkMaxBuildsPerFrame => terrainChunks.MaxChunkBuildsPerFrame;
        public int TerrainChunkMaxUnloadsPerFrame => terrainChunks.MaxChunkUnloadsPerFrame;
        public Vector3 TerrainChunkLodPixelErrors => terrainChunks.LodPixelErrors;
        public bool EnablePropPlacement => props.EnablePropPlacement;
        public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => props.PropCategories;
        public bool WaterEnabled => water.Enabled;
        public float WaterLevel => water.WaterLevel;
        public float WaterPlanePadding => water.PlanePadding;
        public Color WaterColor => water.WaterColor;
        public bool TerrainDetailsEnabled => terrainDetails.Enabled;
        public float TerrainDetailDensityMultiplier => terrainDetails.DensityMultiplier;
        public int TerrainDetailResolution => terrainDetails.DetailResolution;
        public int TerrainDetailResolutionPerPatch => terrainDetails.DetailResolutionPerPatch;
        public ForestLodSettings ForestLodSettings => forestRendering.ResolveLodSettings();
        public GpuGrassSettings GpuGrassSettings => forestRendering.ResolveGpuGrassSettings();
        public SettlementGenerationConfig Settlements => settlements;
        public PointOfInterestGenerationConfig PointsOfInterest => pointsOfInterest;
        public RoadGenerationConfig Roads => roads;

        public void ConfigureAssetCatalog(ProceduralEnvironmentAssetCatalog catalog)
        {
            assetCatalog = catalog;
        }

        public void ConfigureGlobal(float width, float length)
        {
            global.Configure(width, length);
        }

        public void ConfigureSeed(SeedMode mode, int seed)
        {
            this.seed.Configure(mode, seed);
        }

        public void ConfigureTerrain(
            int resolution,
            float height,
            float scale,
            int octaveCount,
            float persistenceValue,
            float lacunarityValue,
            float multiplier,
            TerrainLandShape shape,
            float ridges,
            float valleys,
            float cliffs,
            float terraces,
            float microRelief,
            bool edgeFalloff,
            float falloffStart,
            float falloffStrength)
        {
            terrain.Configure(
                resolution,
                height,
                scale,
                octaveCount,
                persistenceValue,
                lacunarityValue,
                multiplier,
                shape,
                ridges,
                valleys,
                cliffs,
                terraces,
                microRelief,
                edgeFalloff,
                falloffStart,
                falloffStrength);
        }

        public void ConfigureTerrainChunks(
            bool enabled,
            float chunkSize,
            int resolution,
            int loadRadius,
            int unloadBuffer,
            int initialLoadRadius,
            int bootstrapPreloadRadius,
            int chunkBuildsPerFrame,
            int chunkUnloadsPerFrame,
            Vector3 lodPixelErrors)
        {
            terrainChunks.Configure(
                enabled,
                chunkSize,
                resolution,
                loadRadius,
                unloadBuffer,
                initialLoadRadius,
                bootstrapPreloadRadius,
                chunkBuildsPerFrame,
                chunkUnloadsPerFrame,
                lodPixelErrors);
        }

        public void ConfigureProps(bool enabled, IEnumerable<PropCategoryPlacementSettings> categories)
        {
            props.Configure(enabled, categories);
        }

        public void ConfigureWater(bool enabled, float level, float padding, Color color)
        {
            water.Configure(enabled, level, padding, color);
        }

        public void ConfigureTerrainDetails(bool enabled, float densityMultiplier, int resolution, int resolutionPerPatch)
        {
            terrainDetails.Configure(enabled, densityMultiplier, resolution, resolutionPerPatch);
        }

        public void ConfigureForestRendering(ForestQualityLevel qualityPreset, bool usePresetValues = true)
        {
            forestRendering.Configure(qualityPreset, usePresetValues);
        }

        public void ConfigureForestLodSettings(ForestLodSettings settings)
        {
            forestRendering.ConfigureCustomLod(settings);
        }

        public void ConfigureGpuGrass(GpuGrassSettings settings)
        {
            forestRendering.ConfigureGpuGrass(settings);
        }

        public Bounds GetWorldBounds()
        {
            var center = Vector3.zero;
            var size = new Vector3(global.WorldWidth, 0f, global.WorldLength);
            return new Bounds(center, size);
        }
    }
}
