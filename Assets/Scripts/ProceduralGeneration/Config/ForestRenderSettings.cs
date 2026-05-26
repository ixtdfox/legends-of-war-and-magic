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

        public ForestQualityLevel QualityPreset => qualityPreset;
        public bool UsePresetValues => usePresetValues;

        public ForestLodSettings ResolveLodSettings()
        {
            return usePresetValues || customLodSettings == null
                ? ForestLodSettings.CreatePreset(qualityPreset)
                : customLodSettings.Clone();
        }

        public void Configure(ForestQualityLevel preset, bool usePreset = true)
        {
            qualityPreset = preset;
            usePresetValues = usePreset;
            if (usePresetValues || customLodSettings == null)
            {
                customLodSettings = ForestLodSettings.CreatePreset(preset);
            }
        }
    }
}
