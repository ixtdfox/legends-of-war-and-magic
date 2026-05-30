using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Generation
{
    public sealed class TerrainAwareRoadPathfinder
    {
        private static readonly Vector2Int[] Neighbors =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
            new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
            new(2, 1), new(2, -1), new(-2, 1), new(-2, -1),
            new(1, 2), new(1, -2), new(-1, 2), new(-1, -2)
        };

        public RoadPathResult FindPath(
            Vector2 start,
            Vector2 end,
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler sampler,
            WorldGenerationMaskSet masks,
            RoadGenerationConfig config,
            RoadTypeSettings roadTypeSettings)
        {
            var resolution = config.PathfindingGridResolution;
            var bounds = settings.GetWorldBounds();
            var startCell = WorldToCell(start, bounds, resolution);
            var endCell = WorldToCell(end, bounds, resolution);
            var cellCount = resolution * resolution;
            var records = new NodeRecord[cellCount];
            var open = new List<int> { ToIndex(startCell, resolution) };
            var closed = new bool[cellCount];
            records[open[0]] = new NodeRecord(0f, Heuristic(startCell, endCell), -1, true);

            while (open.Count > 0)
            {
                var currentOpenIndex = ResolveLowestCostIndex(open, records);
                var currentIndex = open[currentOpenIndex];
                open.RemoveAt(currentOpenIndex);

                if (closed[currentIndex])
                {
                    continue;
                }

                closed[currentIndex] = true;
                var currentCell = FromIndex(currentIndex, resolution);
                if (currentCell == endCell)
                {
                    return BuildResult(currentIndex, records, bounds, resolution, start, end, true);
                }

                for (var i = 0; i < Neighbors.Length; i++)
                {
                    var nextCell = currentCell + Neighbors[i];
                    if (!IsInside(nextCell, resolution))
                    {
                        continue;
                    }

                    var nextIndex = ToIndex(nextCell, resolution);
                    if (closed[nextIndex])
                    {
                        continue;
                    }

                    var stepCost = EvaluateStepCost(
                        currentCell,
                        nextCell,
                        start,
                        end,
                        bounds,
                        resolution,
                        settings,
                        sampler,
                        masks,
                        config,
                        roadTypeSettings);
                    if (float.IsPositiveInfinity(stepCost))
                    {
                        continue;
                    }

                    var newCost = records[currentIndex].CostFromStart + stepCost;
                    if (records[nextIndex].Opened && newCost >= records[nextIndex].CostFromStart)
                    {
                        continue;
                    }

                    records[nextIndex] = new NodeRecord(newCost, newCost + Heuristic(nextCell, endCell), currentIndex, true);
                    open.Add(nextIndex);
                }
            }

            return BuildFallback(start, end);
        }

        public float EvaluateStepCostForTests(
            float distance,
            float slopeDegrees,
            bool water,
            RoadGenerationConfig config,
            RoadTypeSettings roadTypeSettings)
        {
            var slope01 = Mathf.InverseLerp(0f, 45f, slopeDegrees);
            var steepness = Mathf.Tan(slopeDegrees * Mathf.Deg2Rad);
            return distance * roadTypeSettings.TerrainCostMultiplier +
                   Mathf.Pow(steepness * config.SlopePenalty, config.SlopePower) * distance +
                   Mathf.Pow(slope01, 2f) * config.VerySteepSlopePenalty * distance +
                   (water ? config.WaterPenalty : 0f);
        }

        private static float EvaluateStepCost(
            Vector2Int from,
            Vector2Int to,
            Vector2 start,
            Vector2 end,
            Bounds bounds,
            int resolution,
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler sampler,
            WorldGenerationMaskSet masks,
            RoadGenerationConfig config,
            RoadTypeSettings roadTypeSettings)
        {
            var fromWorld = CellToWorld(from, bounds, resolution);
            var toWorld = CellToWorld(to, bounds, resolution);
            var distance = Vector2.Distance(fromWorld, toWorld);
            if (!sampler.TrySample(fromWorld.x, fromWorld.y, out var fromPoint, out _) ||
                !sampler.TrySample(toWorld.x, toWorld.y, out var toPoint, out var normal))
            {
                return float.PositiveInfinity;
            }

            var slope = Vector3.Angle(normal, Vector3.up);
            var water = settings.WaterEnabled && toPoint.y <= settings.WaterLevel + 0.35f;
            var steepness = Mathf.Abs(toPoint.y - fromPoint.y) / Mathf.Max(0.01f, distance);
            var heightChange = Mathf.Abs(toPoint.y - fromPoint.y);
            var cost = distance * roadTypeSettings.TerrainCostMultiplier +
                       Mathf.Pow(steepness * config.SlopePenalty, config.SlopePower) * distance +
                       Mathf.Pow(heightChange / Mathf.Max(1f, distance), 2f) * config.ElevationChangePenalty * distance;

            if (slope > 50f)
            {
                cost += config.VerySteepSlopePenalty * distance * Mathf.Pow(Mathf.InverseLerp(50f, 75f, slope), 2f);
            }

            var maxSlopeDegrees = Mathf.Atan(roadTypeSettings.MaxLongitudinalSlope) * Mathf.Rad2Deg;
            if (slope > maxSlopeDegrees)
            {
                cost += config.SlopePenalty * distance * Mathf.Pow(Mathf.InverseLerp(maxSlopeDegrees, 65f, slope), 2f);
            }

            if (water)
            {
                cost += config.WaterPenalty;
            }

            if (masks != null)
            {
                var nearEndpoint = Vector2.Distance(toWorld, start) < 35f || Vector2.Distance(toWorld, end) < 35f;
                if (!nearEndpoint && masks.IsNoSpawn(toWorld))
                {
                    cost += config.ForbiddenZonePenalty;
                }

                var existingRoad = masks.Evaluate(toWorld, GenerationZoneKind.Road);
                if (existingRoad > 0.05f)
                {
                    cost -= config.ExistingRoadBonus * existingRoad;
                }
            }

            return Mathf.Max(distance * 0.12f, cost);
        }

        private static RoadPathResult BuildResult(
            int endIndex,
            NodeRecord[] records,
            Bounds bounds,
            int resolution,
            Vector2 start,
            Vector2 end,
            bool complete)
        {
            var reversed = new List<Vector2>();
            var cursor = endIndex;
            var guard = 0;
            while (cursor >= 0 && guard++ < records.Length)
            {
                reversed.Add(CellToWorld(FromIndex(cursor, resolution), bounds, resolution));
                cursor = records[cursor].ParentIndex;
            }

            reversed.Reverse();
            if (reversed.Count > 0)
            {
                reversed[0] = start;
                reversed[reversed.Count - 1] = end;
            }

            return new RoadPathResult(reversed, records[endIndex].CostFromStart, complete);
        }

        private static RoadPathResult BuildFallback(Vector2 start, Vector2 end)
        {
            var midpoint = Vector2.Lerp(start, end, 0.5f);
            var delta = end - start;
            var normal = delta.sqrMagnitude > 0.01f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
            var offset = Mathf.Min(80f, delta.magnitude * 0.12f);
            return new RoadPathResult(new[] { start, midpoint + normal * offset, end }, delta.magnitude * 10f, false);
        }

        private static int ResolveLowestCostIndex(List<int> open, NodeRecord[] records)
        {
            var bestOpenIndex = 0;
            var bestCost = records[open[0]].EstimatedTotalCost;
            for (var i = 1; i < open.Count; i++)
            {
                var cost = records[open[i]].EstimatedTotalCost;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestOpenIndex = i;
                }
            }

            return bestOpenIndex;
        }

        private static float Heuristic(Vector2Int a, Vector2Int b)
        {
            return Vector2Int.Distance(a, b);
        }

        private static Vector2Int WorldToCell(Vector2 point, Bounds bounds, int resolution)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(bounds.min.x, bounds.max.x, point.x) * (resolution - 1)), 0, resolution - 1),
                Mathf.Clamp(Mathf.RoundToInt(Mathf.InverseLerp(bounds.min.z, bounds.max.z, point.y) * (resolution - 1)), 0, resolution - 1));
        }

        private static Vector2 CellToWorld(Vector2Int cell, Bounds bounds, int resolution)
        {
            var nx = cell.x / (float)Mathf.Max(1, resolution - 1);
            var nz = cell.y / (float)Mathf.Max(1, resolution - 1);
            return new Vector2(
                Mathf.Lerp(bounds.min.x, bounds.max.x, nx),
                Mathf.Lerp(bounds.min.z, bounds.max.z, nz));
        }

        private static int ToIndex(Vector2Int cell, int resolution)
        {
            return cell.y * resolution + cell.x;
        }

        private static Vector2Int FromIndex(int index, int resolution)
        {
            return new Vector2Int(index % resolution, index / resolution);
        }

        private static bool IsInside(Vector2Int cell, int resolution)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < resolution && cell.y < resolution;
        }

        private readonly struct NodeRecord
        {
            public NodeRecord(float costFromStart, float estimatedTotalCost, int parentIndex, bool opened)
            {
                CostFromStart = costFromStart;
                EstimatedTotalCost = estimatedTotalCost;
                ParentIndex = parentIndex;
                Opened = opened;
            }

            public float CostFromStart { get; }
            public float EstimatedTotalCost { get; }
            public int ParentIndex { get; }
            public bool Opened { get; }
        }
    }

    public readonly struct RoadPathResult
    {
        public RoadPathResult(IReadOnlyList<Vector2> points, float cost, bool complete)
        {
            Points = points ?? Array.Empty<Vector2>();
            Cost = cost;
            Complete = complete;
        }

        public IReadOnlyList<Vector2> Points { get; }
        public float Cost { get; }
        public bool Complete { get; }
    }
}
