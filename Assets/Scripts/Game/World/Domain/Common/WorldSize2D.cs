using System;

namespace LegendsOfWarAndMagic.Game.World.Domain.Common
{
    public readonly struct WorldSize2D : IEquatable<WorldSize2D>
    {
        public WorldSize2D(float width, float depth)
        {
            Width = WorldMath.Max(0f, width);
            Depth = WorldMath.Max(0f, depth);
        }

        public float Width { get; }
        public float Depth { get; }

        public bool Equals(WorldSize2D other)
        {
            return Width.Equals(other.Width) && Depth.Equals(other.Depth);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldSize2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width.GetHashCode() * 397) ^ Depth.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Width:0.###} x {Depth:0.###}";
        }
    }
}
