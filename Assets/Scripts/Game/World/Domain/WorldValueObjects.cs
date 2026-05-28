using System;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public enum WorldShapeType
    {
        HugeIsland,
        IslandArchipelago,
        ContinentPart,
        Peninsula,
        PeninsulaAndIslands,
        TwoPeninsulas,
        BrokenCoast,
        InlandSeaRegion,
        MountainRingBasin,
        RiverDeltaRegion
    }

    public enum WorldDirection
    {
        North,
        South,
        East,
        West,
        NorthEast,
        NorthWest,
        SouthEast,
        SouthWest
    }

    public enum WorldFeatureType
    {
        River,
        MountainRange,
        Lake,
        Coastline,
        Road
    }

    public readonly struct WorldId : IEquatable<WorldId>
    {
        public WorldId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        }

        public string Value { get; }

        public static WorldId NewId()
        {
            return new WorldId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(WorldId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldId other && Equals(other);
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

    public readonly struct WorldSeed
    {
        public WorldSeed(int value)
        {
            Value = value == 0 ? 1 : value;
        }

        public int Value { get; }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct WorldName
    {
        public WorldName(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? "Unnamed World" : value.Trim();
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct WorldBounds
    {
        public WorldBounds(float width, float height)
        {
            Width = Math.Max(1f, width);
            Height = Math.Max(1f, height);
        }

        public float Width { get; }
        public float Height { get; }
    }

    public readonly struct MapPoint
    {
        public MapPoint(float x, float y)
        {
            X = Clamp01(x);
            Y = Clamp01(y);
        }

        public float X { get; }
        public float Y { get; }

        public float DistanceTo(MapPoint other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    public readonly struct MapRect
    {
        public MapRect(float x, float y, float width, float height)
        {
            X = Clamp01(x);
            Y = Clamp01(y);
            Width = Clamp01(width);
            Height = Clamp01(height);
        }

        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    public readonly struct MapColor
    {
        public MapColor(float r, float g, float b, float a = 1f)
        {
            R = Clamp01(r);
            G = Clamp01(g);
            B = Clamp01(b);
            A = Clamp01(a);
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    public sealed class WorldShape
    {
        public WorldShape(WorldShapeType type, string description)
        {
            Type = type;
            Description = string.IsNullOrWhiteSpace(description) ? type.ToString() : description;
        }

        public WorldShapeType Type { get; }
        public string Description { get; }
    }

    public sealed class WorldGenerationMetadata
    {
        public WorldGenerationMetadata(
            string generatorVersion,
            DateTime createdUtc,
            string configSnapshot)
        {
            GeneratorVersion = string.IsNullOrWhiteSpace(generatorVersion) ? "unknown" : generatorVersion;
            CreatedUtc = createdUtc;
            ConfigSnapshot = configSnapshot ?? string.Empty;
        }

        public string GeneratorVersion { get; }
        public DateTime CreatedUtc { get; }
        public string ConfigSnapshot { get; }
    }
}
