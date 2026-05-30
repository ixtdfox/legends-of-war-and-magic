using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public abstract class WorldFeature
    {
        protected WorldFeature(string id, string name, WorldFeatureType type, IReadOnlyCollection<MapPoint> points)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? type.ToString() : name;
            Type = type;
            Points = points ?? Array.Empty<MapPoint>();
        }

        public string Id { get; }
        public string Name { get; }
        public WorldFeatureType Type { get; }
        public IReadOnlyCollection<MapPoint> Points { get; }
    }

    public sealed class River : WorldFeature
    {
        public River(string id, string name, IReadOnlyCollection<MapPoint> points, float width)
            : base(id, name, WorldFeatureType.River, points)
        {
            Width = Math.Max(0.001f, width);
        }

        public float Width { get; }
    }

    public sealed class MountainRange : WorldFeature
    {
        public MountainRange(string id, string name, IReadOnlyCollection<MapPoint> ridgePoints, float intensity)
            : base(id, name, WorldFeatureType.MountainRange, ridgePoints)
        {
            Intensity = Math.Max(0f, intensity);
        }

        public float Intensity { get; }
    }

    public sealed class Lake : WorldFeature
    {
        public Lake(string id, string name, IReadOnlyCollection<MapPoint> shoreline, float radius)
            : base(id, name, WorldFeatureType.Lake, shoreline)
        {
            Radius = Math.Max(0.001f, radius);
        }

        public float Radius { get; }
    }

    public sealed class Coastline : WorldFeature
    {
        public Coastline(string id, string name, IReadOnlyCollection<MapPoint> points)
            : base(id, name, WorldFeatureType.Coastline, points)
        {
        }
    }

    public sealed class Road : WorldFeature
    {
        public Road(string id, string name, IReadOnlyCollection<MapPoint> points)
            : base(id, name, WorldFeatureType.Road, points)
        {
        }
    }
}
