using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.Roads
{
    public enum RoadType
    {
        Trail,
        DirtRoad,
        MainRoad,
        StoneRoad,
        HiddenPath
    }

    public enum RoadNodeType
    {
        Settlement,
        PointOfInterest,
        LocationTransition
    }

    public sealed class RoadNode
    {
        public RoadNode(string id, string displayName, RoadNodeType type, WorldPoint2D worldPosition, int importance)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
            Type = type;
            WorldPosition = worldPosition;
            Importance = WorldMath.Max(0, importance);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public RoadNodeType Type { get; }
        public WorldPoint2D WorldPosition { get; }
        public int Importance { get; }
    }

    public sealed class RoadSegment
    {
        public RoadSegment(string id, string fromNodeId, string toNodeId, RoadType type, IReadOnlyList<WorldPoint2D> points)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            FromNodeId = fromNodeId ?? string.Empty;
            ToNodeId = toNodeId ?? string.Empty;
            Type = type;
            Points = points ?? Array.Empty<WorldPoint2D>();
        }

        public string Id { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public RoadType Type { get; }
        public IReadOnlyList<WorldPoint2D> Points { get; }
    }

    public sealed class RoadNetwork
    {
        public RoadNetwork(IReadOnlyList<RoadNode> nodes, IReadOnlyList<RoadSegment> segments)
        {
            Nodes = nodes ?? Array.Empty<RoadNode>();
            Segments = segments ?? Array.Empty<RoadSegment>();
        }

        public IReadOnlyList<RoadNode> Nodes { get; }
        public IReadOnlyList<RoadSegment> Segments { get; }
    }
}
