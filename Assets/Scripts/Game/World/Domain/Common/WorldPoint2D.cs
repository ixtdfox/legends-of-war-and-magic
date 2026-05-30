using System;

namespace LegendsOfWarAndMagic.Game.World.Domain.Common
{
    public readonly struct WorldPoint2D : IEquatable<WorldPoint2D>
    {
        public WorldPoint2D(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }
        public float Z { get; }

        public float DistanceTo(WorldPoint2D other)
        {
            var dx = X - other.X;
            var dz = Z - other.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public bool Equals(WorldPoint2D other)
        {
            return X.Equals(other.X) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPoint2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Z.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{X:0.###}, {Z:0.###}";
        }
    }
}
