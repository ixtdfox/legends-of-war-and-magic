using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Core
{
    public sealed class ProceduralTerrainSampler : IProceduralTerrainSampler
    {
        private const float FlatnessWeight = 0.75f;
        private const float HillWeight = 0.6f;
        private const float ElevationWeight = 0.4f;

        private readonly ProceduralLocationSettings settings;
        private readonly int seed;
        private readonly Vector2[] octaveOffsets;
        private readonly Vector2[] ridgeOffsets;
        private readonly Vector2[] valleyOffsets;
        private readonly Vector2 microOffset;
        private readonly float fractalAmplitudeRange;

        public ProceduralTerrainSampler(ProceduralLocationSettings settings, int seed)
        {
            this.settings = settings;
            this.seed = seed;
            WorldBounds = settings != null ? settings.GetWorldBounds() : new Bounds(Vector3.zero, Vector3.zero);

            var random = new System.Random(seed);
            var octaves = Mathf.Max(1, settings != null ? settings.Octaves : 1);
            octaveOffsets = BuildOctaveOffsets(random, octaves);
            ridgeOffsets = BuildOctaveOffsets(random, octaves);
            valleyOffsets = BuildOctaveOffsets(random, Mathf.Max(2, octaves - 2));
            microOffset = new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));
            fractalAmplitudeRange = ResolveFractalAmplitudeRange(octaves, settings != null ? settings.Persistence : 0.45f);
        }

        public Bounds WorldBounds { get; }

        public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
        {
            if (settings == null || !ContainsXZ(worldX, worldZ))
            {
                point = default;
                normal = default;
                return false;
            }

            point = new Vector3(worldX, SampleHeightMeters(worldX, worldZ), worldZ);
            normal = SampleNormal(worldX, worldZ);
            return true;
        }

        public float SampleHeight01(float worldX, float worldZ)
        {
            if (settings == null)
            {
                return 0f;
            }

            var sample = SampleFractalNoise(worldX, worldZ, settings.NoiseScale, settings.Persistence, settings.Lacunarity, octaveOffsets);
            var normalized = Mathf.Clamp01((sample + fractalAmplitudeRange) / Mathf.Max(0.0001f, fractalAmplitudeRange * 2f));
            var shapedHeight = ShapeNaturalTerrain(normalized);
            shapedHeight = ApplyRidgesAndValleys(shapedHeight, worldX, worldZ);
            var withLandShape = ApplyLandShape(shapedHeight, worldX, worldZ);
            return Mathf.Clamp01(withLandShape * settings.HeightMultiplier);
        }

        public float SampleHeightMeters(float worldX, float worldZ)
        {
            return SampleHeight01(worldX, worldZ) * (settings != null ? settings.TerrainHeight : 0f);
        }

        public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
        {
            var distance = Mathf.Max(0.25f, sampleDistance);
            var min = WorldBounds.min;
            var max = WorldBounds.max;
            var leftX = Mathf.Max(min.x, worldX - distance);
            var rightX = Mathf.Min(max.x, worldX + distance);
            var downZ = Mathf.Max(min.z, worldZ - distance);
            var upZ = Mathf.Min(max.z, worldZ + distance);
            var left = SampleHeightMeters(leftX, worldZ);
            var right = SampleHeightMeters(rightX, worldZ);
            var down = SampleHeightMeters(worldX, downZ);
            var up = SampleHeightMeters(worldX, upZ);
            return new Vector3(left - right, Mathf.Max(0.001f, rightX - leftX + upZ - downZ), down - up).normalized;
        }

        public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
        {
            var safeResolution = Mathf.Max(2, resolution);
            var heights = new float[safeResolution, safeResolution];
            var maxIndex = safeResolution - 1f;
            for (var y = 0; y < safeResolution; y++)
            {
                var worldZ = minZ + length * (y / maxIndex);
                for (var x = 0; x < safeResolution; x++)
                {
                    var worldX = minX + width * (x / maxIndex);
                    heights[y, x] = SampleHeight01(worldX, worldZ);
                }
            }

            return heights;
        }

        private bool ContainsXZ(float worldX, float worldZ)
        {
            var min = WorldBounds.min;
            var max = WorldBounds.max;
            return worldX >= min.x && worldX <= max.x && worldZ >= min.z && worldZ <= max.z;
        }

        private static Vector2[] BuildOctaveOffsets(System.Random random, int octaves)
        {
            var offsets = new Vector2[Mathf.Max(1, octaves)];
            for (var i = 0; i < offsets.Length; i++)
            {
                offsets[i] = new Vector2(
                    random.Next(-100000, 100000),
                    random.Next(-100000, 100000));
            }

            return offsets;
        }

        private static float ResolveFractalAmplitudeRange(int octaves, float persistence)
        {
            var amplitude = 1f;
            var range = 0f;
            for (var i = 0; i < Mathf.Max(1, octaves); i++)
            {
                range += amplitude;
                amplitude *= Mathf.Clamp01(persistence);
            }

            return Mathf.Max(0.0001f, range);
        }

        private float SampleFractalNoise(float worldX, float worldZ, float noiseScale, float persistence, float lacunarity, Vector2[] offsets)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var noiseHeight = 0f;
            var scale = Mathf.Max(1f, noiseScale);

            for (var octave = 0; octave < offsets.Length; octave++)
            {
                var sampleX = (worldX / scale) * frequency + offsets[octave].x;
                var sampleZ = (worldZ / scale) * frequency + offsets[octave].y;
                var perlinValue = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;
                noiseHeight += perlinValue * amplitude;
                amplitude *= Mathf.Clamp01(persistence);
                frequency *= Mathf.Max(1f, lacunarity);
            }

            return noiseHeight;
        }

        private float ApplyRidgesAndValleys(float height, float worldX, float worldZ)
        {
            var ridge = SampleRidgedNoise(worldX, worldZ);
            var valley = SampleFractalNoise01(worldX, worldZ, settings.NoiseScale * 1.85f, 2, 0.54f, 1.75f, valleyOffsets);
            var normalized = ToNormalized01(worldX, worldZ);
            var micro = Mathf.PerlinNoise(normalized.x * 38f + microOffset.x, normalized.y * 38f + microOffset.y);

            var elevationMask = SmoothRange(0.22f, 0.9f, height);
            var ridgeMask = SmoothRange(0.46f, 0.9f, ridge);
            var valleyMask = Mathf.Pow(1f - SmoothRange(0.28f, 0.78f, valley), 1.65f);

            var ridgeLift = ridgeMask * settings.RidgeIntensity * Mathf.Lerp(0.08f, 0.24f, elevationMask);
            var valleyCut = valleyMask * settings.ValleyIntensity * Mathf.Lerp(0.13f, 0.04f, elevationMask);
            var microRelief = (micro - 0.5f) * settings.MicroReliefStrength * Mathf.Lerp(0.035f, 0.075f, elevationMask);

            height = Mathf.Clamp01(height + ridgeLift - valleyCut + microRelief);
            height = ApplyTerraces(height, ridgeMask);
            height = ApplyCliffBands(height, ridgeMask, valleyMask);
            return Mathf.Clamp01(height);
        }

        private float SampleRidgedNoise(float worldX, float worldZ)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var maxValue = 0f;
            var ridgeScale = Mathf.Max(1f, settings.NoiseScale * 0.78f);

            for (var octave = 0; octave < ridgeOffsets.Length; octave++)
            {
                var sampleX = (worldX / ridgeScale) * frequency + ridgeOffsets[octave].x;
                var sampleZ = (worldZ / ridgeScale) * frequency + ridgeOffsets[octave].y;
                var perlin = Mathf.PerlinNoise(sampleX, sampleZ);
                var ridge = 1f - Mathf.Abs(perlin * 2f - 1f);
                ridge *= ridge;
                value += ridge * amplitude;
                maxValue += amplitude;
                amplitude *= Mathf.Clamp(settings.Persistence + 0.08f, 0.2f, 0.82f);
                frequency *= Mathf.Max(1.25f, settings.Lacunarity * 1.08f);
            }

            return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);
        }

        private static float SampleFractalNoise01(
            float worldX,
            float worldZ,
            float noiseScale,
            int octaves,
            float persistence,
            float lacunarity,
            Vector2[] octaveOffsets)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var maxValue = 0f;
            var scale = Mathf.Max(1f, noiseScale);

            for (var octave = 0; octave < octaves; octave++)
            {
                var offset = octaveOffsets[Mathf.Min(octave, octaveOffsets.Length - 1)];
                var sampleX = (worldX / scale) * frequency + offset.x;
                var sampleZ = (worldZ / scale) * frequency + offset.y;
                value += Mathf.PerlinNoise(sampleX, sampleZ) * amplitude;
                maxValue += amplitude;
                amplitude *= Mathf.Clamp01(persistence);
                frequency *= Mathf.Max(1f, lacunarity);
            }

            return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);
        }

        private float ApplyTerraces(float height, float ridgeMask)
        {
            if (settings.TerraceStrength <= 0f)
            {
                return height;
            }

            const float terraceCount = 14f;
            var terraceMask = SmoothRange(0.28f, 0.88f, height) * Mathf.Lerp(0.45f, 1f, ridgeMask);
            var stepped = Mathf.Round(height * terraceCount) / terraceCount;
            return Mathf.Lerp(height, stepped, settings.TerraceStrength * terraceMask);
        }

        private float ApplyCliffBands(float height, float ridgeMask, float valleyMask)
        {
            if (settings.CliffIntensity <= 0f)
            {
                return height;
            }

            var cliffMask = SmoothRange(0.50f, 0.94f, ridgeMask) *
                            SmoothRange(0.25f, 0.82f, height) *
                            Mathf.Lerp(0.72f, 1.15f, valleyMask);
            var cliffStrength = settings.CliffIntensity * cliffMask;
            if (cliffStrength <= 0f)
            {
                return height;
            }

            var shelfCount = Mathf.Lerp(12f, 22f, settings.CliffIntensity);
            var shelved = Mathf.Floor(height * shelfCount) / shelfCount;
            var sharpened = Mathf.Pow(height, Mathf.Lerp(1f, 0.82f, Mathf.Clamp01(cliffStrength)));
            return Mathf.Clamp01(Mathf.Lerp(height, Mathf.Lerp(shelved, sharpened, 0.45f), Mathf.Clamp01(cliffStrength * 0.48f)));
        }

        private static float ShapeNaturalTerrain(float normalizedHeight)
        {
            var flatComponent = Mathf.Pow(normalizedHeight, 1.75f) * FlatnessWeight;
            var hillComponent = Mathf.Pow(normalizedHeight, 1.2f) * HillWeight;
            var elevationComponent = Mathf.Pow(normalizedHeight, 2.5f) * ElevationWeight;
            return Mathf.Clamp01((flatComponent + hillComponent + elevationComponent) / (FlatnessWeight + HillWeight + ElevationWeight));
        }

        private float ApplyLandShape(float height, float worldX, float worldZ)
        {
            return settings.LandShape switch
            {
                TerrainLandShape.Islands => ApplyIslandFalloff(height, worldX, worldZ, 0.94f),
                TerrainLandShape.Archipelago => ApplyArchipelagoFalloff(height, worldX, worldZ),
                _ => ApplyEdgeFalloffIfEnabled(height, worldX, worldZ)
            };
        }

        private float ApplyEdgeFalloffIfEnabled(float height, float worldX, float worldZ)
        {
            if (!settings.UseEdgeFalloff)
            {
                return height;
            }

            var normalized = ToSignedNormalized(worldX, worldZ);
            var distanceFromCenter = Mathf.Max(Mathf.Abs(normalized.x), Mathf.Abs(normalized.y));
            return ApplyFalloff(height, distanceFromCenter, 0.7f);
        }

        private float ApplyIslandFalloff(float height, float worldX, float worldZ, float edgeReductionMultiplier)
        {
            var normalized = ToSignedNormalized(worldX, worldZ);
            var distanceFromCenter = Mathf.Sqrt(normalized.x * normalized.x + normalized.y * normalized.y) / 1.4142135f;
            return ApplyFalloff(height, distanceFromCenter, edgeReductionMultiplier);
        }

        private float ApplyArchipelagoFalloff(float height, float worldX, float worldZ)
        {
            var normalized = ToSignedNormalized(worldX, worldZ);
            var offsetA = ((seed & 0xFFFF) / 65535f) * 19.73f;
            var offsetB = (((seed >> 8) & 0xFFFF) / 65535f) * 23.19f;
            var clusterNoise = Mathf.PerlinNoise((normalized.x + offsetA) * 3.15f, (normalized.y + offsetB) * 3.15f);
            var islandMask = SmoothRange(0.34f, 0.78f, clusterNoise);
            var clusteredHeight = height * Mathf.Lerp(0.16f, 1f, islandMask);
            return ApplyIslandFalloff(clusteredHeight, worldX, worldZ, 0.98f);
        }

        private float ApplyFalloff(float height, float distanceFromCenter, float edgeReductionMultiplier)
        {
            if (distanceFromCenter <= settings.EdgeFalloffStart)
            {
                return height;
            }

            var falloffRange = Mathf.Max(1e-5f, 1f - settings.EdgeFalloffStart);
            var falloffT = Mathf.Clamp01((distanceFromCenter - settings.EdgeFalloffStart) / falloffRange);
            var edgeReduction = Mathf.Pow(falloffT, settings.EdgeFalloffStrength);
            return height * (1f - edgeReduction * edgeReductionMultiplier);
        }

        private Vector2 ToNormalized01(float worldX, float worldZ)
        {
            var min = WorldBounds.min;
            var max = WorldBounds.max;
            return new Vector2(
                Mathf.InverseLerp(min.x, max.x, worldX),
                Mathf.InverseLerp(min.z, max.z, worldZ));
        }

        private Vector2 ToSignedNormalized(float worldX, float worldZ)
        {
            var normalized = ToNormalized01(worldX, worldZ);
            return normalized * 2f - Vector2.one;
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }
    }
}
