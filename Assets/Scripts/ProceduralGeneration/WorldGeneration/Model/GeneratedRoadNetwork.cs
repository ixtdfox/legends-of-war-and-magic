using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public sealed class GeneratedRoadNode
    {
        public GeneratedRoadNode(string id, string displayName, RoadNodeType type, Vector2 worldPosition, int importance)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
            Type = type;
            WorldPosition = worldPosition;
            Importance = Mathf.Max(0, importance);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public RoadNodeType Type { get; }
        public Vector2 WorldPosition { get; }
        public int Importance { get; }
    }

    public sealed class GeneratedRoadSegment
    {
        public GeneratedRoadSegment(string id, string fromNodeId, string toNodeId, RoadType type, IReadOnlyList<Vector2> points, float width, float cost)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            FromNodeId = fromNodeId ?? string.Empty;
            ToNodeId = toNodeId ?? string.Empty;
            Type = type;
            Points = points ?? Array.Empty<Vector2>();
            Width = Mathf.Max(0.25f, width);
            Cost = Mathf.Max(0f, cost);
        }

        public string Id { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public RoadType Type { get; }
        public IReadOnlyList<Vector2> Points { get; }
        public float Width { get; }
        public float Cost { get; }
    }

    public sealed class GeneratedRoadNetwork
    {
        public GeneratedRoadNetwork(IReadOnlyList<GeneratedRoadNode> nodes, IReadOnlyList<GeneratedRoadSegment> segments)
        {
            Nodes = nodes ?? Array.Empty<GeneratedRoadNode>();
            Segments = segments ?? Array.Empty<GeneratedRoadSegment>();
        }

        public IReadOnlyList<GeneratedRoadNode> Nodes { get; }
        public IReadOnlyList<GeneratedRoadSegment> Segments { get; }
    }
}
