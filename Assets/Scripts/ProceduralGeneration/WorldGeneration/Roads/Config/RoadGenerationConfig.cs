using System;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config
{
    [Serializable]
    public sealed class RoadGenerationConfig
    {
        [SerializeField] private bool enabled = true;

        [Header("Pathfinding")]
        [Min(16)]
        [SerializeField] private int pathfindingGridResolution = 192;

        [Min(0f)]
        [SerializeField] private float slopePenalty = 10f;

        [Range(1f, 5f)]
        [SerializeField] private float slopePower = 2f;

        [Min(0f)]
        [SerializeField] private float waterPenalty = 600f;

        [Min(0f)]
        [SerializeField] private float forbiddenZonePenalty = 900f;

        [Min(0f)]
        [SerializeField] private float existingRoadBonus = 4f;

        [Min(0f)]
        [SerializeField] private float elevationChangePenalty = 28f;

        [Min(0f)]
        [SerializeField] private float verySteepSlopePenalty = 120f;

        [Header("Road Widths")]
        [SerializeField] private RoadTypeSettings trail = new(RoadType.Trail, 2.2f, 1.6f, 0.7f);
        [SerializeField] private RoadTypeSettings dirtRoad = new(RoadType.DirtRoad, 3.5f, 1.25f, 0.8f);
        [SerializeField] private RoadTypeSettings mainRoad = new(RoadType.MainRoad, 5f, 1f, 0.9f);
        [SerializeField] private RoadTypeSettings stoneRoad = new(RoadType.StoneRoad, 6f, 0.82f, 1f);
        [SerializeField] private RoadTypeSettings hiddenPath = new(RoadType.HiddenPath, 1.4f, 2.2f, 0.45f);

        public bool Enabled => enabled;
        public int PathfindingGridResolution => Mathf.Clamp(pathfindingGridResolution, 16, 256);
        public float SlopePenalty => Mathf.Max(0f, slopePenalty);
        public float SlopePower => Mathf.Clamp(slopePower, 1f, 5f);
        public float WaterPenalty => Mathf.Max(0f, waterPenalty);
        public float ForbiddenZonePenalty => Mathf.Max(0f, forbiddenZonePenalty);
        public float ExistingRoadBonus => Mathf.Max(0f, existingRoadBonus);
        public float ElevationChangePenalty => Mathf.Max(0f, elevationChangePenalty);
        public float VerySteepSlopePenalty => Mathf.Max(0f, verySteepSlopePenalty);

        public RoadTypeSettings Resolve(RoadType type)
        {
            return type switch
            {
                RoadType.Trail => trail,
                RoadType.DirtRoad => dirtRoad,
                RoadType.MainRoad => mainRoad,
                RoadType.StoneRoad => stoneRoad,
                RoadType.HiddenPath => hiddenPath,
                _ => dirtRoad
            };
        }
    }

    [Serializable]
    public sealed class RoadTypeSettings
    {
        [SerializeField] private RoadType type = RoadType.DirtRoad;
        [Min(0.25f)]
        [SerializeField] private float width = 3f;
        [Min(0.1f)]
        [SerializeField] private float terrainCostMultiplier = 1f;
        [Range(0f, 1f)]
        [SerializeField] private float smoothing = 0.7f;
        [Min(0.1f)]
        [SerializeField] private float shoulderWidth = 2.25f;
        [Min(0f)]
        [SerializeField] private float heightBedExtraWidth = 1.25f;
        [Min(0f)]
        [SerializeField] private float outerSmoothWidth = 6f;
        [Range(0f, 1f)]
        [SerializeField] private float outerSmoothingStrength = 0.35f;
        [Range(0.01f, 0.45f)]
        [SerializeField] private float maxLongitudinalSlope = 0.16f;
        [Range(0f, 0.35f)]
        [SerializeField] private float maxCrossSlope = 0.035f;
        [Min(0.01f)]
        [SerializeField] private float maxHeightDeltaPerMeter = 0.30f;
        [Range(0f, 1f)]
        [SerializeField] private float cutStrength = 0.92f;
        [Range(0f, 1f)]
        [SerializeField] private float fillStrength = 0.62f;
        [Min(0)]
        [SerializeField] private int heightSmoothingIterations = 5;
        [Min(0)]
        [SerializeField] private int terrainSmoothingIterations = 3;
        [Min(0)]
        [SerializeField] private int gradientLimitIterations = 4;
        [Min(0.5f)]
        [SerializeField] private float profileSampleStepMeters = 2.5f;
        [Min(0.5f)]
        [SerializeField] private float profileSmoothingRadiusMeters = 14f;
        [Range(0f, 0.75f)]
        [SerializeField] private float edgeNoiseStrength = 0.18f;
        [Range(0f, 1f)]
        [SerializeField] private float stoneBlend = 0.0f;

        public RoadTypeSettings()
        {
            ApplyCarvingDefaults(type);
        }

        public RoadTypeSettings(RoadType type, float width, float terrainCostMultiplier, float smoothing)
        {
            this.type = type;
            this.width = width;
            this.terrainCostMultiplier = terrainCostMultiplier;
            this.smoothing = smoothing;
            ApplyCarvingDefaults(type);
        }

        public RoadType Type => type;
        public float Width => Mathf.Max(0.25f, width);
        public float TerrainCostMultiplier => Mathf.Max(0.1f, terrainCostMultiplier);
        public float Smoothing => Mathf.Clamp01(smoothing);
        public float ShoulderWidth => Mathf.Max(0.1f, shoulderWidth);
        public float HeightBedExtraWidth => Mathf.Max(0f, heightBedExtraWidth);
        public float OuterSmoothWidth => Mathf.Max(0f, outerSmoothWidth);
        public float OuterSmoothingStrength => Mathf.Clamp01(outerSmoothingStrength);
        public float MaxLongitudinalSlope => Mathf.Clamp(maxLongitudinalSlope, 0.01f, 0.45f);
        public float MaxCrossSlope => Mathf.Clamp(maxCrossSlope, 0f, 0.35f);
        public float MaxHeightDeltaPerMeter => Mathf.Clamp(maxHeightDeltaPerMeter, 0.01f, 2f);
        public float CutStrength => Mathf.Clamp01(cutStrength);
        public float FillStrength => Mathf.Clamp01(fillStrength);
        public int HeightSmoothingIterations => Mathf.Clamp(heightSmoothingIterations, 0, 16);
        public int TerrainSmoothingIterations => Mathf.Clamp(terrainSmoothingIterations, 0, 12);
        public int GradientLimitIterations => Mathf.Clamp(gradientLimitIterations, 0, 12);
        public float ProfileSampleStepMeters => Mathf.Clamp(profileSampleStepMeters, 0.5f, 20f);
        public float ProfileSmoothingRadiusMeters => Mathf.Clamp(profileSmoothingRadiusMeters, 0.5f, 80f);
        public float EdgeNoiseStrength => Mathf.Clamp01(edgeNoiseStrength);
        public float StoneBlend => Mathf.Clamp01(stoneBlend);

        private void ApplyCarvingDefaults(RoadType roadType)
        {
            switch (roadType)
            {
                case RoadType.Trail:
                    shoulderWidth = 4.0f;
                    heightBedExtraWidth = 3.2f;
                    outerSmoothWidth = 8.0f;
                    outerSmoothingStrength = 0.34f;
                    maxLongitudinalSlope = 0.16f;
                    maxCrossSlope = 0.035f;
                    maxHeightDeltaPerMeter = 0.22f;
                    cutStrength = 0.72f;
                    fillStrength = 0.48f;
                    heightSmoothingIterations = 5;
                    terrainSmoothingIterations = 4;
                    gradientLimitIterations = 6;
                    profileSampleStepMeters = 4.0f;
                    profileSmoothingRadiusMeters = 20f;
                    edgeNoiseStrength = 0.35f;
                    stoneBlend = 0f;
                    break;
                case RoadType.MainRoad:
                    shoulderWidth = 8.0f;
                    heightBedExtraWidth = 8.0f;
                    outerSmoothWidth = 13.0f;
                    outerSmoothingStrength = 0.52f;
                    maxLongitudinalSlope = 0.095f;
                    maxCrossSlope = 0.012f;
                    maxHeightDeltaPerMeter = 0.10f;
                    cutStrength = 0.95f;
                    fillStrength = 0.78f;
                    heightSmoothingIterations = 11;
                    terrainSmoothingIterations = 7;
                    gradientLimitIterations = 9;
                    profileSampleStepMeters = 5.0f;
                    profileSmoothingRadiusMeters = 38f;
                    edgeNoiseStrength = 0.13f;
                    stoneBlend = 0.25f;
                    break;
                case RoadType.StoneRoad:
                    shoulderWidth = 9.0f;
                    heightBedExtraWidth = 9.0f;
                    outerSmoothWidth = 14.0f;
                    outerSmoothingStrength = 0.58f;
                    maxLongitudinalSlope = 0.08f;
                    maxCrossSlope = 0.01f;
                    maxHeightDeltaPerMeter = 0.08f;
                    cutStrength = 1f;
                    fillStrength = 0.84f;
                    heightSmoothingIterations = 13;
                    terrainSmoothingIterations = 8;
                    gradientLimitIterations = 10;
                    profileSampleStepMeters = 5.0f;
                    profileSmoothingRadiusMeters = 44f;
                    edgeNoiseStrength = 0.08f;
                    stoneBlend = 1f;
                    break;
                case RoadType.HiddenPath:
                    shoulderWidth = 3.0f;
                    heightBedExtraWidth = 2.2f;
                    outerSmoothWidth = 6.0f;
                    outerSmoothingStrength = 0.26f;
                    maxLongitudinalSlope = 0.20f;
                    maxCrossSlope = 0.045f;
                    maxHeightDeltaPerMeter = 0.28f;
                    cutStrength = 0.45f;
                    fillStrength = 0.30f;
                    heightSmoothingIterations = 3;
                    terrainSmoothingIterations = 3;
                    gradientLimitIterations = 5;
                    profileSampleStepMeters = 3.5f;
                    profileSmoothingRadiusMeters = 14f;
                    edgeNoiseStrength = 0.42f;
                    stoneBlend = 0f;
                    break;
                default:
                    shoulderWidth = 7.0f;
                    heightBedExtraWidth = 6.0f;
                    outerSmoothWidth = 11.0f;
                    outerSmoothingStrength = 0.48f;
                    maxLongitudinalSlope = 0.11f;
                    maxCrossSlope = 0.018f;
                    maxHeightDeltaPerMeter = 0.14f;
                    cutStrength = 0.86f;
                    fillStrength = 0.66f;
                    heightSmoothingIterations = 9;
                    terrainSmoothingIterations = 6;
                    gradientLimitIterations = 8;
                    profileSampleStepMeters = 5.0f;
                    profileSmoothingRadiusMeters = 32f;
                    edgeNoiseStrength = 0.18f;
                    stoneBlend = 0.05f;
                    break;
            }
        }
    }
}
