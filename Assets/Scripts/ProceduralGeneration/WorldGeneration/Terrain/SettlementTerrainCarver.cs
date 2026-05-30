using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain
{
    public static class SettlementTerrainCarver
    {
        private const int SmoothingIterations = 5;
        private const int GradientLimitIterations = 36;
        private const float MaxSettlementEdgeDeltaPerMeter = 0.18f;
        private const int SpikeRepairIterations = 3;

        public static void Apply(
            float[,] heights,
            float minX,
            float minZ,
            float width,
            float length,
            ProceduralLocationSettings settings,
            IReadOnlyList<GeneratedSettlement> settlements,
            IProceduralTerrainSampler baseSampler)
        {
            if (heights == null || settings == null || settlements == null || settlements.Count == 0)
            {
                return;
            }

            var platforms = BuildPlatformInfos(settings, settlements, baseSampler);
            if (platforms.Length == 0)
            {
                return;
            }

            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var terrainHeight = Mathf.Max(1f, settings.TerrainHeight);
            var invTerrainHeight = 1f / terrainHeight;
            var spacingX = width / Mathf.Max(1, resolutionX - 1);
            var spacingZ = length / Mathf.Max(1, resolutionZ - 1);

            var original = (float[,])heights.Clone();
            var next = (float[,])heights.Clone();
            var zones = new byte[resolutionZ, resolutionX];
            var zoneWeights = new float[resolutionZ, resolutionX];
            var platformHeights = new float[resolutionZ, resolutionX];
            var changed = false;

            for (var z = 0; z < resolutionZ; z++)
            {
                var worldZ = minZ + length * (z / (float)Mathf.Max(1, resolutionZ - 1));
                for (var x = 0; x < resolutionX; x++)
                {
                    var worldX = minX + width * (x / (float)Mathf.Max(1, resolutionX - 1));
                    var point = new Vector2(worldX, worldZ);
                    var originalMeters = original[z, x] * terrainHeight;
                    if (!TryEvaluate(point, originalMeters, platforms, out var sample))
                    {
                        continue;
                    }

                    var targetMeters = sample.Zone == SettlementTerrainZone.OuterSmoothing
                        ? SmoothNeighborhoodHeightMeters(original, z, x, terrainHeight)
                        : sample.TargetHeightMeters;
                    var nextHeight = Mathf.Clamp01(Mathf.Lerp(originalMeters, targetMeters, sample.ApplyWeight) * invTerrainHeight);
                    if (Mathf.Abs(nextHeight - next[z, x]) > 0.00001f)
                    {
                        next[z, x] = nextHeight;
                        changed = true;
                    }

                    zones[z, x] = (byte)sample.Zone;
                    zoneWeights[z, x] = sample.MaskWeight;
                    platformHeights[z, x] = Mathf.Clamp01(sample.PlatformHeightMeters * invTerrainHeight);
                }
            }

            if (!changed)
            {
                return;
            }

            SmoothModifiedTerrain(next, original, zones, zoneWeights, platformHeights, SmoothingIterations);
            LimitGradients(next, zones, zoneWeights, spacingX, spacingZ, terrainHeight);
            SmoothModifiedTerrain(next, original, zones, zoneWeights, platformHeights, Mathf.Max(1, SmoothingIterations / 2));
            LimitGradients(next, zones, zoneWeights, spacingX, spacingZ, terrainHeight);
            RepairSingleSampleSpikes(next, zones, zoneWeights, platformHeights, spacingX, spacingZ, terrainHeight);

            for (var z = 0; z < resolutionZ; z++)
            {
                for (var x = 0; x < resolutionX; x++)
                {
                    heights[z, x] = next[z, x];
                }
            }
        }

        public static float SampleAdjustedHeightMeters(
            float baseHeightMeters,
            Vector2 point,
            IReadOnlyList<SettlementTerrainPlatform> platforms)
        {
            if (platforms == null || platforms.Count == 0)
            {
                return baseHeightMeters;
            }

            return TryEvaluate(point, baseHeightMeters, platforms, out var sample) &&
                   sample.Zone != SettlementTerrainZone.OuterSmoothing
                ? Mathf.Lerp(baseHeightMeters, sample.TargetHeightMeters, sample.ApplyWeight)
                : baseHeightMeters;
        }

        public static SettlementTerrainPlatform[] BuildPlatformInfos(
            ProceduralLocationSettings settings,
            IReadOnlyList<GeneratedSettlement> settlements,
            IProceduralTerrainSampler baseSampler)
        {
            if (settings == null || settlements == null || settlements.Count == 0 || baseSampler == null)
            {
                return Array.Empty<SettlementTerrainPlatform>();
            }

            var platforms = new List<SettlementTerrainPlatform>(settlements.Count);
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                if (settlement == null)
                {
                    continue;
                }

                var platformRadius = ResolvePlatformRadius(settlement);
                var shoulderWidth = ResolveShoulderWidth(settlement);
                var outerSmoothWidth = ResolveOuterSmoothWidth(settlement);
                var platformHeight = ResolvePlatformHeightMeters(baseSampler, settlement, platformRadius);
                platforms.Add(new SettlementTerrainPlatform(
                    settlement.Id,
                    settlement.WorldPosition,
                    platformRadius,
                    shoulderWidth,
                    outerSmoothWidth,
                    platformHeight,
                    ResolveOuterSmoothingStrength(settlement.Tier)));
            }

            return platforms.ToArray();
        }

        private static bool TryEvaluate(
            Vector2 point,
            float originalHeightMeters,
            IReadOnlyList<SettlementTerrainPlatform> platforms,
            out SettlementTerrainSample sample)
        {
            sample = default;
            var hasSample = false;
            var bestPriority = float.MinValue;
            for (var i = 0; i < platforms.Count; i++)
            {
                var platform = platforms[i];
                var distance = Vector2.Distance(point, platform.Center);
                if (distance > platform.TotalInfluenceRadius)
                {
                    continue;
                }

                SettlementTerrainZone zone;
                float targetHeightMeters;
                float applyWeight;
                float maskWeight;
                float priority;
                if (distance <= platform.PlatformRadius)
                {
                    zone = SettlementTerrainZone.Platform;
                    targetHeightMeters = platform.HeightMeters;
                    applyWeight = 1f;
                    maskWeight = 1f;
                    priority = 3f + Mathf.InverseLerp(platform.PlatformRadius, 0f, distance);
                }
                else if (distance <= platform.ShoulderEndRadius)
                {
                    var t = Smooth01(Mathf.InverseLerp(platform.PlatformRadius, platform.ShoulderEndRadius, distance));
                    zone = SettlementTerrainZone.Shoulder;
                    targetHeightMeters = Mathf.Lerp(platform.HeightMeters, originalHeightMeters, t);
                    applyWeight = 1f;
                    maskWeight = 1f - t;
                    priority = 2f + maskWeight;
                }
                else
                {
                    var t = Smooth01(Mathf.InverseLerp(platform.ShoulderEndRadius, platform.TotalInfluenceRadius, distance));
                    zone = SettlementTerrainZone.OuterSmoothing;
                    targetHeightMeters = originalHeightMeters;
                    maskWeight = Mathf.Pow(1f - t, 2f);
                    applyWeight = maskWeight * platform.OuterSmoothingStrength;
                    priority = maskWeight * 0.75f;
                }

                if (priority <= bestPriority)
                {
                    continue;
                }

                sample = new SettlementTerrainSample(zone, targetHeightMeters, applyWeight, maskWeight, platform.HeightMeters);
                bestPriority = priority;
                hasSample = true;
            }

            return hasSample;
        }

        private static float ResolvePlatformHeightMeters(
            IProceduralTerrainSampler sampler,
            GeneratedSettlement settlement,
            float platformRadius)
        {
            var center = settlement.WorldPosition;
            var innerRadius = Mathf.Clamp(platformRadius * 0.45f, 5f, 28f);
            var outerRadius = Mathf.Clamp(platformRadius * 0.85f, 8f, 48f);
            var samples = new float[17];
            samples[0] = sampler.SampleHeightMeters(center.x, center.y);
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Mathf.PI * 0.25f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var innerPoint = center + direction * innerRadius;
                var outerPoint = center + direction * outerRadius;
                samples[i + 1] = sampler.SampleHeightMeters(innerPoint.x, innerPoint.y);
                samples[i + 9] = sampler.SampleHeightMeters(outerPoint.x, outerPoint.y);
            }

            Array.Sort(samples);
            var start = 4;
            var end = samples.Length - 4;
            var total = 0f;
            var count = 0;
            for (var i = start; i < end; i++)
            {
                total += samples[i];
                count++;
            }

            return count <= 0 ? samples[samples.Length / 2] : total / count;
        }

        private static float ResolvePlatformRadius(GeneratedSettlement settlement)
        {
            var tierScale = settlement.Tier switch
            {
                SettlementTier.Camp => 0.62f,
                SettlementTier.Hamlet => 0.82f,
                SettlementTier.Village => 0.90f,
                SettlementTier.Town => 0.92f,
                SettlementTier.City => 0.94f,
                SettlementTier.Capital => 0.96f,
                _ => 0.86f
            };

            var radius = Mathf.Max(5f, settlement.Radius * tierScale);
            for (var i = 0; i < settlement.Buildings.Count; i++)
            {
                var building = settlement.Buildings[i];
                var footprintRadius = building.FootprintSize.magnitude * 0.5f + 7f;
                radius = Mathf.Max(radius, Vector2.Distance(settlement.WorldPosition, building.WorldPosition) + footprintRadius);
            }

            for (var i = 0; i < settlement.Districts.Count; i++)
            {
                var district = settlement.Districts[i];
                radius = Mathf.Max(radius, Vector2.Distance(settlement.WorldPosition, district.Center) + district.Radius);
            }

            return radius;
        }

        private static float ResolveShoulderWidth(GeneratedSettlement settlement)
        {
            var minimum = settlement.Tier == SettlementTier.Camp ? 16f : 42f;
            return Mathf.Max(minimum, settlement.Radius * 0.55f);
        }

        private static float ResolveOuterSmoothWidth(GeneratedSettlement settlement)
        {
            var minimum = settlement.Tier == SettlementTier.Camp ? 12f : 28f;
            return Mathf.Max(minimum, settlement.Radius * 0.35f);
        }

        private static float ResolveOuterSmoothingStrength(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 0.28f,
                SettlementTier.Hamlet => 0.34f,
                SettlementTier.Village => 0.38f,
                SettlementTier.Town => 0.44f,
                SettlementTier.City => 0.48f,
                SettlementTier.Capital => 0.52f,
                _ => 0.36f
            };
        }

        private static float SmoothNeighborhoodHeightMeters(float[,] original, int z, int x, float terrainHeight)
        {
            var resolutionZ = original.GetLength(0);
            var resolutionX = original.GetLength(1);
            var total = 0f;
            var weightTotal = 0f;
            for (var oz = -2; oz <= 2; oz++)
            {
                var pz = Mathf.Clamp(z + oz, 0, resolutionZ - 1);
                for (var ox = -2; ox <= 2; ox++)
                {
                    var px = Mathf.Clamp(x + ox, 0, resolutionX - 1);
                    var distance = Mathf.Sqrt(ox * ox + oz * oz);
                    var weight = distance <= 0.01f ? 4f : 1f / distance;
                    total += original[pz, px] * weight;
                    weightTotal += weight;
                }
            }

            return weightTotal <= 0f ? original[z, x] * terrainHeight : total / weightTotal * terrainHeight;
        }

        private static void SmoothModifiedTerrain(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] platformHeights,
            int iterations)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var buffer = (float[,])heights.Clone();
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        var zone = (SettlementTerrainZone)zones[z, x];
                        if (zone == SettlementTerrainZone.None)
                        {
                            buffer[z, x] = heights[z, x];
                            continue;
                        }

                        if (zone == SettlementTerrainZone.Platform)
                        {
                            buffer[z, x] = platformHeights[z, x];
                            continue;
                        }

                        var average = NeighborAverage(heights, z, x);
                        var strength = zone == SettlementTerrainZone.Shoulder ? 0.42f : 0.22f;
                        var smoothed = Mathf.Lerp(heights[z, x], average, strength * Mathf.Clamp01(zoneWeights[z, x]));
                        if (zone == SettlementTerrainZone.Shoulder)
                        {
                            var falloffToOriginal = Mathf.Clamp01(1f - zoneWeights[z, x]);
                            smoothed = Mathf.Lerp(smoothed, original[z, x], falloffToOriginal * 0.12f);
                        }

                        buffer[z, x] = smoothed;
                    }
                }

                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        heights[z, x] = buffer[z, x];
                    }
                }

                RestorePlatform(heights, zones, platformHeights);
            }
        }

        private static void LimitGradients(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            float spacingX,
            float spacingZ,
            float terrainHeight)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var neighbors = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(1, -1)
            };

            for (var iteration = 0; iteration < GradientLimitIterations; iteration++)
            {
                var changed = false;
                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        var zone = (SettlementTerrainZone)zones[z, x];
                        if (zone == SettlementTerrainZone.None ||
                            (zone != SettlementTerrainZone.Platform && zoneWeights[z, x] < 0.035f))
                        {
                            continue;
                        }

                        for (var i = 0; i < neighbors.Length; i++)
                        {
                            var nx = x + neighbors[i].x;
                            var nz = z + neighbors[i].y;
                            if (nx < 0 || nz < 0 || nx >= resolutionX || nz >= resolutionZ)
                            {
                                continue;
                            }

                            var spacing = neighbors[i].x != 0 && neighbors[i].y != 0
                                ? Mathf.Sqrt(spacingX * spacingX + spacingZ * spacingZ)
                                : neighbors[i].x != 0
                                    ? spacingX
                                    : spacingZ;
                            changed |= LimitPair(heights, zones, zoneWeights, z, x, nz, nx, spacing, terrainHeight);
                        }
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private static void RepairSingleSampleSpikes(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] platformHeights,
            float spacingX,
            float spacingZ,
            float terrainHeight)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var buffer = (float[,])heights.Clone();
            var spacing = Mathf.Max(0.1f, Mathf.Min(spacingX, spacingZ));
            var spikeThreshold = Mathf.Max(0.10f, MaxSettlementEdgeDeltaPerMeter * spacing * 1.25f) / Mathf.Max(1f, terrainHeight);

            for (var iteration = 0; iteration < SpikeRepairIterations; iteration++)
            {
                var changed = false;
                for (var z = 1; z < resolutionZ - 1; z++)
                {
                    for (var x = 1; x < resolutionX - 1; x++)
                    {
                        var zone = (SettlementTerrainZone)zones[z, x];
                        if (zone == SettlementTerrainZone.Platform)
                        {
                            buffer[z, x] = platformHeights[z, x];
                            continue;
                        }

                        var modifiedNeighbors = CountModifiedNeighbors(zones, zoneWeights, z, x);
                        if (zone == SettlementTerrainZone.None && modifiedNeighbors < 4)
                        {
                            buffer[z, x] = heights[z, x];
                            continue;
                        }

                        var trimmedAverage = TrimmedNeighborAverage(heights, z, x);
                        var delta = heights[z, x] - trimmedAverage;
                        if (Mathf.Abs(delta) <= spikeThreshold)
                        {
                            buffer[z, x] = heights[z, x];
                            continue;
                        }

                        var strength = zone == SettlementTerrainZone.None ? 0.82f : 0.62f;
                        buffer[z, x] = Mathf.Lerp(heights[z, x], trimmedAverage, strength);
                        if (zone == SettlementTerrainZone.None)
                        {
                            zones[z, x] = (byte)SettlementTerrainZone.OuterSmoothing;
                            zoneWeights[z, x] = 0.22f;
                        }

                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }

                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        heights[z, x] = buffer[z, x];
                    }
                }

                RestorePlatform(heights, zones, platformHeights);
            }
        }

        private static int CountModifiedNeighbors(byte[,] zones, float[,] zoneWeights, int z, int x)
        {
            var count = 0;
            for (var oz = -1; oz <= 1; oz++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oz == 0)
                    {
                        continue;
                    }

                    if ((SettlementTerrainZone)zones[z + oz, x + ox] != SettlementTerrainZone.None ||
                        zoneWeights[z + oz, x + ox] > 0.02f)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static float TrimmedNeighborAverage(float[,] heights, int z, int x)
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            var total = 0f;
            var count = 0;
            for (var oz = -1; oz <= 1; oz++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oz == 0)
                    {
                        continue;
                    }

                    var value = heights[z + oz, x + ox];
                    total += value;
                    count++;
                    min = Mathf.Min(min, value);
                    max = Mathf.Max(max, value);
                }
            }

            if (count <= 2)
            {
                return heights[z, x];
            }

            return (total - min - max) / (count - 2);
        }

        private static bool LimitPair(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            int z,
            int x,
            int nz,
            int nx,
            float spacing,
            float terrainHeight)
        {
            var zoneA = (SettlementTerrainZone)zones[z, x];
            var zoneB = (SettlementTerrainZone)zones[nz, nx];
            if (zoneA == SettlementTerrainZone.Platform && zoneB == SettlementTerrainZone.Platform)
            {
                return false;
            }

            var maxDelta = MaxSettlementEdgeDeltaPerMeter * Mathf.Max(0.1f, spacing) / Mathf.Max(1f, terrainHeight);
            var delta = heights[z, x] - heights[nz, nx];
            var absDelta = Mathf.Abs(delta);
            if (absDelta <= maxDelta)
            {
                return false;
            }

            var excess = absDelta - maxDelta;
            var sign = Mathf.Sign(delta);
            if (zoneA == SettlementTerrainZone.Platform && zoneB != SettlementTerrainZone.Platform)
            {
                heights[nz, nx] = Mathf.Clamp01(heights[nz, nx] + sign * excess);
                PromoteOuterSample(zones, zoneWeights, nz, nx, zoneWeights[z, x]);
                return true;
            }

            if (zoneB == SettlementTerrainZone.Platform && zoneA != SettlementTerrainZone.Platform)
            {
                heights[z, x] = Mathf.Clamp01(heights[z, x] - sign * excess);
                PromoteOuterSample(zones, zoneWeights, z, x, zoneWeights[nz, nx]);
                return true;
            }

            if (zoneA != SettlementTerrainZone.None && zoneB != SettlementTerrainZone.None)
            {
                heights[z, x] = Mathf.Clamp01(heights[z, x] - sign * excess * 0.5f);
                heights[nz, nx] = Mathf.Clamp01(heights[nz, nx] + sign * excess * 0.5f);
                return true;
            }

            if (zoneA != SettlementTerrainZone.None)
            {
                heights[nz, nx] = Mathf.Clamp01(heights[nz, nx] + sign * excess * 0.65f);
                PromoteOuterSample(zones, zoneWeights, nz, nx, zoneWeights[z, x]);
                return true;
            }

            if (zoneB != SettlementTerrainZone.None)
            {
                heights[z, x] = Mathf.Clamp01(heights[z, x] - sign * excess * 0.65f);
                PromoteOuterSample(zones, zoneWeights, z, x, zoneWeights[nz, nx]);
                return true;
            }

            return false;
        }

        private static void PromoteOuterSample(byte[,] zones, float[,] zoneWeights, int z, int x, float sourceWeight)
        {
            var propagatedWeight = Mathf.Clamp01(sourceWeight * 0.55f);
            if (propagatedWeight < 0.035f)
            {
                return;
            }

            if ((SettlementTerrainZone)zones[z, x] != SettlementTerrainZone.None)
            {
                zoneWeights[z, x] = Mathf.Max(zoneWeights[z, x], propagatedWeight);
                return;
            }

            zones[z, x] = (byte)SettlementTerrainZone.OuterSmoothing;
            zoneWeights[z, x] = propagatedWeight;
        }

        private static float NeighborAverage(float[,] heights, int z, int x)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var total = 0f;
            var weightTotal = 0f;
            for (var oz = -1; oz <= 1; oz++)
            {
                var pz = Mathf.Clamp(z + oz, 0, resolutionZ - 1);
                for (var ox = -1; ox <= 1; ox++)
                {
                    var px = Mathf.Clamp(x + ox, 0, resolutionX - 1);
                    var weight = ox == 0 && oz == 0 ? 2.5f : ox == 0 || oz == 0 ? 1.4f : 0.8f;
                    total += heights[pz, px] * weight;
                    weightTotal += weight;
                }
            }

            return weightTotal <= 0f ? heights[z, x] : total / weightTotal;
        }

        private static void RestorePlatform(float[,] heights, byte[,] zones, float[,] platformHeights)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            for (var z = 0; z < resolutionZ; z++)
            {
                for (var x = 0; x < resolutionX; x++)
                {
                    if ((SettlementTerrainZone)zones[z, x] == SettlementTerrainZone.Platform)
                    {
                        heights[z, x] = platformHeights[z, x];
                    }
                }
            }
        }

        private static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }

    public sealed class SettlementAdjustedTerrainSampler : IProceduralTerrainSampler
    {
        private readonly IProceduralTerrainSampler baseSampler;
        private readonly ProceduralLocationSettings settings;
        private readonly IReadOnlyList<GeneratedSettlement> settlements;
        private readonly SettlementTerrainPlatform[] platforms;

        public SettlementAdjustedTerrainSampler(
            IProceduralTerrainSampler baseSampler,
            ProceduralLocationSettings settings,
            IReadOnlyList<GeneratedSettlement> settlements)
        {
            this.baseSampler = baseSampler;
            this.settings = settings;
            this.settlements = settlements ?? Array.Empty<GeneratedSettlement>();
            platforms = SettlementTerrainCarver.BuildPlatformInfos(settings, this.settlements, baseSampler);
        }

        public Bounds WorldBounds => baseSampler != null ? baseSampler.WorldBounds : default;

        public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
        {
            if (baseSampler == null || !baseSampler.TrySample(worldX, worldZ, out point, out _))
            {
                point = default;
                normal = default;
                return false;
            }

            point = new Vector3(worldX, SampleHeightMeters(worldX, worldZ), worldZ);
            normal = SampleNormal(worldX, worldZ);
            return true;
        }

        public float SampleHeight01(float worldX, float worldZ)
        {
            return settings != null && settings.TerrainHeight > 0f
                ? Mathf.Clamp01(SampleHeightMeters(worldX, worldZ) / settings.TerrainHeight)
                : 0f;
        }

        public float SampleHeightMeters(float worldX, float worldZ)
        {
            if (baseSampler == null)
            {
                return 0f;
            }

            var baseHeight = baseSampler.SampleHeightMeters(worldX, worldZ);
            return SettlementTerrainCarver.SampleAdjustedHeightMeters(baseHeight, new Vector2(worldX, worldZ), platforms);
        }

        public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
        {
            var distance = Mathf.Max(0.5f, sampleDistance);
            var left = SampleHeightMeters(worldX - distance, worldZ);
            var right = SampleHeightMeters(worldX + distance, worldZ);
            var down = SampleHeightMeters(worldX, worldZ - distance);
            var up = SampleHeightMeters(worldX, worldZ + distance);
            return new Vector3(left - right, distance * 2f, down - up).normalized;
        }

        public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
        {
            var heights = baseSampler != null
                ? baseSampler.BuildHeightMap(resolution, minX, minZ, width, length)
                : new float[Mathf.Max(2, resolution), Mathf.Max(2, resolution)];
            SettlementTerrainCarver.Apply(heights, minX, minZ, width, length, settings, settlements, baseSampler);
            return heights;
        }
    }

    public readonly struct SettlementTerrainPlatform
    {
        public SettlementTerrainPlatform(
            string settlementId,
            Vector2 center,
            float platformRadius,
            float shoulderWidth,
            float outerSmoothWidth,
            float heightMeters,
            float outerSmoothingStrength)
        {
            SettlementId = settlementId ?? string.Empty;
            Center = center;
            PlatformRadius = Mathf.Max(1f, platformRadius);
            ShoulderWidth = Mathf.Max(0f, shoulderWidth);
            OuterSmoothWidth = Mathf.Max(0f, outerSmoothWidth);
            HeightMeters = heightMeters;
            OuterSmoothingStrength = Mathf.Clamp01(outerSmoothingStrength);
        }

        public string SettlementId { get; }
        public Vector2 Center { get; }
        public float PlatformRadius { get; }
        public float ShoulderWidth { get; }
        public float OuterSmoothWidth { get; }
        public float HeightMeters { get; }
        public float OuterSmoothingStrength { get; }
        public float ShoulderEndRadius => PlatformRadius + ShoulderWidth;
        public float TotalInfluenceRadius => ShoulderEndRadius + OuterSmoothWidth;
    }

    internal enum SettlementTerrainZone
    {
        None,
        Platform,
        Shoulder,
        OuterSmoothing
    }

    internal readonly struct SettlementTerrainSample
    {
        public SettlementTerrainSample(
            SettlementTerrainZone zone,
            float targetHeightMeters,
            float applyWeight,
            float maskWeight,
            float platformHeightMeters)
        {
            Zone = zone;
            TargetHeightMeters = targetHeightMeters;
            ApplyWeight = Mathf.Clamp01(applyWeight);
            MaskWeight = Mathf.Clamp01(maskWeight);
            PlatformHeightMeters = platformHeightMeters;
        }

        public SettlementTerrainZone Zone { get; }
        public float TargetHeightMeters { get; }
        public float ApplyWeight { get; }
        public float MaskWeight { get; }
        public float PlatformHeightMeters { get; }
    }
}
