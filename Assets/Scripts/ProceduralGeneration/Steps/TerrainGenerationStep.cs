using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

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

            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "GeneratedTerrain";
            terrainObject.transform.SetParent(context.GeneratedRoot, false);
            terrainObject.transform.position = new Vector3(-settings.WorldWidth * 0.5f, 0f, -settings.WorldLength * 0.5f);
        }

        private static int SanitizeHeightmapResolution(int requestedResolution)
        {
            var clamped = Mathf.Clamp(requestedResolution, 33, 4097);
            return Mathf.ClosestPowerOfTwo(clamped - 1) + 1;
        }

        private static float[,] BuildHeightMap(int resolution, ProceduralLocationSettings settings, int seed)
        {
            var heights = new float[resolution, resolution];
            var random = new System.Random(seed);
            var octaveOffsets = BuildOctaveOffsets(random, settings.Octaves);

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
                    var withEdgeFalloff = ApplyEdgeFalloffIfEnabled(shapedHeight, x, y, resolution, settings);
                    heights[y, x] = Mathf.Clamp01(withEdgeFalloff * settings.HeightMultiplier);
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
    }
}
