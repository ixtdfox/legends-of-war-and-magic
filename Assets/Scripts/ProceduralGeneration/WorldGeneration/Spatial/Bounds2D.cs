using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial
{
    public readonly struct Bounds2D
    {
        public Bounds2D(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = new Vector2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
        }

        public Vector2 Center { get; }
        public Vector2 Size { get; }
        public Vector2 Extents => Size * 0.5f;
        public Vector2 Min => Center - Extents;
        public Vector2 Max => Center + Extents;

        public static Bounds2D FromMinMax(Vector2 min, Vector2 max)
        {
            var safeMin = Vector2.Min(min, max);
            var safeMax = Vector2.Max(min, max);
            return new Bounds2D((safeMin + safeMax) * 0.5f, safeMax - safeMin);
        }

        public static Bounds2D FromCircle(Vector2 center, float radius)
        {
            var diameter = Mathf.Max(0f, radius) * 2f;
            return new Bounds2D(center, new Vector2(diameter, diameter));
        }

        public bool Contains(Vector2 point)
        {
            var min = Min;
            var max = Max;
            return point.x >= min.x && point.x <= max.x && point.y >= min.y && point.y <= max.y;
        }

        public bool Intersects(Bounds2D other)
        {
            var min = Min;
            var max = Max;
            var otherMin = other.Min;
            var otherMax = other.Max;
            return min.x <= otherMax.x &&
                   max.x >= otherMin.x &&
                   min.y <= otherMax.y &&
                   max.y >= otherMin.y;
        }

        public bool IntersectsCircle(Vector2 center, float radius)
        {
            var min = Min;
            var max = Max;
            var closestX = Mathf.Clamp(center.x, min.x, max.x);
            var closestY = Mathf.Clamp(center.y, min.y, max.y);
            var dx = center.x - closestX;
            var dy = center.y - closestY;
            return dx * dx + dy * dy <= radius * radius;
        }
    }
}
