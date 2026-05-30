using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Generation
{
    public sealed class RoadNetworkGenerator
    {
        public GeneratedRoadNetwork Generate(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler terrainSampler,
            RoadGenerationConfig config,
            PointOfInterestGenerationConfig poiConfig,
            IReadOnlyList<GeneratedSettlement> settlements,
            IReadOnlyList<GeneratedPointOfInterest> pointsOfInterest,
            WorldGenerationMaskSet masks,
            int seed)
        {
            if (settings == null || terrainSampler == null || config == null || !config.Enabled)
            {
                return new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), Array.Empty<GeneratedRoadSegment>());
            }

            var graph = new RoadGraphBuilder().Build(settings, settlements, pointsOfInterest);
            var pathfinder = new TerrainAwareRoadPathfinder();
            var smoother = new RoadPathSmoother();
            var segments = new List<GeneratedRoadSegment>();
            var edges = BuildEdges(graph.Nodes, settlements, pointsOfInterest);

            for (var i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                var roadType = ResolveRoadType(edge.From, edge.To);
                var roadSettings = config.Resolve(roadType);
                var path = pathfinder.FindPath(
                    edge.From.WorldPosition,
                    edge.To.WorldPosition,
                    settings,
                    terrainSampler,
                    masks,
                    config,
                    roadSettings);
                var points = smoother.Smooth(path.Points, roadSettings.Smoothing, terrainSampler, settings.WaterLevel);
                var segment = new GeneratedRoadSegment(
                    $"road_{i:D3}_{edge.From.Id}_{edge.To.Id}",
                    edge.From.Id,
                    edge.To.Id,
                    roadType,
                    points,
                    roadSettings.Width,
                    path.Cost);
                segments.Add(segment);
                RoadMaskRasterizer.AddToMask(masks, segment);
            }

            return new GeneratedRoadNetwork(graph.Nodes, segments);
        }

        private static List<RoadEdgeDraft> BuildEdges(
            IReadOnlyList<GeneratedRoadNode> nodes,
            IReadOnlyList<GeneratedSettlement> settlements,
            IReadOnlyList<GeneratedPointOfInterest> pointsOfInterest)
        {
            var edges = new List<RoadEdgeDraft>();
            var majorSettlement = FindMajorSettlementNode(nodes);
            var transitions = FindNodes(nodes, RoadNodeType.LocationTransition);
            for (var i = 0; i < transitions.Count; i++)
            {
                if (majorSettlement != null)
                {
                    AddUnique(edges, transitions[i], majorSettlement);
                }
            }

            var settlementNodes = FindNodes(nodes, RoadNodeType.Settlement);
            for (var i = 0; i < settlementNodes.Count; i++)
            {
                var current = settlementNodes[i];
                var nearest = FindNearest(current, settlementNodes, node => node != current);
                if (nearest != null)
                {
                    AddUnique(edges, current, nearest);
                }

                if (majorSettlement != null && current != majorSettlement)
                {
                    AddUnique(edges, current, majorSettlement);
                }
            }

            var poiNodes = FindNodes(nodes, RoadNodeType.PointOfInterest);
            for (var i = 0; i < poiNodes.Count; i++)
            {
                var poi = poiNodes[i];
                if (poi.Importance <= 0)
                {
                    continue;
                }

                var nearestSettlement = FindNearest(poi, settlementNodes, _ => true);
                if (nearestSettlement != null)
                {
                    AddUnique(edges, poi, nearestSettlement);
                    continue;
                }

                var nearestTransition = FindNearest(poi, transitions, _ => true);
                if (nearestTransition != null)
                {
                    AddUnique(edges, poi, nearestTransition);
                }
            }

            return edges;
        }

        private static RoadType ResolveRoadType(GeneratedRoadNode from, GeneratedRoadNode to)
        {
            var maxImportance = Mathf.Max(from.Importance, to.Importance);
            if (from.Type == RoadNodeType.LocationTransition || to.Type == RoadNodeType.LocationTransition)
            {
                return maxImportance >= 5 ? RoadType.MainRoad : RoadType.DirtRoad;
            }

            if (from.Type == RoadNodeType.PointOfInterest || to.Type == RoadNodeType.PointOfInterest)
            {
                return maxImportance >= 4 ? RoadType.DirtRoad : RoadType.Trail;
            }

            return maxImportance >= 5 ? RoadType.MainRoad : RoadType.DirtRoad;
        }

        private static GeneratedRoadNode FindMajorSettlementNode(IReadOnlyList<GeneratedRoadNode> nodes)
        {
            GeneratedRoadNode best = null;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.Type != RoadNodeType.Settlement)
                {
                    continue;
                }

                if (best == null || node.Importance > best.Importance)
                {
                    best = node;
                }
            }

            return best;
        }

        private static List<GeneratedRoadNode> FindNodes(IReadOnlyList<GeneratedRoadNode> nodes, RoadNodeType type)
        {
            var result = new List<GeneratedRoadNode>();
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type == type)
                {
                    result.Add(nodes[i]);
                }
            }

            return result;
        }

        private static GeneratedRoadNode FindNearest(GeneratedRoadNode origin, IReadOnlyList<GeneratedRoadNode> nodes, Func<GeneratedRoadNode, bool> predicate)
        {
            GeneratedRoadNode best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!predicate(node))
                {
                    continue;
                }

                var distance = (node.WorldPosition - origin.WorldPosition).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }

            return best;
        }

        private static void AddUnique(List<RoadEdgeDraft> edges, GeneratedRoadNode from, GeneratedRoadNode to)
        {
            if (from == null || to == null || from == to)
            {
                return;
            }

            for (var i = 0; i < edges.Count; i++)
            {
                if ((edges[i].From == from && edges[i].To == to) ||
                    (edges[i].From == to && edges[i].To == from))
                {
                    return;
                }
            }

            edges.Add(new RoadEdgeDraft(from, to));
        }

        private readonly struct RoadEdgeDraft
        {
            public RoadEdgeDraft(GeneratedRoadNode from, GeneratedRoadNode to)
            {
                From = from;
                To = to;
            }

            public GeneratedRoadNode From { get; }
            public GeneratedRoadNode To { get; }
        }
    }

    public sealed class RoadGraphBuilder
    {
        public RoadGraphDraft Build(
            ProceduralLocationSettings settings,
            IReadOnlyList<GeneratedSettlement> settlements,
            IReadOnlyList<GeneratedPointOfInterest> pointsOfInterest)
        {
            var nodes = new List<GeneratedRoadNode>();
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                nodes.Add(new GeneratedRoadNode(
                    settlement.Id,
                    settlement.Name,
                    RoadNodeType.Settlement,
                    settlement.WorldPosition,
                    ResolveSettlementImportance(settlement.Tier)));
            }

            for (var i = 0; i < pointsOfInterest.Count; i++)
            {
                var poi = pointsOfInterest[i];
                nodes.Add(new GeneratedRoadNode(
                    poi.Id,
                    poi.Name,
                    RoadNodeType.PointOfInterest,
                    poi.WorldPosition,
                    ResolvePoiImportance(poi)));
            }

            AddTransitionNodes(settings, nodes);
            return new RoadGraphDraft(nodes);
        }

        private static void AddTransitionNodes(ProceduralLocationSettings settings, List<GeneratedRoadNode> nodes)
        {
            var bounds = settings.GetWorldBounds();
            var center = bounds.center;
            nodes.Add(new GeneratedRoadNode("transition_north", "North Transition", RoadNodeType.LocationTransition, new Vector2(center.x, bounds.max.z - 12f), 5));
            nodes.Add(new GeneratedRoadNode("transition_south", "South Transition", RoadNodeType.LocationTransition, new Vector2(center.x, bounds.min.z + 12f), 5));
            nodes.Add(new GeneratedRoadNode("transition_east", "East Transition", RoadNodeType.LocationTransition, new Vector2(bounds.max.x - 12f, center.z), 5));
            nodes.Add(new GeneratedRoadNode("transition_west", "West Transition", RoadNodeType.LocationTransition, new Vector2(bounds.min.x + 12f, center.z), 5));
        }

        private static int ResolveSettlementImportance(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 1,
                SettlementTier.Hamlet => 2,
                SettlementTier.Village => 3,
                SettlementTier.Town => 5,
                SettlementTier.City => 7,
                SettlementTier.Capital => 9,
                _ => 1
            };
        }

        private static int ResolvePoiImportance(GeneratedPointOfInterest poi)
        {
            if (poi.Type == PointOfInterestType.HiddenCache || poi.Type == PointOfInterestType.StoneCircle)
            {
                return 0;
            }

            return poi.DangerLevel >= 5 || poi.Type == PointOfInterestType.AncientTemple || poi.Type == PointOfInterestType.ResourceNode ? 3 : 1;
        }
    }

    public sealed class RoadGraphDraft
    {
        public RoadGraphDraft(IReadOnlyList<GeneratedRoadNode> nodes)
        {
            Nodes = nodes ?? Array.Empty<GeneratedRoadNode>();
        }

        public IReadOnlyList<GeneratedRoadNode> Nodes { get; }
    }

    public sealed class RoadPathSmoother
    {
        public IReadOnlyList<Vector2> Smooth(IReadOnlyList<Vector2> points, float amount, IProceduralTerrainSampler sampler, float waterLevel)
        {
            if (points == null || points.Count <= 2 || amount <= 0f)
            {
                return points ?? Array.Empty<Vector2>();
            }

            var smoothed = new List<Vector2>(points);
            var iterations = amount > 0.8f ? 2 : 1;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var next = new List<Vector2> { smoothed[0] };
                for (var i = 1; i < smoothed.Count - 1; i++)
                {
                    var target = (smoothed[i - 1] + smoothed[i] + smoothed[i + 1]) / 3f;
                    var valid = sampler == null ||
                                (sampler.TrySample(target.x, target.y, out var point, out var normal) &&
                                 point.y > waterLevel + 0.2f &&
                                 Vector3.Angle(normal, Vector3.up) < 48f);
                    next.Add(valid ? Vector2.Lerp(smoothed[i], target, amount * 0.55f) : smoothed[i]);
                }

                next.Add(smoothed[smoothed.Count - 1]);
                smoothed = next;
            }

            return Simplify(smoothed, 4f);
        }

        private static IReadOnlyList<Vector2> Simplify(IReadOnlyList<Vector2> points, float minDistance)
        {
            if (points.Count <= 2)
            {
                return points;
            }

            var result = new List<Vector2> { points[0] };
            for (var i = 1; i < points.Count - 1; i++)
            {
                if (Vector2.Distance(result[result.Count - 1], points[i]) >= minDistance)
                {
                    result.Add(points[i]);
                }
            }

            result.Add(points[points.Count - 1]);
            return result;
        }
    }

    public static class RoadMaskRasterizer
    {
        public static void AddToMask(WorldGenerationMaskSet masks, GeneratedRoadSegment segment)
        {
            if (masks == null || segment == null || segment.Points.Count == 0)
            {
                return;
            }

            masks.AddPath(new GenerationPathMask(
                segment.Id,
                GenerationZoneKind.Road,
                segment.Points,
                segment.Width * 0.5f + 2.25f,
                1f,
                segment.Id));
            masks.AddPath(new GenerationPathMask(
                $"{segment.Id}_reduced_vegetation",
                GenerationZoneKind.ReducedVegetation,
                segment.Points,
                segment.Width * 1.5f + 5f,
                0.68f,
                segment.Id));
        }
    }
}
