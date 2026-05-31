using System;
using System.Collections;
using System.Collections.Generic;
using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain
{
    public sealed class RoadTerrainCarvingContext
    {
        private readonly SpatialHashGrid2D<RoadCarvingPath> pathIndex;
        private readonly List<RoadCarvingPath> paths;

        private RoadTerrainCarvingContext(List<RoadCarvingPath> paths, float cellSize, float maxInfluenceRadius)
        {
            this.paths = paths ?? new List<RoadCarvingPath>();
            MaxInfluenceRadius = Mathf.Max(1f, maxInfluenceRadius);
            pathIndex = new SpatialHashGrid2D<RoadCarvingPath>(Mathf.Max(16f, cellSize));
            for (var i = 0; i < this.paths.Count; i++)
            {
                pathIndex.Insert(this.paths[i].Bounds, this.paths[i]);
            }
        }

        public IReadOnlyList<RoadCarvingPath> Paths => paths;
        public float MaxInfluenceRadius { get; }

        public static RoadTerrainCarvingContext Build(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler sampler,
            RoadGenerationConfig roadConfig,
            GeneratedRoadNetwork roadNetwork)
        {
            RoadTerrainCarvingDebug.Clear();
            if (settings == null || sampler == null || roadNetwork?.Segments == null)
            {
                return Empty;
            }

            var config = roadConfig ?? new RoadGenerationConfig();
            var paths = new List<RoadCarvingPath>(roadNetwork.Segments.Count);
            var maxInfluence = 1f;
            for (var i = 0; i < roadNetwork.Segments.Count; i++)
            {
                var segment = roadNetwork.Segments[i];
                if (segment == null || segment.Points.Count < 2)
                {
                    continue;
                }

                var roadType = config.Resolve(segment.Type);
                var path = RoadCarvingPath.Build(segment, roadType, sampler);
                paths.Add(path);
                maxInfluence = Mathf.Max(maxInfluence, path.InfluenceRadius);
            }

            return paths.Count == 0
                ? Empty
                : new RoadTerrainCarvingContext(paths, Mathf.Max(96f, maxInfluence * 2f), maxInfluence);
        }

        public IReadOnlyList<RoadCarvingPath> Query(Bounds2D area)
        {
            return pathIndex.Query(area);
        }

        public static RoadTerrainCarvingContext Empty { get; } = new(new List<RoadCarvingPath>(), 96f, 1f);
    }

    public static class TerrainRoadCarver
    {
        private const double RuntimeFrameBudgetMilliseconds = 4d;

        public static bool Apply(
            float[,] heights,
            float minX,
            float minZ,
            float width,
            float length,
            ProceduralLocationSettings settings,
            RoadTerrainCarvingContext context)
        {
            if (heights == null || settings == null || context == null || context.Paths.Count == 0)
            {
                return false;
            }

            var padding = Mathf.Max(16f, context.MaxInfluenceRadius + 2f);
            var chunkArea = Bounds2D.FromMinMax(
                new Vector2(minX - padding, minZ - padding),
                new Vector2(minX + width + padding, minZ + length + padding));
            var candidates = context.Query(chunkArea);
            if (candidates.Count == 0)
            {
                return false;
            }

            var candidateArray = new RoadCarvingPath[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                candidateArray[i] = candidates[i];
            }

            var changed = false;
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
            var profileHeights = new float[resolutionZ, resolutionX];
            var allowedSlopes = new float[resolutionZ, resolutionX];
            var terrainSmoothingIterations = new int[resolutionZ, resolutionX];
            var gradientLimitIterations = new int[resolutionZ, resolutionX];

            for (var z = 0; z < resolutionZ; z++)
            {
                var worldZ = minZ + length * (z / (float)Mathf.Max(1, resolutionZ - 1));
                for (var x = 0; x < resolutionX; x++)
                {
                    var worldX = minX + width * (x / (float)Mathf.Max(1, resolutionX - 1));
                    var worldPosition = new Vector2(worldX, worldZ);

                    if (!TryFindBestSample(candidateArray, worldPosition, out var best))
                    {
                        continue;
                    }

                    var originalMeters = original[z, x] * terrainHeight;
                    var targetMeters = ResolveTargetHeightMeters(original, z, x, terrainHeight, best);
                    var weight = ResolveApplyWeight(best);
                    var nextMeters = Mathf.Lerp(originalMeters, targetMeters, weight);
                    var nextHeight = Mathf.Clamp01(nextMeters * invTerrainHeight);
                    if (Mathf.Abs(nextHeight - heights[z, x]) > 0.00001f)
                    {
                        next[z, x] = nextHeight;
                        changed = true;
                    }

                    zones[z, x] = (byte)best.Zone;
                    zoneWeights[z, x] = best.MaskWeight;
                    profileHeights[z, x] = Mathf.Clamp01(best.ProfileHeightMeters * invTerrainHeight);
                    allowedSlopes[z, x] = best.Settings.MaxHeightDeltaPerMeter;
                    terrainSmoothingIterations[z, x] = best.Settings.TerrainSmoothingIterations;
                    gradientLimitIterations[z, x] = best.Settings.GradientLimitIterations;
                }
            }

            if (!changed)
            {
                return false;
            }

            var corrections = RoadTerrainSmoothingPass.Apply(
                next,
                original,
                zones,
                zoneWeights,
                profileHeights,
                allowedSlopes,
                terrainSmoothingIterations,
                gradientLimitIterations,
                minX,
                minZ,
                width,
                length,
                spacingX,
                spacingZ,
                terrainHeight);

            for (var z = 0; z < resolutionZ; z++)
            {
                for (var x = 0; x < resolutionX; x++)
                {
                    heights[z, x] = next[z, x];
                }
            }

            RoadTerrainCarvingDebug.RecordModifiedSamples(next, original, zones, minX, minZ, width, length, terrainHeight);
            RoadTerrainCarvingDebug.AddGradientCorrections(corrections);
            return true;
        }

        public static IEnumerator ApplyRoutine(
            float[,] heights,
            float minX,
            float minZ,
            float width,
            float length,
            ProceduralLocationSettings settings,
            RoadTerrainCarvingContext context,
            Action<bool> completed)
        {
            if (heights == null || settings == null || context == null || context.Paths.Count == 0)
            {
                completed?.Invoke(false);
                yield break;
            }

            var padding = Mathf.Max(16f, context.MaxInfluenceRadius + 2f);
            var chunkArea = Bounds2D.FromMinMax(
                new Vector2(minX - padding, minZ - padding),
                new Vector2(minX + width + padding, minZ + length + padding));
            var candidates = context.Query(chunkArea);
            if (candidates.Count == 0)
            {
                completed?.Invoke(false);
                yield break;
            }

            var candidateArray = new RoadCarvingPath[candidates.Count];
            for (var i = 0; i < candidates.Count; i++)
            {
                candidateArray[i] = candidates[i];
            }

            var changed = false;
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
            var profileHeights = new float[resolutionZ, resolutionX];
            var allowedSlopes = new float[resolutionZ, resolutionX];
            var terrainSmoothingIterations = new int[resolutionZ, resolutionX];
            var gradientLimitIterations = new int[resolutionZ, resolutionX];

            var z = 0;
            while (z < resolutionZ)
            {
                using (DebugSessionManager.Profiler.Scope("TerrainRoadCarver.SampleRows", new
                {
                    startZ = z,
                    resolutionZ,
                    candidates = candidateArray.Length
                }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (z < resolutionZ)
                    {
                        var worldZ = minZ + length * (z / (float)Mathf.Max(1, resolutionZ - 1));
                        for (var x = 0; x < resolutionX; x++)
                        {
                            var worldX = minX + width * (x / (float)Mathf.Max(1, resolutionX - 1));
                            var worldPosition = new Vector2(worldX, worldZ);

                            if (!TryFindBestSample(candidateArray, worldPosition, out var best))
                            {
                                continue;
                            }

                            var originalMeters = original[z, x] * terrainHeight;
                            var targetMeters = ResolveTargetHeightMeters(original, z, x, terrainHeight, best);
                            var weight = ResolveApplyWeight(best);
                            var nextMeters = Mathf.Lerp(originalMeters, targetMeters, weight);
                            var nextHeight = Mathf.Clamp01(nextMeters * invTerrainHeight);
                            if (Mathf.Abs(nextHeight - heights[z, x]) > 0.00001f)
                            {
                                next[z, x] = nextHeight;
                                changed = true;
                            }

                            zones[z, x] = (byte)best.Zone;
                            zoneWeights[z, x] = best.MaskWeight;
                            profileHeights[z, x] = Mathf.Clamp01(best.ProfileHeightMeters * invTerrainHeight);
                            allowedSlopes[z, x] = best.Settings.MaxHeightDeltaPerMeter;
                            terrainSmoothingIterations[z, x] = best.Settings.TerrainSmoothingIterations;
                            gradientLimitIterations[z, x] = best.Settings.GradientLimitIterations;
                        }

                        z++;
                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (z < resolutionZ)
                {
                    yield return null;
                }
            }

            if (!changed)
            {
                completed?.Invoke(false);
                yield break;
            }

            IReadOnlyList<RoadGradientCorrectionDebug> corrections = Array.Empty<RoadGradientCorrectionDebug>();
            yield return RoadTerrainSmoothingPass.ApplyRoutine(
                next,
                original,
                zones,
                zoneWeights,
                profileHeights,
                allowedSlopes,
                terrainSmoothingIterations,
                gradientLimitIterations,
                minX,
                minZ,
                width,
                length,
                spacingX,
                spacingZ,
                terrainHeight,
                result => corrections = result);

            z = 0;
            while (z < resolutionZ)
            {
                using (DebugSessionManager.Profiler.Scope("TerrainRoadCarver.CopyRows", new { startZ = z, resolutionZ }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (z < resolutionZ)
                    {
                        for (var x = 0; x < resolutionX; x++)
                        {
                            heights[z, x] = next[z, x];
                        }

                        z++;
                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (z < resolutionZ)
                {
                    yield return null;
                }
            }

            RoadTerrainCarvingDebug.RecordModifiedSamples(next, original, zones, minX, minZ, width, length, terrainHeight);
            RoadTerrainCarvingDebug.AddGradientCorrections(corrections);
            completed?.Invoke(true);
        }

        private static bool TryFindBestSample(RoadCarvingPath[] candidates, Vector2 worldPosition, out RoadCarvingSample best)
        {
            best = default;
            var hasBest = false;
            for (var i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].TrySample(worldPosition, out var sample))
                {
                    continue;
                }

                if (!hasBest || sample.Priority > best.Priority)
                {
                    best = sample;
                    hasBest = true;
                }
            }

            return hasBest;
        }

        private static float ResolveTargetHeightMeters(float[,] original, int z, int x, float terrainHeight, RoadCarvingSample sample)
        {
            var originalMeters = original[z, x] * terrainHeight;
            switch (sample.Zone)
            {
                case RoadCarvingZone.RoadBed:
                    return sample.ProfileHeightMeters;
                case RoadCarvingZone.Shoulder:
                    return Mathf.Lerp(sample.ProfileHeightMeters, originalMeters, sample.Falloff);
                case RoadCarvingZone.OuterSmoothing:
                    return SmoothNeighborhoodHeightMeters(original, z, x, terrainHeight);
                default:
                    return originalMeters;
            }
        }

        private static float ResolveApplyWeight(RoadCarvingSample sample)
        {
            return sample.Zone switch
            {
                RoadCarvingZone.RoadBed => 1f,
                RoadCarvingZone.Shoulder => 1f,
                RoadCarvingZone.OuterSmoothing => sample.MaskWeight * sample.Settings.OuterSmoothingStrength,
                _ => 0f
            };
        }

        private static float SmoothNeighborhoodHeightMeters(float[,] original, int z, int x, float terrainHeight)
        {
            var resolutionZ = original.GetLength(0);
            var resolutionX = original.GetLength(1);
            var total = 0f;
            var weightTotal = 0f;
            for (var oy = -1; oy <= 1; oy++)
            {
                var py = Mathf.Clamp(z + oy, 0, resolutionZ - 1);
                for (var ox = -1; ox <= 1; ox++)
                {
                    var px = Mathf.Clamp(x + ox, 0, resolutionX - 1);
                    var weight = ox == 0 && oy == 0 ? 3f : ox == 0 || oy == 0 ? 2f : 1f;
                    total += original[py, px] * weight;
                    weightTotal += weight;
                }
            }

            return weightTotal <= 0f ? original[z, x] * terrainHeight : total / weightTotal * terrainHeight;
        }
    }

    internal static class RoadTerrainSmoothingPass
    {
        private const double RuntimeFrameBudgetMilliseconds = 4d;

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new(1, 0),
            new(0, 1),
            new(1, 1),
            new(1, -1)
        };

        public static IReadOnlyList<RoadGradientCorrectionDebug> Apply(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] profileHeights,
            float[,] allowedSlopes,
            int[,] terrainSmoothingIterations,
            int[,] gradientLimitIterations,
            float minX,
            float minZ,
            float width,
            float length,
            float spacingX,
            float spacingZ,
            float terrainHeight)
        {
            var maxSmoothIterations = MaxValue(terrainSmoothingIterations);
            if (maxSmoothIterations > 0)
            {
                SmoothModifiedTerrain(
                    heights,
                    original,
                    zones,
                    zoneWeights,
                    profileHeights,
                    terrainSmoothingIterations,
                    maxSmoothIterations);
            }

            var maxGradientIterations = MaxValue(gradientLimitIterations);
            if (maxGradientIterations <= 0)
            {
                return Array.Empty<RoadGradientCorrectionDebug>();
            }

            var corrections = new List<RoadGradientCorrectionDebug>();
            corrections.AddRange(LimitGradients(
                    heights,
                    zones,
                    zoneWeights,
                    allowedSlopes,
                    gradientLimitIterations,
                    maxGradientIterations,
                    minX,
                    minZ,
                    width,
                    length,
                    spacingX,
                    spacingZ,
                    terrainHeight));

            if (maxSmoothIterations > 0)
            {
                SmoothModifiedTerrain(
                    heights,
                    original,
                    zones,
                    zoneWeights,
                    profileHeights,
                    terrainSmoothingIterations,
                    Mathf.Max(1, maxSmoothIterations / 2));
            }

            corrections.AddRange(LimitGradients(
                heights,
                zones,
                zoneWeights,
                allowedSlopes,
                gradientLimitIterations,
                Mathf.Max(1, maxGradientIterations / 2),
                minX,
                minZ,
                width,
                length,
                spacingX,
                spacingZ,
                terrainHeight));

            return corrections;
        }

        public static IEnumerator ApplyRoutine(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] profileHeights,
            float[,] allowedSlopes,
            int[,] terrainSmoothingIterations,
            int[,] gradientLimitIterations,
            float minX,
            float minZ,
            float width,
            float length,
            float spacingX,
            float spacingZ,
            float terrainHeight,
            Action<IReadOnlyList<RoadGradientCorrectionDebug>> completed)
        {
            var maxSmoothIterations = MaxValue(terrainSmoothingIterations);
            if (maxSmoothIterations > 0)
            {
                yield return SmoothModifiedTerrainRoutine(
                    heights,
                    original,
                    zones,
                    zoneWeights,
                    profileHeights,
                    terrainSmoothingIterations,
                    maxSmoothIterations);
            }

            var maxGradientIterations = MaxValue(gradientLimitIterations);
            if (maxGradientIterations <= 0)
            {
                completed?.Invoke(Array.Empty<RoadGradientCorrectionDebug>());
                yield break;
            }

            var corrections = new List<RoadGradientCorrectionDebug>();
            yield return LimitGradientsRoutine(
                heights,
                zones,
                zoneWeights,
                allowedSlopes,
                gradientLimitIterations,
                maxGradientIterations,
                minX,
                minZ,
                width,
                length,
                spacingX,
                spacingZ,
                terrainHeight,
                corrections);

            if (maxSmoothIterations > 0)
            {
                yield return SmoothModifiedTerrainRoutine(
                    heights,
                    original,
                    zones,
                    zoneWeights,
                    profileHeights,
                    terrainSmoothingIterations,
                    Mathf.Max(1, maxSmoothIterations / 2));
            }

            yield return LimitGradientsRoutine(
                heights,
                zones,
                zoneWeights,
                allowedSlopes,
                gradientLimitIterations,
                Mathf.Max(1, maxGradientIterations / 2),
                minX,
                minZ,
                width,
                length,
                spacingX,
                spacingZ,
                terrainHeight,
                corrections);

            completed?.Invoke(corrections);
        }

        private static void SmoothModifiedTerrain(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] profileHeights,
            int[,] terrainSmoothingIterations,
            int maxIterations)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var buffer = (float[,])heights.Clone();
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        var zone = (RoadCarvingZone)zones[z, x];
                        if (zone == RoadCarvingZone.None || terrainSmoothingIterations[z, x] <= iteration)
                        {
                            buffer[z, x] = heights[z, x];
                            continue;
                        }

                        if (zone == RoadCarvingZone.RoadBed)
                        {
                            buffer[z, x] = profileHeights[z, x];
                            continue;
                        }

                        var average = NeighborAverage(heights, z, x);
                        var baseStrength = zone == RoadCarvingZone.Shoulder ? 0.34f : 0.18f;
                        var strength = baseStrength * Mathf.Clamp01(zoneWeights[z, x]);
                        var smoothed = Mathf.Lerp(heights[z, x], average, strength);
                        if (zone == RoadCarvingZone.Shoulder)
                        {
                            var falloffToOriginal = Mathf.Clamp01(1f - zoneWeights[z, x]);
                            smoothed = Mathf.Lerp(smoothed, original[z, x], falloffToOriginal * 0.18f);
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

                RestoreRoadBed(heights, zones, profileHeights);
            }
        }

        private static IEnumerator SmoothModifiedTerrainRoutine(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] profileHeights,
            int[,] terrainSmoothingIterations,
            int maxIterations)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var buffer = (float[,])heights.Clone();
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var z = 0;
                while (z < resolutionZ)
                {
                    using (DebugSessionManager.Profiler.Scope("RoadTerrainSmoothing.SmoothRows", new
                    {
                        iteration,
                        maxIterations,
                        startZ = z
                    }))
                    {
                        var sliceStopwatch = Stopwatch.StartNew();
                        while (z < resolutionZ)
                        {
                            for (var x = 0; x < resolutionX; x++)
                            {
                                var zone = (RoadCarvingZone)zones[z, x];
                                if (zone == RoadCarvingZone.None || terrainSmoothingIterations[z, x] <= iteration)
                                {
                                    buffer[z, x] = heights[z, x];
                                    continue;
                                }

                                if (zone == RoadCarvingZone.RoadBed)
                                {
                                    buffer[z, x] = profileHeights[z, x];
                                    continue;
                                }

                                var average = NeighborAverage(heights, z, x);
                                var baseStrength = zone == RoadCarvingZone.Shoulder ? 0.34f : 0.18f;
                                var strength = baseStrength * Mathf.Clamp01(zoneWeights[z, x]);
                                var smoothed = Mathf.Lerp(heights[z, x], average, strength);
                                if (zone == RoadCarvingZone.Shoulder)
                                {
                                    var falloffToOriginal = Mathf.Clamp01(1f - zoneWeights[z, x]);
                                    smoothed = Mathf.Lerp(smoothed, original[z, x], falloffToOriginal * 0.18f);
                                }

                                buffer[z, x] = smoothed;
                            }

                            z++;
                            if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                            {
                                break;
                            }
                        }
                    }

                    if (z < resolutionZ)
                    {
                        yield return null;
                    }
                }

                yield return CopyRowsRoutine(buffer, heights, "RoadTerrainSmoothing.CopyRows");
                yield return RestoreRoadBedRoutine(heights, zones, profileHeights);
            }
        }

        private static IReadOnlyList<RoadGradientCorrectionDebug> LimitGradients(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] allowedSlopes,
            int[,] gradientLimitIterations,
            int maxIterations,
            float minX,
            float minZ,
            float width,
            float length,
            float spacingX,
            float spacingZ,
            float terrainHeight)
        {
            var corrections = new List<RoadGradientCorrectionDebug>();
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var changed = false;
                for (var z = 0; z < resolutionZ; z++)
                {
                    for (var x = 0; x < resolutionX; x++)
                    {
                        var zone = (RoadCarvingZone)zones[z, x];
                        if (zone == RoadCarvingZone.None && zoneWeights[z, x] <= 0f)
                        {
                            continue;
                        }

                        for (var i = 0; i < NeighborOffsets.Length; i++)
                        {
                            var nx = x + NeighborOffsets[i].x;
                            var nz = z + NeighborOffsets[i].y;
                            if (nx < 0 || nz < 0 || nx >= resolutionX || nz >= resolutionZ)
                            {
                                continue;
                            }

                            var iterations = Mathf.Max(gradientLimitIterations[z, x], gradientLimitIterations[nz, nx]);
                            if (iterations <= iteration)
                            {
                                continue;
                            }

                            var spacing = NeighborOffsets[i].x != 0 && NeighborOffsets[i].y != 0
                                ? Mathf.Sqrt(spacingX * spacingX + spacingZ * spacingZ)
                                : NeighborOffsets[i].x != 0
                                    ? spacingX
                                    : spacingZ;
                            if (LimitPair(
                                    heights,
                                    zones,
                                    zoneWeights,
                                    allowedSlopes,
                                    gradientLimitIterations,
                                    z,
                                    x,
                                    nz,
                                    nx,
                                    spacing,
                                    terrainHeight,
                                    minX,
                                    minZ,
                                    width,
                                    length,
                                    corrections))
                            {
                                changed = true;
                            }
                        }
                    }
                }

                if (!changed)
                {
                    break;
                }
            }

            return corrections;
        }

        private static IEnumerator LimitGradientsRoutine(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] allowedSlopes,
            int[,] gradientLimitIterations,
            int maxIterations,
            float minX,
            float minZ,
            float width,
            float length,
            float spacingX,
            float spacingZ,
            float terrainHeight,
            List<RoadGradientCorrectionDebug> corrections)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            for (var iteration = 0; iteration < maxIterations; iteration++)
            {
                var changed = false;
                var z = 0;
                while (z < resolutionZ)
                {
                    using (DebugSessionManager.Profiler.Scope("RoadTerrainSmoothing.LimitGradientRows", new
                    {
                        iteration,
                        maxIterations,
                        startZ = z
                    }))
                    {
                        var sliceStopwatch = Stopwatch.StartNew();
                        while (z < resolutionZ)
                        {
                            for (var x = 0; x < resolutionX; x++)
                            {
                                var zone = (RoadCarvingZone)zones[z, x];
                                if (zone == RoadCarvingZone.None && zoneWeights[z, x] <= 0f)
                                {
                                    continue;
                                }

                                for (var i = 0; i < NeighborOffsets.Length; i++)
                                {
                                    var nx = x + NeighborOffsets[i].x;
                                    var nz = z + NeighborOffsets[i].y;
                                    if (nx < 0 || nz < 0 || nx >= resolutionX || nz >= resolutionZ)
                                    {
                                        continue;
                                    }

                                    var iterations = Mathf.Max(gradientLimitIterations[z, x], gradientLimitIterations[nz, nx]);
                                    if (iterations <= iteration)
                                    {
                                        continue;
                                    }

                                    var spacing = NeighborOffsets[i].x != 0 && NeighborOffsets[i].y != 0
                                        ? Mathf.Sqrt(spacingX * spacingX + spacingZ * spacingZ)
                                        : NeighborOffsets[i].x != 0
                                            ? spacingX
                                            : spacingZ;
                                    if (LimitPair(
                                            heights,
                                            zones,
                                            zoneWeights,
                                            allowedSlopes,
                                            gradientLimitIterations,
                                            z,
                                            x,
                                            nz,
                                            nx,
                                            spacing,
                                            terrainHeight,
                                            minX,
                                            minZ,
                                            width,
                                            length,
                                            corrections))
                                    {
                                        changed = true;
                                    }
                                }
                            }

                            z++;
                            if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                            {
                                break;
                            }
                        }
                    }

                    if (z < resolutionZ)
                    {
                        yield return null;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        private static bool LimitPair(
            float[,] heights,
            byte[,] zones,
            float[,] zoneWeights,
            float[,] allowedSlopes,
            int[,] gradientLimitIterations,
            int z,
            int x,
            int nz,
            int nx,
            float spacing,
            float terrainHeight,
            float minX,
            float minZ,
            float width,
            float length,
            List<RoadGradientCorrectionDebug> corrections)
        {
            var zoneA = (RoadCarvingZone)zones[z, x];
            var zoneB = (RoadCarvingZone)zones[nz, nx];
            if (zoneA == RoadCarvingZone.RoadBed && zoneB == RoadCarvingZone.RoadBed)
            {
                return false;
            }

            var slope = Mathf.Max(ResolveAllowedSlope(allowedSlopes[z, x]), ResolveAllowedSlope(allowedSlopes[nz, nx]));
            var maxDelta = slope * Mathf.Max(0.1f, spacing) / Mathf.Max(1f, terrainHeight);
            var delta = heights[z, x] - heights[nz, nx];
            var absDelta = Mathf.Abs(delta);
            if (absDelta <= maxDelta)
            {
                return false;
            }

            var excess = absDelta - maxDelta;
            var sign = Mathf.Sign(delta);
            var correctionA = 0f;
            var correctionB = 0f;
            if (zoneA == RoadCarvingZone.RoadBed && zoneB != RoadCarvingZone.RoadBed)
            {
                correctionB = sign * excess;
            }
            else if (zoneB == RoadCarvingZone.RoadBed && zoneA != RoadCarvingZone.RoadBed)
            {
                correctionA = -sign * excess;
            }
            else if (zoneA != RoadCarvingZone.None && zoneB != RoadCarvingZone.None)
            {
                correctionA = -sign * excess * 0.5f;
                correctionB = sign * excess * 0.5f;
            }
            else if (zoneA != RoadCarvingZone.None)
            {
                correctionB = sign * excess * 0.55f;
            }
            else if (zoneB != RoadCarvingZone.None)
            {
                correctionA = -sign * excess * 0.55f;
            }
            else
            {
                return false;
            }

            if (correctionA != 0f)
            {
                heights[z, x] = Mathf.Clamp01(heights[z, x] + correctionA);
                EnsureGradientZone(zones, zoneWeights, allowedSlopes, gradientLimitIterations, z, x, slope);
                AddCorrection(corrections, x, z, heights.GetLength(1), heights.GetLength(0), minX, minZ, width, length, Mathf.Abs(correctionA) * terrainHeight);
            }

            if (correctionB != 0f)
            {
                heights[nz, nx] = Mathf.Clamp01(heights[nz, nx] + correctionB);
                EnsureGradientZone(zones, zoneWeights, allowedSlopes, gradientLimitIterations, nz, nx, slope);
                AddCorrection(corrections, nx, nz, heights.GetLength(1), heights.GetLength(0), minX, minZ, width, length, Mathf.Abs(correctionB) * terrainHeight);
            }

            return true;
        }

        private static void EnsureGradientZone(
            byte[,] zones,
            float[,] zoneWeights,
            float[,] allowedSlopes,
            int[,] gradientLimitIterations,
            int z,
            int x,
            float slope)
        {
            if ((RoadCarvingZone)zones[z, x] == RoadCarvingZone.None)
            {
                zones[z, x] = (byte)RoadCarvingZone.OuterSmoothing;
                zoneWeights[z, x] = Mathf.Max(zoneWeights[z, x], 0.18f);
                gradientLimitIterations[z, x] = Mathf.Max(gradientLimitIterations[z, x], 1);
            }

            allowedSlopes[z, x] = Mathf.Max(allowedSlopes[z, x], slope);
        }

        private static float NeighborAverage(float[,] heights, int z, int x)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var total = 0f;
            var weightTotal = 0f;
            for (var oy = -1; oy <= 1; oy++)
            {
                var py = Mathf.Clamp(z + oy, 0, resolutionZ - 1);
                for (var ox = -1; ox <= 1; ox++)
                {
                    var px = Mathf.Clamp(x + ox, 0, resolutionX - 1);
                    var weight = ox == 0 && oy == 0 ? 2f : ox == 0 || oy == 0 ? 1.25f : 0.75f;
                    total += heights[py, px] * weight;
                    weightTotal += weight;
                }
            }

            return weightTotal <= 0f ? heights[z, x] : total / weightTotal;
        }

        private static void RestoreRoadBed(float[,] heights, byte[,] zones, float[,] profileHeights)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            for (var z = 0; z < resolutionZ; z++)
            {
                for (var x = 0; x < resolutionX; x++)
                {
                    if ((RoadCarvingZone)zones[z, x] == RoadCarvingZone.RoadBed)
                    {
                        heights[z, x] = profileHeights[z, x];
                    }
                }
            }
        }

        private static IEnumerator RestoreRoadBedRoutine(float[,] heights, byte[,] zones, float[,] profileHeights)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            var z = 0;
            while (z < resolutionZ)
            {
                using (DebugSessionManager.Profiler.Scope("RoadTerrainSmoothing.RestoreRoadBedRows", new
                {
                    startZ = z,
                    resolutionZ
                }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (z < resolutionZ)
                    {
                        for (var x = 0; x < resolutionX; x++)
                        {
                            if ((RoadCarvingZone)zones[z, x] == RoadCarvingZone.RoadBed)
                            {
                                heights[z, x] = profileHeights[z, x];
                            }
                        }

                        z++;
                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (z < resolutionZ)
                {
                    yield return null;
                }
            }
        }

        private static IEnumerator CopyRowsRoutine(float[,] source, float[,] target, string scopeName)
        {
            var resolutionZ = source.GetLength(0);
            var resolutionX = source.GetLength(1);
            var z = 0;
            while (z < resolutionZ)
            {
                using (DebugSessionManager.Profiler.Scope(scopeName, new
                {
                    startZ = z,
                    resolutionZ
                }))
                {
                    var sliceStopwatch = Stopwatch.StartNew();
                    while (z < resolutionZ)
                    {
                        for (var x = 0; x < resolutionX; x++)
                        {
                            target[z, x] = source[z, x];
                        }

                        z++;
                        if (sliceStopwatch.Elapsed.TotalMilliseconds >= RuntimeFrameBudgetMilliseconds)
                        {
                            break;
                        }
                    }
                }

                if (z < resolutionZ)
                {
                    yield return null;
                }
            }
        }

        private static int MaxValue(int[,] values)
        {
            var max = 0;
            var resolutionZ = values.GetLength(0);
            var resolutionX = values.GetLength(1);
            for (var z = 0; z < resolutionZ; z++)
            {
                for (var x = 0; x < resolutionX; x++)
                {
                    max = Mathf.Max(max, values[z, x]);
                }
            }

            return max;
        }

        private static float ResolveAllowedSlope(float value)
        {
            return value > 0f ? value : 0.35f;
        }

        private static void AddCorrection(
            List<RoadGradientCorrectionDebug> corrections,
            int x,
            int z,
            int resolutionX,
            int resolutionZ,
            float minX,
            float minZ,
            float width,
            float length,
            float deltaMeters)
        {
            if (corrections.Count >= RoadTerrainCarvingDebug.MaxGradientCorrectionDebugPoints)
            {
                return;
            }

            corrections.Add(new RoadGradientCorrectionDebug(
                new Vector3(
                    minX + width * (x / (float)Mathf.Max(1, resolutionX - 1)),
                    1.25f,
                    minZ + length * (z / (float)Mathf.Max(1, resolutionZ - 1))),
                deltaMeters));
        }
    }

    public sealed class RoadCarvingPath
    {
        private readonly float[] pathDistances;
        private readonly float[] profileDistances;
        private readonly float[] profileHeights;

        private RoadCarvingPath(
            GeneratedRoadSegment segment,
            RoadTypeSettings settings,
            float[] pathDistances,
            float[] profileDistances,
            float[] profileHeights)
        {
            Segment = segment;
            Settings = settings;
            this.pathDistances = pathDistances;
            this.profileDistances = profileDistances;
            this.profileHeights = profileHeights;
            RoadBedHalfWidth = Segment.Width * 0.5f + Settings.HeightBedExtraWidth;
            ShoulderEndDistance = RoadBedHalfWidth + Settings.ShoulderWidth;
            InfluenceRadius = ShoulderEndDistance + Settings.OuterSmoothWidth;
            Bounds = BuildBounds(segment.Points, InfluenceRadius + 2f);
        }

        public GeneratedRoadSegment Segment { get; }
        public RoadTypeSettings Settings { get; }
        public float RoadBedHalfWidth { get; }
        public float ShoulderEndDistance { get; }
        public float InfluenceRadius { get; }
        public Bounds2D Bounds { get; }

        public static RoadCarvingPath Build(GeneratedRoadSegment segment, RoadTypeSettings settings, IProceduralTerrainSampler sampler)
        {
            var pathDistances = BuildPathDistances(segment.Points);
            var totalDistance = pathDistances[pathDistances.Length - 1];
            var sampleCount = Mathf.Max(2, Mathf.CeilToInt(totalDistance / settings.ProfileSampleStepMeters) + 1);
            var profileDistances = new float[sampleCount];
            var profileHeights = new float[sampleCount];
            var profilePoints = new Vector2[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var distance = sampleCount == 1 ? 0f : totalDistance * (i / (float)(sampleCount - 1));
                var point = EvaluatePointAtDistance(segment.Points, pathDistances, distance);
                profileDistances[i] = distance;
                profilePoints[i] = point;
                profileHeights[i] = sampler.SampleHeightMeters(point.x, point.y);
            }

            SmoothProfile(profileHeights, profileDistances, settings);
            for (var i = 0; i < sampleCount; i++)
            {
                RoadTerrainCarvingDebug.AddProfileSample(new Vector3(profilePoints[i].x, profileHeights[i], profilePoints[i].y), segment.Type);
            }

            return new RoadCarvingPath(segment, settings, pathDistances, profileDistances, profileHeights);
        }

        public bool TrySample(Vector2 point, out RoadCarvingSample sample)
        {
            sample = default;
            if (!Bounds.Contains(point) || Segment.Points.Count < 2)
            {
                return false;
            }

            var bestDistanceSqr = float.MaxValue;
            var bestSegment = -1;
            var bestT = 0f;
            for (var i = 1; i < Segment.Points.Count; i++)
            {
                var t = ClosestSegmentT(point, Segment.Points[i - 1], Segment.Points[i]);
                var closest = Vector2.Lerp(Segment.Points[i - 1], Segment.Points[i], t);
                var distanceSqr = (point - closest).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    bestSegment = i;
                    bestT = t;
                }
            }

            if (bestSegment < 1)
            {
                return false;
            }

            var distanceToCenter = Mathf.Sqrt(bestDistanceSqr);
            if (distanceToCenter >= InfluenceRadius)
            {
                return false;
            }

            var distanceAlongPath = Mathf.Lerp(pathDistances[bestSegment - 1], pathDistances[bestSegment], bestT);
            var profileHeight = EvaluateProfileHeight(distanceAlongPath);
            if (distanceToCenter <= RoadBedHalfWidth)
            {
                sample = new RoadCarvingSample(
                    RoadCarvingZone.RoadBed,
                    distanceToCenter,
                    0f,
                    1f,
                    1f,
                    profileHeight,
                    Settings);
                return true;
            }

            if (distanceToCenter <= ShoulderEndDistance)
            {
                var t = Smooth01(Mathf.InverseLerp(RoadBedHalfWidth, ShoulderEndDistance, distanceToCenter));
                var maskWeight = 1f - t;
                sample = new RoadCarvingSample(
                    RoadCarvingZone.Shoulder,
                    distanceToCenter,
                    t,
                    maskWeight,
                    0.65f + maskWeight * 0.30f,
                    profileHeight,
                    Settings);
                return true;
            }

            var outerT = Smooth01(Mathf.InverseLerp(ShoulderEndDistance, InfluenceRadius, distanceToCenter));
            var outerWeight = Mathf.Pow(1f - outerT, 2f);
            sample = new RoadCarvingSample(
                RoadCarvingZone.OuterSmoothing,
                distanceToCenter,
                outerT,
                outerWeight,
                outerWeight * 0.35f,
                profileHeight,
                Settings);
            return true;
        }

        private float EvaluateProfileHeight(float distance)
        {
            if (profileDistances.Length == 0)
            {
                return 0f;
            }

            if (distance <= profileDistances[0])
            {
                return profileHeights[0];
            }

            var last = profileDistances.Length - 1;
            if (distance >= profileDistances[last])
            {
                return profileHeights[last];
            }

            for (var i = 1; i < profileDistances.Length; i++)
            {
                if (distance > profileDistances[i])
                {
                    continue;
                }

                var t = Mathf.InverseLerp(profileDistances[i - 1], profileDistances[i], distance);
                return Mathf.Lerp(profileHeights[i - 1], profileHeights[i], Smooth01(t));
            }

            return profileHeights[last];
        }

        private static float[] BuildPathDistances(IReadOnlyList<Vector2> points)
        {
            var distances = new float[points.Count];
            for (var i = 1; i < points.Count; i++)
            {
                distances[i] = distances[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            }

            return distances;
        }

        private static Vector2 EvaluatePointAtDistance(IReadOnlyList<Vector2> points, float[] distances, float distance)
        {
            if (points.Count == 0)
            {
                return Vector2.zero;
            }

            if (distance <= 0f)
            {
                return points[0];
            }

            var last = points.Count - 1;
            if (distance >= distances[last])
            {
                return points[last];
            }

            for (var i = 1; i < points.Count; i++)
            {
                if (distance > distances[i])
                {
                    continue;
                }

                var t = Mathf.InverseLerp(distances[i - 1], distances[i], distance);
                return Vector2.Lerp(points[i - 1], points[i], t);
            }

            return points[last];
        }

        private static void SmoothProfile(float[] heights, float[] distances, RoadTypeSettings settings)
        {
            if (heights.Length <= 2)
            {
                return;
            }

            var iterations = Mathf.Max(0, settings.HeightSmoothingIterations + Mathf.RoundToInt(settings.Smoothing * 3f));
            var buffer = new float[heights.Length];
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                SmoothProfileByRadius(heights, distances, settings.ProfileSmoothingRadiusMeters, buffer);
                Array.Copy(buffer, heights, heights.Length);
                ClampLongitudinalSlope(heights, distances, settings.MaxLongitudinalSlope);
            }

            ClampLongitudinalSlope(heights, distances, settings.MaxLongitudinalSlope);
        }

        private static void SmoothProfileByRadius(float[] heights, float[] distances, float radius, float[] buffer)
        {
            var safeRadius = Mathf.Max(0.5f, radius);
            for (var i = 0; i < heights.Length; i++)
            {
                var total = 0f;
                var weightTotal = 0f;
                for (var j = 0; j < heights.Length; j++)
                {
                    var distance = Mathf.Abs(distances[j] - distances[i]);
                    if (distance > safeRadius)
                    {
                        continue;
                    }

                    var t = distance / safeRadius;
                    var weight = Mathf.Exp(-t * t * 3.0f);
                    total += heights[j] * weight;
                    weightTotal += weight;
                }

                var averaged = weightTotal <= 0f ? heights[i] : total / weightTotal;
                buffer[i] = Mathf.Lerp(heights[i], averaged, 0.86f);
            }
        }

        private static void ClampLongitudinalSlope(float[] heights, float[] distances, float maxSlope)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 1; i < heights.Length; i++)
                {
                    var maxDelta = Mathf.Max(0.05f, (distances[i] - distances[i - 1]) * maxSlope);
                    heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - maxDelta, heights[i - 1] + maxDelta);
                }

                for (var i = heights.Length - 2; i >= 0; i--)
                {
                    var maxDelta = Mathf.Max(0.05f, (distances[i + 1] - distances[i]) * maxSlope);
                    heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - maxDelta, heights[i + 1] + maxDelta);
                }
            }
        }

        private static Bounds2D BuildBounds(IReadOnlyList<Vector2> points, float padding)
        {
            var min = points[0];
            var max = points[0];
            for (var i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            var pad = new Vector2(padding, padding);
            return Bounds2D.FromMinMax(min - pad, max + pad);
        }

        private static float ClosestSegmentT(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
        }

        private static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }

    public enum RoadCarvingZone
    {
        None,
        RoadBed,
        Shoulder,
        OuterSmoothing
    }

    public readonly struct RoadCarvingSample
    {
        public RoadCarvingSample(
            RoadCarvingZone zone,
            float distanceToCenter,
            float falloff,
            float maskWeight,
            float priority,
            float profileHeightMeters,
            RoadTypeSettings settings)
        {
            Zone = zone;
            DistanceToCenter = distanceToCenter;
            Falloff = Mathf.Clamp01(falloff);
            MaskWeight = Mathf.Clamp01(maskWeight);
            Priority = priority;
            ProfileHeightMeters = profileHeightMeters;
            Settings = settings;
        }

        public RoadCarvingZone Zone { get; }
        public float DistanceToCenter { get; }
        public float Falloff { get; }
        public float MaskWeight { get; }
        public float Priority { get; }
        public float ProfileHeightMeters { get; }
        public RoadTypeSettings Settings { get; }
    }

    public readonly struct RoadTerrainDebugPoint
    {
        public RoadTerrainDebugPoint(Vector3 position, RoadType roadType)
        {
            Position = position;
            RoadType = roadType;
        }

        public Vector3 Position { get; }
        public RoadType RoadType { get; }
    }

    public readonly struct RoadModifiedHeightDebug
    {
        public RoadModifiedHeightDebug(Vector3 position, RoadCarvingZone zone, float deltaMeters)
        {
            Position = position;
            Zone = zone;
            DeltaMeters = deltaMeters;
        }

        public Vector3 Position { get; }
        public RoadCarvingZone Zone { get; }
        public float DeltaMeters { get; }
    }

    public readonly struct RoadGradientCorrectionDebug
    {
        public RoadGradientCorrectionDebug(Vector3 position, float deltaMeters)
        {
            Position = position;
            DeltaMeters = deltaMeters;
        }

        public Vector3 Position { get; }
        public float DeltaMeters { get; }
    }

    public static class RoadTerrainCarvingDebug
    {
        public const int MaxProfileDebugPoints = 1800;
        public const int MaxModifiedDebugPoints = 2200;
        public const int MaxGradientCorrectionDebugPoints = 1200;

        private static readonly List<RoadTerrainDebugPoint> ProfileSamples = new();
        private static readonly List<RoadModifiedHeightDebug> ModifiedSamples = new();
        private static readonly List<RoadGradientCorrectionDebug> GradientCorrections = new();

        public static IReadOnlyList<RoadTerrainDebugPoint> LastProfileSamples => ProfileSamples;
        public static IReadOnlyList<RoadModifiedHeightDebug> LastModifiedSamples => ModifiedSamples;
        public static IReadOnlyList<RoadGradientCorrectionDebug> LastGradientCorrections => GradientCorrections;
        public static int CurrentResolutionX { get; private set; } = 2;
        public static int CurrentResolutionZ { get; private set; } = 2;

        public static void Clear()
        {
            ProfileSamples.Clear();
            ModifiedSamples.Clear();
            GradientCorrections.Clear();
            CurrentResolutionX = 2;
            CurrentResolutionZ = 2;
        }

        public static void AddProfileSample(Vector3 position, RoadType roadType)
        {
            if (ProfileSamples.Count >= MaxProfileDebugPoints)
            {
                return;
            }

            ProfileSamples.Add(new RoadTerrainDebugPoint(position, roadType));
        }

        public static void RecordModifiedSamples(
            float[,] heights,
            float[,] original,
            byte[,] zones,
            float minX,
            float minZ,
            float width,
            float length,
            float terrainHeight)
        {
            var resolutionZ = heights.GetLength(0);
            var resolutionX = heights.GetLength(1);
            CurrentResolutionX = resolutionX;
            CurrentResolutionZ = resolutionZ;
            var stride = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(resolutionX * resolutionZ / 700f)));
            for (var z = 0; z < resolutionZ; z += stride)
            {
                var worldZ = minZ + length * (z / (float)Mathf.Max(1, resolutionZ - 1));
                for (var x = 0; x < resolutionX; x += stride)
                {
                    if (ModifiedSamples.Count >= MaxModifiedDebugPoints)
                    {
                        return;
                    }

                    var zone = (RoadCarvingZone)zones[z, x];
                    if (zone == RoadCarvingZone.None)
                    {
                        continue;
                    }

                    var deltaMeters = Mathf.Abs(heights[z, x] - original[z, x]) * terrainHeight;
                    if (deltaMeters < 0.04f)
                    {
                        continue;
                    }

                    var worldX = minX + width * (x / (float)Mathf.Max(1, resolutionX - 1));
                    ModifiedSamples.Add(new RoadModifiedHeightDebug(
                        new Vector3(worldX, heights[z, x] * terrainHeight + 0.5f, worldZ),
                        zone,
                        deltaMeters));
                }
            }
        }

        public static void AddGradientCorrections(IReadOnlyList<RoadGradientCorrectionDebug> corrections)
        {
            if (corrections == null)
            {
                return;
            }

            for (var i = 0; i < corrections.Count && GradientCorrections.Count < MaxGradientCorrectionDebugPoints; i++)
            {
                GradientCorrections.Add(corrections[i]);
            }
        }
    }
}
