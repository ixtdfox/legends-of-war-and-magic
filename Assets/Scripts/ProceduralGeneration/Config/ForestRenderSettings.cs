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
        [SerializeField] private float chunkSize = 18f;

        [Min(0.35f)]
        [SerializeField] private float placementSpacing = 1.18f;

        [Min(0)]
        [SerializeField] private int maxVisibleClumps = 28000;

        [Range(0f, 2f)]
        [SerializeField] private float densityScale = 1f;

        [Range(0f, 1f)]
        [SerializeField] private float terrainDetailDensityScale = 0.38f;

        [Min(0f)]
        [SerializeField] private float terrainDetailFallbackDistance = 55f;

        [Range(0f, 2f)]
        [SerializeField] private float windStrength = 0.23f;

        [Range(0f, 8f)]
        [SerializeField] private float windSpeed = 1.25f;

        [Range(0.01f, 1f)]
        [SerializeField] private float windScale = 0.13f;

        [SerializeField] private bool receiveShadows = true;

        public bool Enabled => enabled;
        public float DrawDistance => Mathf.Max(0f, drawDistance);
        public float HighDetailDistance => Mathf.Clamp(highDetailDistance, 0f, DrawDistance);
        public float ChunkSize => Mathf.Max(4f, chunkSize);
        public float PlacementSpacing => Mathf.Max(0.35f, placementSpacing);
        public int MaxVisibleClumps => Mathf.Max(0, maxVisibleClumps);
        public float DensityScale => Mathf.Clamp(densityScale, 0f, 2f);
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
            switch (preset)
            {
                case ForestQualityLevel.Low:
                    Configure(true, 60f, 16f, 22f, 0.85f, 20000, 0.82f, 0.2f, 35f, 0.16f, 1.0f, 0.11f, false);
                    break;
                case ForestQualityLevel.Medium:
                    Configure(true, 90f, 26f, 18f, 0.58f, 48000, 1.08f, 0.3f, 45f, 0.2f, 1.15f, 0.12f, true);
                    break;
                case ForestQualityLevel.Ultra:
                    Configure(true, 135f, 42f, 16f, 0.38f, 120000, 1.4f, 0.48f, 70f, 0.28f, 1.45f, 0.14f, true);
                    break;
                default:
                    Configure(true, 120f, 34f, 16f, 0.46f, 80000, 1.35f, 0.38f, 60f, 0.23f, 1.25f, 0.13f, true);
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
            placementSpacing = Mathf.Max(0.35f, spacing);
            maxVisibleClumps = Mathf.Max(0, visibleBudget);
            densityScale = Mathf.Clamp(density, 0f, 2f);
            terrainDetailDensityScale = Mathf.Clamp01(terrainDetailScale);
            terrainDetailFallbackDistance = Mathf.Max(0f, detailFallbackDistance);
            windStrength = Mathf.Clamp(wind, 0f, 2f);
            windSpeed = Mathf.Clamp(speed, 0f, 8f);
            windScale = Mathf.Clamp(scale, 0.01f, 1f);
            receiveShadows = shadows;
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
        public float CullDistance => Mathf.Max(Lod2Distance, cullDistance);
        public float ShadowDistance => Mathf.Max(0f, shadowDistance);
        public bool DisableLeafShadowsAfterLod0 => disableLeafShadowsAfterLod0;
        public bool DisableAllShadowsAfterLod1 => disableAllShadowsAfterLod1;
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
                    Configure(24f, 58f, 105f, 125f, 34f, 80, 45, true, true, true, 0.72f);
                    break;
                case ForestQualityLevel.Medium:
                    Configure(36f, 80f, 140f, 160f, 50f, 140, 75, true, true, true, 0.88f);
                    break;
                case ForestQualityLevel.Ultra:
                    Configure(70f, 145f, 220f, 260f, 95f, 360, 180, true, true, true, 1.1f);
                    break;
                default:
                    Configure(48f, 105f, 170f, 190f, 62f, 220, 112, true, true, true, 1f);
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
            cullDistance = Mathf.Max(lod2Distance, cull);
            shadowDistance = Mathf.Max(0f, shadows);
            maxHighDetailTrees = Mathf.Max(0, highDetailTreeBudget);
            maxShadowCastingTrees = Mathf.Max(0, shadowCastingTreeBudget);
            useFarBillboardLod = useBillboards;
            disableLeafShadowsAfterLod0 = disableLeafShadowsPastLod0;
            disableAllShadowsAfterLod1 = disableAllShadowsPastLod1;
            foliageDensityScale = Mathf.Clamp(foliageScale, 0f, 2f);
        }

        public int ResolveLodIndex(float distance, float instanceCullDistance, int maxLodIndex)
        {
            if (maxLodIndex <= 0)
            {
                return IsInsideCullDistance(distance, instanceCullDistance) ? 0 : -1;
            }

            if (!IsInsideCullDistance(distance, instanceCullDistance))
            {
                return -1;
            }

            if (distance <= Lod0Distance)
            {
                return 0;
            }

            if (maxLodIndex == 1)
            {
                return 1;
            }

            if (distance <= Lod1Distance)
            {
                return 1;
            }

            if (!UseFarBillboardLod)
            {
                return Mathf.Min(1, maxLodIndex);
            }

            return Mathf.Min(2, maxLodIndex);
        }

        public bool IsInsideCullDistance(float distance, float instanceCullDistance)
        {
            var effectiveCullDistance = CullDistance;
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
    }
}
