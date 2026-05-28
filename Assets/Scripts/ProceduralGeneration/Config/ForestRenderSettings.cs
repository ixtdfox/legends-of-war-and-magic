using System;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    public enum ForestQualityLevel
    {
        Low,
        Medium,
        High,
        Ultra
    }

    [Serializable]
    public sealed class GpuGrassSettings
    {
        [SerializeField] private bool enabled = true;

        [Min(0f)]
        [SerializeField] private float drawDistance = 92f;

        [Min(0f)]
        [SerializeField] private float highDetailDistance = 34f;

        [Min(4f)]
        [SerializeField] private float chunkSize = 8f;

        [Min(0.2f)]
        [SerializeField] private float placementSpacing = 0.46f;

        [Min(0)]
        [SerializeField] private int maxVisibleClumps = 28000;

        [Range(0f, 2f)]
        [SerializeField] private float densityScale = 1f;

        [Header("Ring LOD Budgets")]
        [Min(0f)]
        [SerializeField] private float nearDistance = 18f;

        [Min(0f)]
        [SerializeField] private float midDistance = 45f;

        [Min(0f)]
        [SerializeField] private float farVisualDistance = 95f;

        [Min(0)]
        [SerializeField] private int maxVisibleGrassTriangles = 260000;

        [Min(0)]
        [SerializeField] private int maxVisibleNearInstances = 18000;

        [Min(0)]
        [SerializeField] private int maxVisibleMidInstances = 24000;

        [Range(0f, 1f)]
        [SerializeField] private float midDensityMultiplier = 0.62f;

        [SerializeField] private bool enableGrassShadows;

        [SerializeField] private bool enableTerrainDensityTint = true;

        [SerializeField] private bool enableJobs;

        [SerializeField] private bool enableIndirectHighTier;

        [SerializeField] private bool useOptimizedClusterRenderer = true;

        [Header("Density Grid")]
        [Min(16)]
        [SerializeField] private int densityGridResolution = 256;

        [SerializeField] private int grassSeed;

        [Min(1f)]
        [SerializeField] private float macroNoiseScale = 42f;

        [Min(0.5f)]
        [SerializeField] private float microNoiseScale = 4.5f;

        [Range(1, 6)]
        [SerializeField] private int noiseOctaves = 4;

        [Range(0.1f, 0.9f)]
        [SerializeField] private float noisePersistence = 0.52f;

        [Range(1.1f, 3f)]
        [SerializeField] private float noiseLacunarity = 2f;

        [Range(0f, 1f)]
        [SerializeField] private float noiseThresholdLow = 0.27f;

        [Range(0f, 1f)]
        [SerializeField] private float noiseThresholdHigh = 0.58f;

        [Range(0.25f, 4f)]
        [SerializeField] private float noiseContrast = 1.55f;

        [Range(0f, 24f)]
        [SerializeField] private float lodFadeDistance = 7f;

        [Range(1, 8)]
        [SerializeField] private int atlasColumns = 1;

        [Range(1, 8)]
        [SerializeField] private int atlasRows = 1;

        [Header("Geometry")]
        [Range(1, 8)]
        [SerializeField] private int nearBladeCount = 4;

        [Range(1, 8)]
        [SerializeField] private int midBladeCount = 2;

        [Header("Terrain Masks")]
        [Range(0f, 90f)]
        [SerializeField] private float slopeFadeStart = 20f;

        [Range(0f, 90f)]
        [SerializeField] private float slopeFadeEnd = 44f;

        [Range(0f, 1f)]
        [SerializeField] private float waterFadeStart = 0.018f;

        [Range(0f, 1f)]
        [SerializeField] private float waterFadeEnd = 0.08f;

        [Range(0f, 1f)]
        [SerializeField] private float heightFadeStart = 0.78f;

        [Range(0f, 1f)]
        [SerializeField] private float heightFadeEnd = 0.96f;

        [Range(0f, 2f)]
        [SerializeField] private float grassVariationLayerWeight = 0.85f;

        [Range(0f, 1f)]
        [SerializeField] private float shoreSuppression = 0.7f;

        [Range(0f, 1f)]
        [SerializeField] private float rockSuppression = 0.9f;

        [Header("Terrain Tint")]
        [Range(0f, 1f)]
        [SerializeField] private float terrainGrassBoost = 0.28f;

        [Range(0f, 1f)]
        [SerializeField] private float terrainVariationBoost = 0.18f;

        [Range(0f, 1f)]
        [SerializeField] private float terrainRockSuppression = 0.08f;

        [Header("Terrain Details")]
        [Range(0f, 1f)]
        [SerializeField] private float terrainDetailDensityScale = 0.38f;

        [Min(0f)]
        [SerializeField] private float terrainDetailFallbackDistance = 55f;

        [Header("Wind")]
        [Range(0f, 2f)]
        [SerializeField] private float windStrength = 0.23f;

        [Range(0f, 8f)]
        [SerializeField] private float windSpeed = 1.25f;

        [Range(0.01f, 1f)]
        [SerializeField] private float windScale = 0.13f;

        [SerializeField] private bool receiveShadows = true;

        public bool Enabled => enabled;
        public float DrawDistance => FarVisualDistance;
        public float HighDetailDistance => NearDistance;
        public float ChunkSize => ClusterSize;
        public float PlacementSpacing => Mathf.Max(0.2f, placementSpacing);
        public int MaxVisibleClumps => MaxVisibleNearInstances + MaxVisibleMidInstances;
        public float DensityScale => Mathf.Clamp(densityScale, 0f, 2f);
        public float DensityMultiplier => DensityScale;
        public float NearDistance => Mathf.Clamp(nearDistance > 0f ? nearDistance : highDetailDistance, 0f, FarVisualDistance);
        public float MidDistance => Mathf.Clamp(midDistance > 0f ? midDistance : drawDistance, NearDistance, FarVisualDistance);
        public float FarVisualDistance => Mathf.Max(
            midDistance > 0f ? midDistance : drawDistance,
            farVisualDistance > 0f ? farVisualDistance : drawDistance);
        public int MaxVisibleGrassTriangles => maxVisibleGrassTriangles > 0 ? maxVisibleGrassTriangles : int.MaxValue;
        public int MaxVisibleNearInstances => maxVisibleNearInstances > 0
            ? maxVisibleNearInstances
            : Mathf.RoundToInt(Mathf.Max(0, maxVisibleClumps) * 0.45f);
        public int MaxVisibleMidInstances => maxVisibleMidInstances > 0
            ? maxVisibleMidInstances
            : Mathf.Max(0, maxVisibleClumps - MaxVisibleNearInstances);
        public float ClusterSize => Mathf.Max(4f, chunkSize);
        public float MidDensityMultiplier => Mathf.Clamp01(midDensityMultiplier);
        public bool EnableGrassShadows => enableGrassShadows;
        public bool EnableTerrainDensityTint => enableTerrainDensityTint;
        public bool EnableJobs => enableJobs;
        public bool EnableIndirectHighTier => enableIndirectHighTier;
        public bool UseOptimizedClusterRenderer => useOptimizedClusterRenderer;
        public int DensityGridResolution => Mathf.Clamp(densityGridResolution, 32, 1024);
        public int GrassSeed => grassSeed;
        public float MacroNoiseScale => Mathf.Max(1f, macroNoiseScale);
        public float MicroNoiseScale => Mathf.Max(0.5f, microNoiseScale);
        public int NoiseOctaves => Mathf.Clamp(noiseOctaves, 1, 6);
        public float NoisePersistence => Mathf.Clamp(noisePersistence, 0.1f, 0.9f);
        public float NoiseLacunarity => Mathf.Clamp(noiseLacunarity, 1.1f, 3f);
        public float NoiseThresholdLow => Mathf.Min(noiseThresholdLow, noiseThresholdHigh);
        public float NoiseThresholdHigh => Mathf.Max(noiseThresholdLow, noiseThresholdHigh);
        public float NoiseContrast => Mathf.Clamp(noiseContrast, 0.25f, 4f);
        public float LodFadeDistance => Mathf.Clamp(lodFadeDistance, 0f, MidDistance);
        public int AtlasColumns => Mathf.Clamp(atlasColumns, 1, 8);
        public int AtlasRows => Mathf.Clamp(atlasRows, 1, 8);
        public int NearBladeCount => Mathf.Clamp(nearBladeCount, 1, 8);
        public int MidBladeCount => Mathf.Clamp(midBladeCount, 1, 8);
        public float SlopeFadeStart => Mathf.Clamp(slopeFadeStart, 0f, 90f);
        public float SlopeFadeEnd => Mathf.Clamp(Mathf.Max(slopeFadeStart, slopeFadeEnd), 0f, 90f);
        public float WaterFadeStart => Mathf.Clamp01(waterFadeStart);
        public float WaterFadeEnd => Mathf.Clamp01(Mathf.Max(waterFadeStart, waterFadeEnd));
        public float HeightFadeStart => Mathf.Clamp01(heightFadeStart);
        public float HeightFadeEnd => Mathf.Clamp01(Mathf.Max(heightFadeStart, heightFadeEnd));
        public float GrassVariationLayerWeight => Mathf.Clamp(grassVariationLayerWeight, 0f, 2f);
        public float ShoreSuppression => Mathf.Clamp01(shoreSuppression);
        public float RockSuppression => Mathf.Clamp01(rockSuppression);
        public float TerrainGrassBoost => Mathf.Clamp01(terrainGrassBoost);
        public float TerrainVariationBoost => Mathf.Clamp01(terrainVariationBoost);
        public float TerrainRockSuppression => Mathf.Clamp01(terrainRockSuppression);
        public float TerrainDetailDensityScale => Mathf.Clamp01(terrainDetailDensityScale);
        public float TerrainDetailFallbackDistance => Mathf.Max(0f, terrainDetailFallbackDistance);
        public float WindStrength => Mathf.Clamp(windStrength, 0f, 2f);
        public float WindSpeed => Mathf.Clamp(windSpeed, 0f, 8f);
        public float WindScale => Mathf.Clamp(windScale, 0.01f, 1f);
        public bool ReceiveShadows => receiveShadows;

        public static GpuGrassSettings CreatePreset(ForestQualityLevel preset)
        {
            var settings = new GpuGrassSettings();
            settings.ApplyPreset(preset);
            return settings;
        }

        public GpuGrassSettings Clone()
        {
            return new GpuGrassSettings
            {
                enabled = enabled,
                drawDistance = drawDistance,
                highDetailDistance = highDetailDistance,
                chunkSize = chunkSize,
                placementSpacing = placementSpacing,
                maxVisibleClumps = maxVisibleClumps,
                densityScale = densityScale,
                nearDistance = nearDistance,
                midDistance = midDistance,
                farVisualDistance = farVisualDistance,
                maxVisibleGrassTriangles = maxVisibleGrassTriangles,
                maxVisibleNearInstances = maxVisibleNearInstances,
                maxVisibleMidInstances = maxVisibleMidInstances,
                midDensityMultiplier = midDensityMultiplier,
                enableGrassShadows = enableGrassShadows,
                enableTerrainDensityTint = enableTerrainDensityTint,
                enableJobs = enableJobs,
                enableIndirectHighTier = enableIndirectHighTier,
                useOptimizedClusterRenderer = useOptimizedClusterRenderer,
                densityGridResolution = densityGridResolution,
                grassSeed = grassSeed,
                macroNoiseScale = macroNoiseScale,
                microNoiseScale = microNoiseScale,
                noiseOctaves = noiseOctaves,
                noisePersistence = noisePersistence,
                noiseLacunarity = noiseLacunarity,
                noiseThresholdLow = noiseThresholdLow,
                noiseThresholdHigh = noiseThresholdHigh,
                noiseContrast = noiseContrast,
                lodFadeDistance = lodFadeDistance,
                atlasColumns = atlasColumns,
                atlasRows = atlasRows,
                nearBladeCount = nearBladeCount,
                midBladeCount = midBladeCount,
                slopeFadeStart = slopeFadeStart,
                slopeFadeEnd = slopeFadeEnd,
                waterFadeStart = waterFadeStart,
                waterFadeEnd = waterFadeEnd,
                heightFadeStart = heightFadeStart,
                heightFadeEnd = heightFadeEnd,
                grassVariationLayerWeight = grassVariationLayerWeight,
                shoreSuppression = shoreSuppression,
                rockSuppression = rockSuppression,
                terrainGrassBoost = terrainGrassBoost,
                terrainVariationBoost = terrainVariationBoost,
                terrainRockSuppression = terrainRockSuppression,
                terrainDetailDensityScale = terrainDetailDensityScale,
                terrainDetailFallbackDistance = terrainDetailFallbackDistance,
                windStrength = windStrength,
                windSpeed = windSpeed,
                windScale = windScale,
                receiveShadows = receiveShadows
            };
        }

        public void ApplyPreset(ForestQualityLevel preset)
        {
            ResetRuntimeTuningDefaults();

            switch (preset)
            {
                case ForestQualityLevel.Low:
                    ConfigureBudget(
                        true,
                        14f,
                        38f,
                        90f,
                        8f,
                        0.38f,
                        200000,
                        13000,
                        17500,
                        0.96f,
                        0.34f,
                        0.2f,
                        35f,
                        0.16f,
                        1.0f,
                        0.11f,
                        false,
                        true,
                        256,
                        42f,
                        4.5f,
                        4,
                        0.52f,
                        2f,
                        0.16f,
                        0.48f,
                        1.35f);
                    break;
                case ForestQualityLevel.Medium:
                    ConfigureBudget(
                        true,
                        16f,
                        44f,
                        115f,
                        8f,
                        0.34f,
                        250000,
                        16500,
                        22000,
                        1.12f,
                        0.38f,
                        0.3f,
                        45f,
                        0.2f,
                        1.15f,
                        0.12f,
                        false,
                        true,
                        256,
                        46f,
                        4.2f,
                        4,
                        0.52f,
                        2f,
                        0.14f,
                        0.44f,
                        1.35f);
                    break;
                case ForestQualityLevel.Ultra:
                    ConfigureBudget(
                        true,
                        20f,
                        56f,
                        150f,
                        8f,
                        0.28f,
                        300000,
                        22000,
                        23000,
                        1.4f,
                        0.44f,
                        0.48f,
                        70f,
                        0.28f,
                        1.45f,
                        0.14f,
                        false,
                        true,
                        384,
                        56f,
                        3.6f,
                        5,
                        0.55f,
                        2.05f,
                        0.1f,
                        0.4f,
                        1.45f);
                    break;
                default:
                    ConfigureBudget(
                        true,
                        18f,
                        50f,
                        135f,
                        8f,
                        0.3f,
                        290000,
                        20000,
                        23500,
                        1.28f,
                        0.42f,
                        0.38f,
                        60f,
                        0.23f,
                        1.25f,
                        0.13f,
                        false,
                        true,
                        320,
                        52f,
                        4f,
                        5,
                        0.54f,
                        2.05f,
                        0.12f,
                        0.42f,
                        1.4f);
                    break;
            }
        }

        public void Configure(
            bool isEnabled,
            float distance,
            float highDistance,
            float chunk,
            float spacing,
            int visibleBudget,
            float density,
            float terrainDetailScale,
            float detailFallbackDistance,
            float wind,
            float speed,
            float scale,
            bool shadows)
        {
            enabled = isEnabled;
            drawDistance = Mathf.Max(0f, distance);
            highDetailDistance = Mathf.Clamp(highDistance, 0f, drawDistance);
            chunkSize = Mathf.Max(4f, chunk);
            placementSpacing = Mathf.Max(0.2f, spacing);
            maxVisibleClumps = Mathf.Max(0, visibleBudget);
            densityScale = Mathf.Clamp(density, 0f, 2f);
            nearDistance = highDetailDistance;
            midDistance = Mathf.Clamp(distance, nearDistance, distance);
            farVisualDistance = drawDistance;
            maxVisibleNearInstances = Mathf.RoundToInt(maxVisibleClumps * 0.45f);
            maxVisibleMidInstances = Mathf.Max(0, maxVisibleClumps - maxVisibleNearInstances);
            maxVisibleGrassTriangles = Mathf.Max(120000, maxVisibleNearInstances * 8 + maxVisibleMidInstances * 4);
            midDensityMultiplier = 0.62f;
            enableGrassShadows = shadows;
            enableTerrainDensityTint = true;
            terrainDetailDensityScale = Mathf.Clamp01(terrainDetailScale);
            terrainDetailFallbackDistance = Mathf.Max(0f, detailFallbackDistance);
            windStrength = Mathf.Clamp(wind, 0f, 2f);
            windSpeed = Mathf.Clamp(speed, 0f, 8f);
            windScale = Mathf.Clamp(scale, 0.01f, 1f);
            receiveShadows = shadows;
        }

        public void ConfigureBudget(
            bool isEnabled,
            float near,
            float mid,
            float farVisual,
            float cluster,
            float spacing,
            int triangleBudget,
            int nearInstanceBudget,
            int midInstanceBudget,
            float density,
            float midDensity,
            float terrainDetailScale,
            float detailFallbackDistance,
            float wind,
            float speed,
            float scale,
            bool grassShadows,
            bool terrainTint,
            int densityResolution,
            float macroScale,
            float microScale,
            int octaves,
            float persistence,
            float lacunarity,
            float thresholdLow,
            float thresholdHigh,
            float contrast)
        {
            enabled = isEnabled;
            nearDistance = Mathf.Max(0f, near);
            midDistance = Mathf.Max(nearDistance, mid);
            farVisualDistance = Mathf.Max(midDistance, farVisual);
            highDetailDistance = nearDistance;
            drawDistance = farVisualDistance;
            chunkSize = Mathf.Max(4f, cluster);
            placementSpacing = Mathf.Max(0.2f, spacing);
            maxVisibleGrassTriangles = Mathf.Max(0, triangleBudget);
            maxVisibleNearInstances = Mathf.Max(0, nearInstanceBudget);
            maxVisibleMidInstances = Mathf.Max(0, midInstanceBudget);
            maxVisibleClumps = maxVisibleNearInstances + maxVisibleMidInstances;
            densityScale = Mathf.Clamp(density, 0f, 2f);
            midDensityMultiplier = Mathf.Clamp01(midDensity);
            terrainDetailDensityScale = Mathf.Clamp01(terrainDetailScale);
            terrainDetailFallbackDistance = Mathf.Max(0f, detailFallbackDistance);
            windStrength = Mathf.Clamp(wind, 0f, 2f);
            windSpeed = Mathf.Clamp(speed, 0f, 8f);
            windScale = Mathf.Clamp(scale, 0.01f, 1f);
            receiveShadows = true;
            enableGrassShadows = grassShadows;
            enableTerrainDensityTint = terrainTint;
            densityGridResolution = Mathf.Clamp(densityResolution, 32, 1024);
            macroNoiseScale = Mathf.Max(1f, macroScale);
            microNoiseScale = Mathf.Max(0.5f, microScale);
            noiseOctaves = Mathf.Clamp(octaves, 1, 6);
            noisePersistence = Mathf.Clamp(persistence, 0.1f, 0.9f);
            noiseLacunarity = Mathf.Clamp(lacunarity, 1.1f, 3f);
            noiseThresholdLow = Mathf.Clamp01(thresholdLow);
            noiseThresholdHigh = Mathf.Clamp01(Mathf.Max(noiseThresholdLow, thresholdHigh));
            noiseContrast = Mathf.Clamp(contrast, 0.25f, 4f);
            nearBladeCount = densityScale >= 0.78f ? 8 : 6;
            midBladeCount = densityScale >= 0.92f ? 6 : 4;
            useOptimizedClusterRenderer = true;
        }

        private void ResetRuntimeTuningDefaults()
        {
            lodFadeDistance = 7f;
            atlasColumns = 1;
            atlasRows = 1;
            nearBladeCount = 6;
            midBladeCount = 3;
            slopeFadeStart = 20f;
            slopeFadeEnd = 44f;
            waterFadeStart = 0.018f;
            waterFadeEnd = 0.08f;
            heightFadeStart = 0.78f;
            heightFadeEnd = 0.96f;
            grassVariationLayerWeight = 0.85f;
            shoreSuppression = 0.7f;
            rockSuppression = 0.9f;
            terrainGrassBoost = 0.26f;
            terrainVariationBoost = 0.16f;
            terrainRockSuppression = 0.08f;
        }
    }

    [Serializable]
    public sealed class ForestLodSettings
    {
        [Header("Distance Bands")]
        [Min(0f)]
        [SerializeField] private float lod0Distance = 48f;

        [Min(0f)]
        [SerializeField] private float lod1Distance = 105f;

        [Min(0f)]
        [SerializeField] private float lod2Distance = 170f;

        [Min(0f)]
        [SerializeField] private float cullDistance = 190f;

        [Header("Shadows")]
        [Min(0f)]
        [SerializeField] private float shadowDistance = 62f;

        [SerializeField] private bool disableLeafShadowsAfterLod0 = true;
        [SerializeField] private bool disableAllShadowsAfterLod1 = true;

        [Range(0f, 1f)]
        [SerializeField] private float sourceShadowDistanceScale = 0.36f;

        [Min(0f)]
        [SerializeField] private float minimumSourceShadowDistance = 10f;

        [Header("Budgets")]
        [Min(0)]
        [SerializeField] private int maxHighDetailTrees = 220;

        [Min(0)]
        [SerializeField] private int maxShadowCastingTrees = 112;

        [SerializeField] private bool useFarBillboardLod = true;

        [Range(0f, 2f)]
        [SerializeField] private float foliageDensityScale = 1f;

        public float Lod0Distance => Mathf.Max(0f, lod0Distance);
        public float Lod1Distance => Mathf.Max(Lod0Distance, lod1Distance);
        public float Lod2Distance => Mathf.Max(Lod1Distance, lod2Distance);
        public float CullDistance => Mathf.Max(0f, cullDistance);
        public float ShadowDistance => Mathf.Max(0f, shadowDistance);
        public bool DisableLeafShadowsAfterLod0 => disableLeafShadowsAfterLod0;
        public bool DisableAllShadowsAfterLod1 => disableAllShadowsAfterLod1;
        public float SourceShadowDistanceScale => Mathf.Clamp01(sourceShadowDistanceScale);
        public float MinimumSourceShadowDistance => Mathf.Max(0f, minimumSourceShadowDistance);
        public int MaxHighDetailTrees => Mathf.Max(0, maxHighDetailTrees);
        public int MaxShadowCastingTrees => Mathf.Max(0, maxShadowCastingTrees);
        public bool UseFarBillboardLod => useFarBillboardLod;
        public float FoliageDensityScale => Mathf.Clamp(foliageDensityScale, 0f, 2f);

        public float NearTreeRadius => Lod0Distance;
        public float MidTreeRadius => Lod1Distance;
        public float FarTreeRadius => Lod2Distance;
        public float TreeCullDistance => CullDistance;

        public static ForestLodSettings CreatePreset(ForestQualityLevel preset)
        {
            var settings = new ForestLodSettings();
            settings.ApplyPreset(preset);
            return settings;
        }

        public ForestLodSettings Clone()
        {
            return new ForestLodSettings
            {
                lod0Distance = lod0Distance,
                lod1Distance = lod1Distance,
                lod2Distance = lod2Distance,
                cullDistance = cullDistance,
                shadowDistance = shadowDistance,
                disableLeafShadowsAfterLod0 = disableLeafShadowsAfterLod0,
                disableAllShadowsAfterLod1 = disableAllShadowsAfterLod1,
                sourceShadowDistanceScale = sourceShadowDistanceScale,
                minimumSourceShadowDistance = minimumSourceShadowDistance,
                maxHighDetailTrees = maxHighDetailTrees,
                maxShadowCastingTrees = maxShadowCastingTrees,
                useFarBillboardLod = useFarBillboardLod,
                foliageDensityScale = foliageDensityScale
            };
        }

        public void ApplyPreset(ForestQualityLevel preset)
        {
            switch (preset)
            {
                case ForestQualityLevel.Low:
                    Configure(42f, 95f, 190f, 240f, 72f, 900, 360, true, false, false, 0.82f);
                    break;
                case ForestQualityLevel.Medium:
                    Configure(56f, 130f, 260f, 330f, 95f, 1400, 520, true, false, false, 1.00f);
                    break;
                case ForestQualityLevel.Ultra:
                    Configure(80f, 190f, 360f, 460f, 140f, 2600, 950, true, false, false, 1.20f);
                    break;
                default:
                    Configure(68f, 160f, 320f, 410f, 120f, 2100, 760, true, false, false, 1.12f);
                    break;
            }
        }

        public void Configure(
            float lod0,
            float lod1,
            float lod2,
            float cull,
            float shadows,
            int highDetailTreeBudget,
            int shadowCastingTreeBudget,
            bool useBillboards,
            bool disableLeafShadowsPastLod0,
            bool disableAllShadowsPastLod1,
            float foliageScale)
        {
            lod0Distance = Mathf.Max(0f, lod0);
            lod1Distance = Mathf.Max(lod0Distance, lod1);
            lod2Distance = Mathf.Max(lod1Distance, lod2);
            cullDistance = Mathf.Max(0f, cull);
            shadowDistance = Mathf.Max(0f, shadows);
            maxHighDetailTrees = Mathf.Max(0, highDetailTreeBudget);
            maxShadowCastingTrees = Mathf.Max(0, shadowCastingTreeBudget);
            useFarBillboardLod = useBillboards;
            disableLeafShadowsAfterLod0 = disableLeafShadowsPastLod0;
            disableAllShadowsAfterLod1 = disableAllShadowsPastLod1;
            sourceShadowDistanceScale = 1f;
            minimumSourceShadowDistance = 10f;
            foliageDensityScale = Mathf.Clamp(foliageScale, 0f, 2f);
        }

        public int ResolveLodIndex(float distance, float instanceCullDistance, int maxLodIndex)
        {
            if (!IsInsideCullDistance(distance, instanceCullDistance))
            {
                return -1;
            }

            if (maxLodIndex <= 0)
            {
                return 0;
            }

            if (distance <= Lod0Distance)
            {
                return 0;
            }

            if (maxLodIndex == 1)
            {
                return distance <= Lod1Distance ? 1 : -1;
            }

            if (distance <= Lod1Distance)
            {
                return 1;
            }

            if (!UseFarBillboardLod)
            {
                return distance <= Lod2Distance ? Mathf.Min(1, maxLodIndex) : -1;
            }

            return distance <= Lod2Distance ? Mathf.Min(2, maxLodIndex) : -1;
        }

        public bool IsInsideCullDistance(float distance, float instanceCullDistance)
        {
            var effectiveCullDistance = CullDistance > 0f
                ? CullDistance
                : Lod2Distance;
            if (instanceCullDistance > 0f)
            {
                effectiveCullDistance = Mathf.Min(effectiveCullDistance, instanceCullDistance);
            }

            return distance <= effectiveCullDistance;
        }
    }

    [Serializable]
    public sealed class ForestRenderingSettings
    {
        [SerializeField] private ForestQualityLevel qualityPreset = ForestQualityLevel.High;
        [SerializeField] private bool usePresetValues = true;
        [SerializeField] private ForestLodSettings customLodSettings = ForestLodSettings.CreatePreset(ForestQualityLevel.High);
        [SerializeField] private GpuGrassSettings gpuGrassSettings = GpuGrassSettings.CreatePreset(ForestQualityLevel.High);

        public ForestQualityLevel QualityPreset => qualityPreset;
        public bool UsePresetValues => usePresetValues;
        public GpuGrassSettings GpuGrassSettings => ResolveGpuGrassSettings();

        public ForestLodSettings ResolveLodSettings()
        {
            return usePresetValues || customLodSettings == null
                ? ForestLodSettings.CreatePreset(qualityPreset)
                : customLodSettings.Clone();
        }

        public GpuGrassSettings ResolveGpuGrassSettings()
        {
            return usePresetValues || gpuGrassSettings == null
                ? GpuGrassSettings.CreatePreset(qualityPreset)
                : gpuGrassSettings.Clone();
        }

        public void Configure(ForestQualityLevel preset, bool usePreset = true)
        {
            qualityPreset = preset;
            usePresetValues = usePreset;
            if (usePresetValues || customLodSettings == null)
            {
                customLodSettings = ForestLodSettings.CreatePreset(preset);
            }

            if (usePresetValues || gpuGrassSettings == null)
            {
                gpuGrassSettings = GpuGrassSettings.CreatePreset(preset);
            }
        }

        public void ConfigureGpuGrass(GpuGrassSettings settings)
        {
            usePresetValues = false;
            gpuGrassSettings = settings != null
                ? settings.Clone()
                : GpuGrassSettings.CreatePreset(qualityPreset);

            customLodSettings ??= ForestLodSettings.CreatePreset(qualityPreset);
        }

        public void ConfigureCustomLod(ForestLodSettings settings)
        {
            usePresetValues = false;
            customLodSettings = settings != null
                ? settings.Clone()
                : ForestLodSettings.CreatePreset(qualityPreset);

            gpuGrassSettings ??= GpuGrassSettings.CreatePreset(qualityPreset);
        }
    }
}
