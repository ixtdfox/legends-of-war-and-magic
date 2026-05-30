using System;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public sealed class TerrainSampleCache
    {
        private readonly IProceduralTerrainSampler sampler;
        private readonly float cellSize;
        private readonly System.Collections.Generic.Dictionary<Vector2Int, CachedSample> samples = new();

        public TerrainSampleCache(IProceduralTerrainSampler sampler, float cellSize)
        {
            this.sampler = sampler;
            this.cellSize = Mathf.Max(0.5f, cellSize);
        }

        public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
        {
            var key = new Vector2Int(Mathf.RoundToInt(worldX / cellSize), Mathf.RoundToInt(worldZ / cellSize));
            if (samples.TryGetValue(key, out var cached))
            {
                point = cached.Point;
                normal = cached.Normal;
                return cached.Valid;
            }

            point = default;
            normal = Vector3.up;
            var valid = sampler != null && sampler.TrySample(worldX, worldZ, out point, out normal);
            samples[key] = new CachedSample(valid, point, normal);
            return valid;
        }

        public float SampleSlope(float worldX, float worldZ)
        {
            return TrySample(worldX, worldZ, out _, out var normal)
                ? Vector3.Angle(normal, Vector3.up)
                : 90f;
        }

        private readonly struct CachedSample
        {
            public CachedSample(bool valid, Vector3 point, Vector3 normal)
            {
                Valid = valid;
                Point = point;
                Normal = normal;
            }

            public bool Valid { get; }
            public Vector3 Point { get; }
            public Vector3 Normal { get; }
        }
    }

    public static class DeterministicRandom
    {
        public static float Next01(this System.Random random)
        {
            return (float)(random ?? throw new ArgumentNullException(nameof(random))).NextDouble();
        }

        public static float Range(this System.Random random, float minInclusive, float maxInclusive)
        {
            return minInclusive + (maxInclusive - minInclusive) * random.Next01();
        }

        public static int Range(this System.Random random, int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxInclusive + 1);
        }

        public static bool Chance(this System.Random random, float probability)
        {
            return random.Next01() <= Mathf.Clamp01(probability);
        }

        public static int Hash(int seed, int salt)
        {
            unchecked
            {
                var hash = seed == 0 ? 17 : seed;
                hash = hash * 397 ^ salt;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return hash == 0 ? 1 : hash;
            }
        }
    }
}
