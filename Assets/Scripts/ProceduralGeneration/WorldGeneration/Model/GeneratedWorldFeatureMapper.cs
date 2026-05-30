using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public static class GeneratedWorldFeatureMapper
    {
        public static Settlement ToSettlement(GeneratedSettlement generated)
        {
            var buildings = new List<SettlementBuilding>();
            for (var i = 0; generated != null && i < generated.Buildings.Count; i++)
            {
                var building = generated.Buildings[i];
                buildings.Add(new SettlementBuilding(
                    building.Id,
                    building.Definition.Id,
                    building.Definition.Type,
                    building.Definition.Level));
            }

            return new Settlement(
                generated?.Id,
                generated?.Name,
                generated?.Tier ?? SettlementTier.Camp,
                generated?.OwnerFactionId,
                generated == null ? new WorldPoint2D(0f, 0f) : generated.WorldPosition.ToWorldPoint2D(),
                generated?.Radius ?? 1f,
                buildings,
                generated?.Stats,
                generated?.Progression,
                generated?.Economy);
        }

        public static PointOfInterest ToPointOfInterest(GeneratedPointOfInterest generated)
        {
            return new PointOfInterest(
                generated?.Id,
                generated?.Name,
                generated?.Type ?? PointOfInterestType.Ruins,
                generated == null ? new WorldPoint2D(0f, 0f) : generated.WorldPosition.ToWorldPoint2D(),
                generated?.Radius ?? 1f,
                generated?.DangerLevel ?? 0,
                generated?.FactionId,
                generated?.Tags,
                generated?.State ?? PointOfInterestState.Undiscovered);
        }

        public static RoadNetwork ToRoadNetwork(GeneratedRoadNetwork generated)
        {
            var nodes = new List<RoadNode>();
            var segments = new List<RoadSegment>();
            for (var i = 0; generated != null && i < generated.Nodes.Count; i++)
            {
                var node = generated.Nodes[i];
                nodes.Add(new RoadNode(node.Id, node.DisplayName, node.Type, node.WorldPosition.ToWorldPoint2D(), node.Importance));
            }

            for (var i = 0; generated != null && i < generated.Segments.Count; i++)
            {
                var segment = generated.Segments[i];
                var points = new List<WorldPoint2D>(segment.Points.Count);
                for (var pointIndex = 0; pointIndex < segment.Points.Count; pointIndex++)
                {
                    points.Add(segment.Points[pointIndex].ToWorldPoint2D());
                }

                segments.Add(new RoadSegment(segment.Id, segment.FromNodeId, segment.ToNodeId, segment.Type, points));
            }

            return new RoadNetwork(nodes, segments);
        }
    }
}
