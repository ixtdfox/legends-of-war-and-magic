using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks
{
    public enum GenerationZoneKind
    {
        SettlementFootprint,
        SettlementClearing,
        Road,
        PointOfInterestFootprint,
        NoSpawn,
        ReducedVegetation,
        Reserved
    }

    public sealed class GenerationMaskZone
    {
        public GenerationMaskZone(
            string id,
            GenerationZoneKind kind,
            Vector2 center,
            float radius,
            float strength,
            string sourceId)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Kind = kind;
            Center = center;
            Radius = Mathf.Max(0f, radius);
            Strength = Mathf.Clamp01(strength);
            SourceId = sourceId ?? string.Empty;
        }

        public string Id { get; }
        public GenerationZoneKind Kind { get; }
        public Vector2 Center { get; }
        public float Radius { get; }
        public float Strength { get; }
        public string SourceId { get; }
        public Bounds2D Bounds => Bounds2D.FromCircle(Center, Radius);

        public float Evaluate(Vector2 point)
        {
            if (Radius <= 0f)
            {
                return 0f;
            }

            var distance = Vector2.Distance(Center, point);
            if (distance >= Radius)
            {
                return 0f;
            }

            return Mathf.Clamp01((1f - distance / Radius) * Strength);
        }
    }

    public sealed class GenerationPathMask
    {
        public GenerationPathMask(
            string id,
            GenerationZoneKind kind,
            IReadOnlyList<Vector2> points,
            float radius,
            float strength,
            string sourceId)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Kind = kind;
            Points = points ?? Array.Empty<Vector2>();
            Radius = Mathf.Max(0f, radius);
            Strength = Mathf.Clamp01(strength);
            SourceId = sourceId ?? string.Empty;
            Bounds = BuildBounds(Points, Radius);
        }

        public string Id { get; }
        public GenerationZoneKind Kind { get; }
        public IReadOnlyList<Vector2> Points { get; }
        public float Radius { get; }
        public float Strength { get; }
        public string SourceId { get; }
        public Bounds2D Bounds { get; }

        public float Evaluate(Vector2 point)
        {
            if (Points.Count == 0 || Radius <= 0f)
            {
                return 0f;
            }

            var bestSqr = float.MaxValue;
            for (var i = 1; i < Points.Count; i++)
            {
                var sqr = DistanceToSegmentSqr(point, Points[i - 1], Points[i]);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                }
            }

            if (Points.Count == 1)
            {
                bestSqr = (point - Points[0]).sqrMagnitude;
            }

            var distance = Mathf.Sqrt(bestSqr);
            if (distance >= Radius)
            {
                return 0f;
            }

            return Mathf.Clamp01((1f - distance / Radius) * Strength);
        }

        private static Bounds2D BuildBounds(IReadOnlyList<Vector2> points, float radius)
        {
            if (points == null || points.Count == 0)
            {
                return new Bounds2D(Vector2.zero, Vector2.zero);
            }

            var min = points[0];
            var max = points[0];
            for (var i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            var padding = new Vector2(radius, radius);
            return Bounds2D.FromMinMax(min - padding, max + padding);
        }

        private static float DistanceToSegmentSqr(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSqr = ab.sqrMagnitude;
            if (lengthSqr <= 0.0001f)
            {
                return (point - a).sqrMagnitude;
            }

            var t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSqr);
            var projection = a + ab * t;
            return (point - projection).sqrMagnitude;
        }
    }

    public sealed class WorldGenerationMaskSet
    {
        private readonly List<GenerationMaskZone> zones = new();
        private readonly List<GenerationPathMask> paths = new();
        private readonly SpatialHashGrid2D<GenerationMaskZone> zoneIndex;
        private readonly SpatialHashGrid2D<GenerationPathMask> pathIndex;

        public WorldGenerationMaskSet(float cellSize = 32f)
        {
            zoneIndex = new SpatialHashGrid2D<GenerationMaskZone>(cellSize);
            pathIndex = new SpatialHashGrid2D<GenerationPathMask>(cellSize);
        }

        public IReadOnlyList<GenerationMaskZone> Zones => zones;
        public IReadOnlyList<GenerationPathMask> Paths => paths;

        public void AddZone(GenerationMaskZone zone)
        {
            if (zone == null || zone.Radius <= 0f)
            {
                return;
            }

            zones.Add(zone);
            zoneIndex.Insert(zone.Bounds, zone);
        }

        public void AddPath(GenerationPathMask path)
        {
            if (path == null || path.Radius <= 0f || path.Points.Count == 0)
            {
                return;
            }

            paths.Add(path);
            pathIndex.Insert(path.Bounds, path);
        }

        public bool IsNoSpawn(Vector2 point)
        {
            return Evaluate(point, GenerationZoneKind.NoSpawn) > 0.01f ||
                   Evaluate(point, GenerationZoneKind.SettlementFootprint) > 0.01f ||
                   Evaluate(point, GenerationZoneKind.PointOfInterestFootprint) > 0.01f ||
                   Evaluate(point, GenerationZoneKind.Road) > 0.05f;
        }

        public float Evaluate(Vector2 point, GenerationZoneKind kind)
        {
            var area = Bounds2D.FromCircle(point, 0.1f);
            var best = 0f;

            var candidateZones = zoneIndex.Query(area);
            for (var i = 0; i < candidateZones.Count; i++)
            {
                var zone = candidateZones[i];
                if (zone.Kind != kind)
                {
                    continue;
                }

                best = Mathf.Max(best, zone.Evaluate(point));
            }

            var candidatePaths = pathIndex.Query(area);
            for (var i = 0; i < candidatePaths.Count; i++)
            {
                var path = candidatePaths[i];
                if (path.Kind != kind)
                {
                    continue;
                }

                best = Mathf.Max(best, path.Evaluate(point));
            }

            return Mathf.Clamp01(best);
        }

        public IReadOnlyList<GenerationMaskZone> QueryZones(Bounds2D area)
        {
            return zoneIndex.Query(area);
        }

        public IReadOnlyList<GenerationPathMask> QueryPaths(Bounds2D area)
        {
            return pathIndex.Query(area);
        }
    }
}
