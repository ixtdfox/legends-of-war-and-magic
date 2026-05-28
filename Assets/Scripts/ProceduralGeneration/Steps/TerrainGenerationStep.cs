using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Generates a bounded Unity Terrain from layered noise.
    /// </summary>
    public sealed class TerrainGenerationStep : IGenerationStep
    {
        private const float FlatnessWeight = 0.75f;
        private const float HillWeight = 0.6f;
        private const float ElevationWeight = 0.4f;

        public void Execute(GenerationContext context)
        {
            var settings = context.Settings;
            var terrainData = new TerrainData
            {
                heightmapResolution = SanitizeHeightmapResolution(settings.HeightmapResolution),
                size = new Vector3(settings.WorldWidth, settings.TerrainHeight, settings.WorldLength)
            };

            terrainData.SetHeights(0, 0, BuildHeightMap(terrainData.heightmapResolution, settings, context.Seed));

            var terrainRoot = new GameObject("GeneratedTerrain").transform;
            terrainRoot.SetParent(context.GeneratedRoot, false);

            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "TerrainSurface";
            terrainObject.transform.SetParent(terrainRoot, false);
            terrainObject.transform.position = new Vector3(-settings.WorldWidth * 0.5f, 0f, -settings.WorldLength * 0.5f);
            context.GeneratedTerrain = terrainObject.GetComponent<Terrain>();
            ConfigureTerrainRenderCost(context.GeneratedTerrain);
            GeneratedTerrainVisuals.Apply(context.GeneratedTerrain, settings, context.Seed);
            context.RecordSpawn("Terrain", 1);
        }

        private static void ConfigureTerrainRenderCost(Terrain terrain)
        {
            if (terrain == null)
            {
                return;
            }

            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.heightmapPixelError = 8f;
        }

        private static int SanitizeHeightmapResolution(int requestedResolution)
        {
            var clamped = Mathf.Clamp(requestedResolution, 33, 4097);
            return Mathf.ClosestPowerOfTwo(clamped - 1) + 1;
        }

        public static float[,] BuildPreviewHeightMap(int requestedResolution, ProceduralLocationSettings settings, int seed)
        {
            return BuildHeightMap(SanitizeHeightmapResolution(requestedResolution), settings, seed);
        }

        private static float[,] BuildHeightMap(int resolution, ProceduralLocationSettings settings, int seed)
        {
            var heights = new float[resolution, resolution];
            var random = new System.Random(seed);
            var octaveOffsets = BuildOctaveOffsets(random, settings.Octaves);
            var ridgeOffsets = BuildOctaveOffsets(random, settings.Octaves);
            var valleyOffsets = BuildOctaveOffsets(random, Mathf.Max(2, settings.Octaves - 2));
            var microOffset = new Vector2(random.Next(-100000, 100000), random.Next(-100000, 100000));

            var maxNoiseHeight = float.MinValue;
            var minNoiseHeight = float.MaxValue;
            var noiseMap = new float[resolution, resolution];

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var sample = SampleFractalNoise(x, y, resolution, settings, octaveOffsets);
                    noiseMap[y, x] = sample;
                    if (sample > maxNoiseHeight) maxNoiseHeight = sample;
                    if (sample < minNoiseHeight) minNoiseHeight = sample;
                }
            }

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var normalized = Mathf.InverseLerp(minNoiseHeight, maxNoiseHeight, noiseMap[y, x]);
                    var shapedHeight = ShapeNaturalTerrain(normalized);
                    shapedHeight = ApplyRidgesAndValleys(shapedHeight, x, y, resolution, settings, ridgeOffsets, valleyOffsets, microOffset);
                    var withLandShape = ApplyLandShape(shapedHeight, x, y, resolution, settings, seed);
                    heights[y, x] = Mathf.Clamp01(withLandShape * settings.HeightMultiplier);
                }
            }

            return heights;
        }

        private static Vector2[] BuildOctaveOffsets(System.Random random, int octaves)
        {
            var offsets = new Vector2[octaves];
            for (var i = 0; i < octaves; i++)
            {
                offsets[i] = new Vector2(
                    random.Next(-100000, 100000),
                    random.Next(-100000, 100000));
            }

            return offsets;
        }

        private static float SampleFractalNoise(int x, int y, int resolution, ProceduralLocationSettings settings, Vector2[] octaveOffsets)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var noiseHeight = 0f;
            var halfResolution = resolution * 0.5f;

            for (var octave = 0; octave < settings.Octaves; octave++)
            {
                var sampleX = ((x - halfResolution) / settings.NoiseScale) * frequency + octaveOffsets[octave].x;
                var sampleY = ((y - halfResolution) / settings.NoiseScale) * frequency + octaveOffsets[octave].y;

                var perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
                noiseHeight += perlinValue * amplitude;

                amplitude *= settings.Persistence;
                frequency *= settings.Lacunarity;
            }

            return noiseHeight;
        }

        private static float ApplyRidgesAndValleys(
            float height,
            int x,
            int y,
            int resolution,
            ProceduralLocationSettings settings,
            Vector2[] ridgeOffsets,
            Vector2[] valleyOffsets,
            Vector2 microOffset)
        {
            var ridge = SampleRidgedNoise(x, y, resolution, settings, ridgeOffsets);
            var valley = SampleFractalNoise01(x, y, resolution, settings.NoiseScale * 1.85f, 2, 0.54f, 1.75f, valleyOffsets);
            var micro = Mathf.PerlinNoise(
                (x / Mathf.Max(1f, resolution - 1f)) * 38f + microOffset.x,
                (y / Mathf.Max(1f, resolution - 1f)) * 38f + microOffset.y);

            var elevationMask = SmoothRange(0.22f, 0.9f, height);
            var ridgeMask = SmoothRange(0.46f, 0.9f, ridge);
            var valleyMask = Mathf.Pow(1f - SmoothRange(0.28f, 0.78f, valley), 1.65f);

            var ridgeLift = ridgeMask * settings.RidgeIntensity * Mathf.Lerp(0.08f, 0.24f, elevationMask);
            var valleyCut = valleyMask * settings.ValleyIntensity * Mathf.Lerp(0.13f, 0.04f, elevationMask);
            var microRelief = (micro - 0.5f) * settings.MicroReliefStrength * Mathf.Lerp(0.035f, 0.075f, elevationMask);

            height = Mathf.Clamp01(height + ridgeLift - valleyCut + microRelief);
            height = ApplyTerraces(height, settings, ridgeMask);
            height = ApplyCliffBands(height, settings, ridgeMask, valleyMask);
            return Mathf.Clamp01(height);
        }

        private static float SampleRidgedNoise(
            int x,
            int y,
            int resolution,
            ProceduralLocationSettings settings,
            Vector2[] octaveOffsets)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var maxValue = 0f;
            var halfResolution = resolution * 0.5f;
            var ridgeScale = Mathf.Max(1f, settings.NoiseScale * 0.78f);

            for (var octave = 0; octave < octaveOffsets.Length; octave++)
            {
                var sampleX = ((x - halfResolution) / ridgeScale) * frequency + octaveOffsets[octave].x;
                var sampleY = ((y - halfResolution) / ridgeScale) * frequency + octaveOffsets[octave].y;
                var perlin = Mathf.PerlinNoise(sampleX, sampleY);
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
            int x,
            int y,
            int resolution,
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
            var halfResolution = resolution * 0.5f;

            for (var octave = 0; octave < octaves; octave++)
            {
                var offset = octaveOffsets[Mathf.Min(octave, octaveOffsets.Length - 1)];
                var sampleX = ((x - halfResolution) / Mathf.Max(1f, noiseScale)) * frequency + offset.x;
                var sampleY = ((y - halfResolution) / Mathf.Max(1f, noiseScale)) * frequency + offset.y;
                value += Mathf.PerlinNoise(sampleX, sampleY) * amplitude;
                maxValue += amplitude;
                amplitude *= Mathf.Clamp01(persistence);
                frequency *= Mathf.Max(1f, lacunarity);
            }

            return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);
        }

        private static float ApplyTerraces(float height, ProceduralLocationSettings settings, float ridgeMask)
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

        private static float ApplyCliffBands(float height, ProceduralLocationSettings settings, float ridgeMask, float valleyMask)
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

        private static float ApplyEdgeFalloffIfEnabled(float height, int x, int y, int resolution, ProceduralLocationSettings settings)
        {
            if (!settings.UseEdgeFalloff)
            {
                return height;
            }

            var nx = x / (float)(resolution - 1) * 2f - 1f;
            var ny = y / (float)(resolution - 1) * 2f - 1f;
            var distanceFromCenter = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));

            if (distanceFromCenter <= settings.EdgeFalloffStart)
            {
                return height;
            }

            var falloffRange = Mathf.Max(1e-5f, 1f - settings.EdgeFalloffStart);
            var falloffT = Mathf.Clamp01((distanceFromCenter - settings.EdgeFalloffStart) / falloffRange);
            var edgeReduction = Mathf.Pow(falloffT, settings.EdgeFalloffStrength);

            return height * (1f - edgeReduction * 0.7f);
        }

        private static float ApplyLandShape(float height, int x, int y, int resolution, ProceduralLocationSettings settings, int seed)
        {
            return settings.LandShape switch
            {
                TerrainLandShape.Islands => ApplyIslandFalloff(height, x, y, resolution, settings, 0.94f),
                TerrainLandShape.Archipelago => ApplyArchipelagoFalloff(height, x, y, resolution, settings, seed),
                _ => ApplyEdgeFalloffIfEnabled(height, x, y, resolution, settings)
            };
        }

        private static float ApplyIslandFalloff(
            float height,
            int x,
            int y,
            int resolution,
            ProceduralLocationSettings settings,
            float edgeReductionMultiplier)
        {
            var nx = x / (float)(resolution - 1) * 2f - 1f;
            var ny = y / (float)(resolution - 1) * 2f - 1f;
            var distanceFromCenter = Mathf.Sqrt(nx * nx + ny * ny) / 1.4142135f;

            if (distanceFromCenter <= settings.EdgeFalloffStart)
            {
                return height;
            }

            var falloffRange = Mathf.Max(1e-5f, 1f - settings.EdgeFalloffStart);
            var falloffT = Mathf.Clamp01((distanceFromCenter - settings.EdgeFalloffStart) / falloffRange);
            var edgeReduction = Mathf.Pow(falloffT, settings.EdgeFalloffStrength);
            return height * (1f - edgeReduction * edgeReductionMultiplier);
        }

        private static float ApplyArchipelagoFalloff(float height, int x, int y, int resolution, ProceduralLocationSettings settings, int seed)
        {
            var nx = x / (float)(resolution - 1) * 2f - 1f;
            var ny = y / (float)(resolution - 1) * 2f - 1f;
            var offsetA = ((seed & 0xFFFF) / 65535f) * 19.73f;
            var offsetB = (((seed >> 8) & 0xFFFF) / 65535f) * 23.19f;
            var clusterNoise = Mathf.PerlinNoise((nx + offsetA) * 3.15f, (ny + offsetB) * 3.15f);
            var islandMask = SmoothRange(0.34f, 0.78f, clusterNoise);
            var clusteredHeight = height * Mathf.Lerp(0.16f, 1f, islandMask);
            return ApplyIslandFalloff(clusteredHeight, x, y, resolution, settings, 0.98f);
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }
    }
}
