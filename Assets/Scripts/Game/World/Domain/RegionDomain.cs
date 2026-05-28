using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public enum RegionType
    {
        Coast,
        Forest,
        Desert,
        Mountain,
        Swamp,
        Highlands,
        Riverlands,
        Island,
        Wasteland,
        Mixed
    }

    public readonly struct RegionId : IEquatable<RegionId>
    {
        public RegionId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        }

        public string Value { get; }

        public static RegionId NewId()
        {
            return new RegionId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(RegionId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RegionId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct RegionName
    {
        public RegionName(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? "Unnamed Region" : value.Trim();
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }

    public sealed class RegionBoundary
    {
        public RegionBoundary(IReadOnlyCollection<MapPoint> points)
        {
            Points = points ?? Array.Empty<MapPoint>();
        }

        public IReadOnlyCollection<MapPoint> Points { get; }
    }

    public sealed class WorldRegion
    {
        public WorldRegion(
            RegionId id,
            RegionName name,
            RegionType type,
            BiomeType dominantBiome,
            IReadOnlyCollection<BiomeType> secondaryBiomes,
            RegionBoundary boundary,
            IReadOnlyCollection<LocationId> locationIds,
            string loreHook)
        {
            Id = id;
            Name = name;
            Type = type;
            DominantBiome = dominantBiome;
            SecondaryBiomes = secondaryBiomes ?? Array.Empty<BiomeType>();
            Boundary = boundary ?? new RegionBoundary(Array.Empty<MapPoint>());
            LocationIds = locationIds ?? Array.Empty<LocationId>();
            LoreHook = loreHook ?? string.Empty;
        }

        public RegionId Id { get; }
        public RegionName Name { get; }
        public RegionType Type { get; }
        public BiomeType DominantBiome { get; }
        public IReadOnlyCollection<BiomeType> SecondaryBiomes { get; }
        public RegionBoundary Boundary { get; }
        public IReadOnlyCollection<LocationId> LocationIds { get; }
        public string LoreHook { get; }
    }
}
