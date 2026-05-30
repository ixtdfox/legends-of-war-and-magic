using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Persistence
{
    public static class GeneratedWorldFeatureSaveMapper
    {
        public static WorldGenerationLayerSaveDto ToDto(WorldGenerationLayers layers, int seed)
        {
            return new WorldGenerationLayerSaveDto
            {
                seed = seed,
                settlements = MapSettlements(layers.Settlements),
                pointsOfInterest = MapPoi(layers.PointsOfInterest),
                roadNetwork = MapRoadNetwork(layers.RoadNetwork),
                masks = MapMasks(layers.Masks)
            };
        }

        private static SettlementSaveDto[] MapSettlements(IReadOnlyList<GeneratedSettlement> settlements)
        {
            var result = new SettlementSaveDto[settlements.Count];
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                result[i] = new SettlementSaveDto
                {
                    id = settlement.Id,
                    name = settlement.Name,
                    tier = settlement.Tier.ToString(),
                    x = settlement.WorldPosition.x,
                    z = settlement.WorldPosition.y,
                    radius = settlement.Radius,
                    population = settlement.Stats.Population,
                    wealth = settlement.Stats.Wealth,
                    security = settlement.Stats.Safety,
                    buildings = MapBuildings(settlement.Buildings),
                    internalRoads = MapSettlementRoads(settlement.InternalRoads)
                };
            }

            return result;
        }

        private static SettlementBuildingSaveDto[] MapBuildings(IReadOnlyList<GeneratedSettlementBuilding> buildings)
        {
            var result = new SettlementBuildingSaveDto[buildings.Count];
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                result[i] = new SettlementBuildingSaveDto
                {
                    id = building.Id,
                    definitionId = building.Definition.Id,
                    type = building.Definition.Type.ToString(),
                    level = building.Definition.Level,
                    x = building.WorldPosition.x,
                    z = building.WorldPosition.y,
                    rotation = building.RotationDegrees,
                    footprintX = building.FootprintSize.x,
                    footprintZ = building.FootprintSize.y,
                    prefabKey = building.Definition.PrefabKey
                };
            }

            return result;
        }

        private static RoadPolylineSaveDto[] MapSettlementRoads(IReadOnlyList<GeneratedSettlementRoadSegment> roads)
        {
            var result = new RoadPolylineSaveDto[roads.Count];
            for (var i = 0; i < roads.Count; i++)
            {
                result[i] = new RoadPolylineSaveDto
                {
                    id = roads[i].Id,
                    type = roads[i].Primary ? "Primary" : "Secondary",
                    width = roads[i].Width,
                    points = MapPoints(roads[i].Points)
                };
            }

            return result;
        }

        private static PointOfInterestSaveDto[] MapPoi(IReadOnlyList<GeneratedPointOfInterest> points)
        {
            var result = new PointOfInterestSaveDto[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var poi = points[i];
                result[i] = new PointOfInterestSaveDto
                {
                    id = poi.Id,
                    name = poi.Name,
                    type = poi.Type.ToString(),
                    x = poi.WorldPosition.x,
                    z = poi.WorldPosition.y,
                    radius = poi.Radius,
                    dangerLevel = poi.DangerLevel,
                    prefabKey = poi.PrefabKey,
                    state = poi.State.ToString(),
                    tags = ToArray(poi.Tags)
                };
            }

            return result;
        }

        private static RoadNetworkSaveDto MapRoadNetwork(GeneratedRoadNetwork roadNetwork)
        {
            return new RoadNetworkSaveDto
            {
                nodes = MapRoadNodes(roadNetwork.Nodes),
                segments = MapRoadSegments(roadNetwork.Segments)
            };
        }

        private static RoadNodeSaveDto[] MapRoadNodes(IReadOnlyList<GeneratedRoadNode> nodes)
        {
            var result = new RoadNodeSaveDto[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                result[i] = new RoadNodeSaveDto
                {
                    id = nodes[i].Id,
                    name = nodes[i].DisplayName,
                    type = nodes[i].Type.ToString(),
                    x = nodes[i].WorldPosition.x,
                    z = nodes[i].WorldPosition.y,
                    importance = nodes[i].Importance
                };
            }

            return result;
        }

        private static RoadSegmentSaveDto[] MapRoadSegments(IReadOnlyList<GeneratedRoadSegment> segments)
        {
            var result = new RoadSegmentSaveDto[segments.Count];
            for (var i = 0; i < segments.Count; i++)
            {
                result[i] = new RoadSegmentSaveDto
                {
                    id = segments[i].Id,
                    fromNodeId = segments[i].FromNodeId,
                    toNodeId = segments[i].ToNodeId,
                    type = segments[i].Type.ToString(),
                    width = segments[i].Width,
                    cost = segments[i].Cost,
                    points = MapPoints(segments[i].Points)
                };
            }

            return result;
        }

        private static MaskSaveDto MapMasks(WorldGenerationMaskSet masks)
        {
            return new MaskSaveDto
            {
                zones = MapZones(masks.Zones),
                paths = MapPaths(masks.Paths)
            };
        }

        private static MaskZoneSaveDto[] MapZones(IReadOnlyList<GenerationMaskZone> zones)
        {
            var result = new MaskZoneSaveDto[zones.Count];
            for (var i = 0; i < zones.Count; i++)
            {
                result[i] = new MaskZoneSaveDto
                {
                    id = zones[i].Id,
                    kind = zones[i].Kind.ToString(),
                    x = zones[i].Center.x,
                    z = zones[i].Center.y,
                    radius = zones[i].Radius,
                    strength = zones[i].Strength,
                    sourceId = zones[i].SourceId
                };
            }

            return result;
        }

        private static MaskPathSaveDto[] MapPaths(IReadOnlyList<GenerationPathMask> paths)
        {
            var result = new MaskPathSaveDto[paths.Count];
            for (var i = 0; i < paths.Count; i++)
            {
                result[i] = new MaskPathSaveDto
                {
                    id = paths[i].Id,
                    kind = paths[i].Kind.ToString(),
                    radius = paths[i].Radius,
                    strength = paths[i].Strength,
                    sourceId = paths[i].SourceId,
                    points = MapPoints(paths[i].Points)
                };
            }

            return result;
        }

        private static WorldPointSaveDto[] MapPoints(IReadOnlyList<Vector2> points)
        {
            var result = new WorldPointSaveDto[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                result[i] = new WorldPointSaveDto { x = points[i].x, z = points[i].y };
            }

            return result;
        }

        private static string[] ToArray(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                result[i] = values[i];
            }

            return result;
        }
    }

    [Serializable]
    public sealed class WorldGenerationLayerSaveDto
    {
        public int seed;
        public SettlementSaveDto[] settlements;
        public PointOfInterestSaveDto[] pointsOfInterest;
        public RoadNetworkSaveDto roadNetwork;
        public MaskSaveDto masks;
    }

    [Serializable]
    public sealed class SettlementSaveDto
    {
        public string id;
        public string name;
        public string tier;
        public float x;
        public float z;
        public float radius;
        public int population;
        public int wealth;
        public int security;
        public SettlementBuildingSaveDto[] buildings;
        public RoadPolylineSaveDto[] internalRoads;
    }

    [Serializable]
    public sealed class SettlementBuildingSaveDto
    {
        public string id;
        public string definitionId;
        public string type;
        public int level;
        public float x;
        public float z;
        public float rotation;
        public float footprintX;
        public float footprintZ;
        public string prefabKey;
    }

    [Serializable]
    public sealed class PointOfInterestSaveDto
    {
        public string id;
        public string name;
        public string type;
        public float x;
        public float z;
        public float radius;
        public int dangerLevel;
        public string prefabKey;
        public string state;
        public string[] tags;
    }

    [Serializable]
    public sealed class RoadNetworkSaveDto
    {
        public RoadNodeSaveDto[] nodes;
        public RoadSegmentSaveDto[] segments;
    }

    [Serializable]
    public sealed class RoadNodeSaveDto
    {
        public string id;
        public string name;
        public string type;
        public float x;
        public float z;
        public int importance;
    }

    [Serializable]
    public sealed class RoadSegmentSaveDto
    {
        public string id;
        public string fromNodeId;
        public string toNodeId;
        public string type;
        public float width;
        public float cost;
        public WorldPointSaveDto[] points;
    }

    [Serializable]
    public sealed class RoadPolylineSaveDto
    {
        public string id;
        public string type;
        public float width;
        public WorldPointSaveDto[] points;
    }

    [Serializable]
    public sealed class MaskSaveDto
    {
        public MaskZoneSaveDto[] zones;
        public MaskPathSaveDto[] paths;
    }

    [Serializable]
    public sealed class MaskZoneSaveDto
    {
        public string id;
        public string kind;
        public float x;
        public float z;
        public float radius;
        public float strength;
        public string sourceId;
    }

    [Serializable]
    public sealed class MaskPathSaveDto
    {
        public string id;
        public string kind;
        public float radius;
        public float strength;
        public string sourceId;
        public WorldPointSaveDto[] points;
    }

    [Serializable]
    public sealed class WorldPointSaveDto
    {
        public float x;
        public float z;
    }
}
