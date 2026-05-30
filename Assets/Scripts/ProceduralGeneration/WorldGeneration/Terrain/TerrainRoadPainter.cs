using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain
{
    public static class TerrainRoadPainter
    {
        public static RoadPaintSample Evaluate(
            Vector2 worldPosition,
            GeneratedRoadNetwork roadNetwork,
            RoadGenerationConfig roadConfig,
            int seed)
        {
            if (roadNetwork?.Segments == null)
            {
                return RoadPaintSample.Empty;
            }

            var best = RoadPaintSample.Empty;
            var hasBest = false;
            var config = roadConfig ?? new RoadGenerationConfig();
            for (var roadIndex = 0; roadIndex < roadNetwork.Segments.Count; roadIndex++)
            {
                var segment = roadNetwork.Segments[roadIndex];
                if (segment == null || segment.Points.Count < 2)
                {
                    continue;
                }

                var settings = config.Resolve(segment.Type);
                var halfWidth = segment.Width * 0.5f;
                var shoulderWidth = settings.ShoulderWidth;
                var influenceRadius = halfWidth + shoulderWidth;
                var distance = DistanceToPolyline(worldPosition, segment);
                if (distance >= influenceRadius)
                {
                    continue;
                }

                var baseWeight = distance <= halfWidth
                    ? 1f
                    : 1f - Smooth01(Mathf.InverseLerp(halfWidth, influenceRadius, distance));
                var noise = (Mathf.PerlinNoise(
                    worldPosition.x * 0.075f + seed * 0.0017f,
                    worldPosition.y * 0.075f - seed * 0.0013f) - 0.5f) * settings.EdgeNoiseStrength;
                var edgeFactor = distance <= halfWidth ? 1f : Mathf.Clamp01(baseWeight + noise);
                var weight = Mathf.Clamp01(edgeFactor);
                if (!hasBest || weight > best.Weight)
                {
                    best = new RoadPaintSample(segment.Type, weight, settings.StoneBlend);
                    hasBest = true;
                }
            }

            return best;
        }

        private static float DistanceToPolyline(Vector2 point, GeneratedRoadSegment segment)
        {
            var bestSqr = float.MaxValue;
            for (var i = 1; i < segment.Points.Count; i++)
            {
                var t = ClosestSegmentT(point, segment.Points[i - 1], segment.Points[i]);
                var closest = Vector2.Lerp(segment.Points[i - 1], segment.Points[i], t);
                var sqr = (point - closest).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                }
            }

            return Mathf.Sqrt(bestSqr);
        }

        private static float ClosestSegmentT(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.0001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
        }

        private static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }

    public readonly struct RoadPaintSample
    {
        public RoadPaintSample(RoadType type, float weight, float stoneBlend)
        {
            Type = type;
            Weight = Mathf.Clamp01(weight);
            StoneBlend = Mathf.Clamp01(stoneBlend);
        }

        public RoadType Type { get; }
        public float Weight { get; }
        public float StoneBlend { get; }
        public bool HasRoad => Weight > 0.001f;

        public static RoadPaintSample Empty { get; } = new(RoadType.DirtRoad, 0f, 0f);
    }
}
