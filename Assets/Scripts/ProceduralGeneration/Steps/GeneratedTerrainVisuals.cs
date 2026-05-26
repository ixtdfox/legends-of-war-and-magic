using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    internal static class GeneratedTerrainVisuals
    {
        private const int TextureSize = 4;
        private const int ShoreLayer = 0;
        private const int GrassLayer = 1;
        private const int GrassVariationLayer = 2;
        private const int RockLayer = 3;
        private const int HighlandLayer = 4;

        public static void Apply(Terrain terrain, ProceduralLocationSettings settings, int seed)
        {
            if (terrain == null || terrain.terrainData == null || settings == null)
            {
                return;
            }

            var terrainData = terrain.terrainData;
            terrainData.terrainLayers = new[]
            {
                ResolveLayer(settings, TerrainSurfaceRole.Shore, "Generated Shore", new Color(0.55f, 0.48f, 0.31f, 1f), 9f),
                ResolveLayer(settings, TerrainSurfaceRole.Ground, "Generated Grass", new Color(0.17f, 0.36f, 0.17f, 1f), 12f),
                ResolveResourceLayer("TerrainLayers/Terrain/Fristy_Terrain_Grass_01", "Generated Grass Variation", new Color(0.28f, 0.42f, 0.16f, 1f), 7f),
                ResolveLayer(settings, TerrainSurfaceRole.Rock, "Generated Rock", new Color(0.35f, 0.36f, 0.34f, 1f), 10f),
                ResolveLayer(settings, TerrainSurfaceRole.Highland, "Generated Highland", new Color(0.54f, 0.57f, 0.50f, 1f), 11f)
            };

            terrainData.alphamapResolution = Mathf.Clamp(terrainData.heightmapResolution / 2, 64, 256);
            PaintTerrain(terrainData, settings, seed);
        }

        private static TerrainLayer ResolveLayer(
            ProceduralLocationSettings settings,
            TerrainSurfaceRole role,
            string fallbackName,
            Color fallbackColor,
            float fallbackTileSize)
        {
            var surface = settings.AssetCatalog != null ? settings.AssetCatalog.FindTerrainSurface(role) : null;
            if (surface == null)
            {
                return CreateLayer(fallbackName, fallbackColor, fallbackTileSize);
            }

            if (surface.TerrainLayer != null)
            {
                return surface.TerrainLayer;
            }

            return CreateLayer(surface.SurfaceName, surface.FallbackColor, surface.TileSize);
        }

        private static TerrainLayer ResolveResourceLayer(string resourcePath, string fallbackName, Color fallbackColor, float fallbackTileSize)
        {
            var layer = Resources.Load<TerrainLayer>(resourcePath);
            return layer != null ? layer : CreateLayer(fallbackName, fallbackColor, fallbackTileSize);
        }

        private static TerrainLayer CreateLayer(string layerName, Color color, float tileSize)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"{layerName} Texture",
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[TextureSize * TextureSize];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var layer = new TerrainLayer
            {
                name = layerName,
                diffuseTexture = texture,
                tileSize = new Vector2(tileSize, tileSize),
                hideFlags = HideFlags.HideAndDontSave
            };

            return layer;
        }

        private static void PaintTerrain(TerrainData terrainData, ProceduralLocationSettings settings, int seed)
        {
            var width = terrainData.alphamapWidth;
            var height = terrainData.alphamapHeight;
            var layerCount = terrainData.terrainLayers.Length;
            var alphas = new float[height, width, layerCount];
            var waterLevel01 = settings.WaterEnabled && settings.TerrainHeight > 0f
                ? Mathf.Clamp01(settings.WaterLevel / settings.TerrainHeight)
                : 0f;
            var seedA = (seed & 0xFFFF) * 0.00037f;
            var seedB = ((seed >> 8) & 0xFFFF) * 0.00041f;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var normalizedHeight = terrainData.GetInterpolatedHeight(nx, ny) / terrainData.size.y;
                    var slope = Vector3.Angle(terrainData.GetInterpolatedNormal(nx, ny), Vector3.up);

                    var broadNoise = Mathf.PerlinNoise(nx * 15.5f + seedA, ny * 15.5f + seedB);
                    var fineNoise = Mathf.PerlinNoise(nx * 52.0f + seedB, ny * 52.0f + seedA);
                    var patchNoise = Mathf.PerlinNoise(nx * 7.5f + seedB * 1.7f, ny * 7.5f + seedA * 1.7f);
                    var ridgeNoise = Mathf.PerlinNoise(nx * 24.0f + seedA * 0.7f, ny * 24.0f + seedB * 0.7f);
                    var variation = (broadNoise - 0.5f) * 0.24f + (fineNoise - 0.5f) * 0.08f;
                    var aboveWater = normalizedHeight - waterLevel01;

                    var wetShore = Mathf.Clamp01(1f - Mathf.Abs(aboveWater) / 0.028f);
                    var lowBank = Mathf.Clamp01(1f - Mathf.Abs(aboveWater - 0.028f) / 0.04f);
                    var shoreWeight = Mathf.Clamp01(wetShore * 1.35f + lowBank * 0.22f);

                    var cliffWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(34f, 62f, slope));
                    var flatness = Mathf.Clamp01(1f - Mathf.InverseLerp(12f, 42f, slope));
                    var highElevation = Mathf.InverseLerp(0.58f, 0.9f, normalizedHeight);
                    var rockOutcrop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 0.9f, ridgeNoise)) * Mathf.InverseLerp(18f, 46f, slope);
                    var rockWeight = Mathf.Clamp01(cliffWeight * 1.85f + highElevation * 0.28f + rockOutcrop * 0.72f + variation * 0.2f);

                    var soilPatch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 0.88f, patchNoise)) * flatness;
                    var highlandWeight = Mathf.Clamp01(
                        highElevation * (0.38f + broadNoise * 0.36f) * (1f - cliffWeight * 0.6f) +
                        soilPatch * 0.68f);

                    var grassVariationWeight = Mathf.Clamp01(
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.82f, patchNoise)) *
                        flatness *
                        (0.58f + fineNoise * 0.38f));
                    var grassWeight = Mathf.Clamp01(1.24f + broadNoise * 0.16f - shoreWeight * 1.35f - rockWeight * 1.05f - highlandWeight * 0.52f);

                    if (slope < 18f && aboveWater > 0.06f)
                    {
                        grassWeight += 0.28f + broadNoise * 0.18f;
                    }

                    if (slope > 45f)
                    {
                        rockWeight += Mathf.InverseLerp(45f, 70f, slope) * 1.75f;
                        grassWeight *= 0.28f;
                        grassVariationWeight *= 0.2f;
                    }

                    if (aboveWater < 0.025f)
                    {
                        grassWeight *= Mathf.Clamp01(Mathf.InverseLerp(-0.01f, 0.055f, aboveWater));
                        grassVariationWeight *= Mathf.Clamp01(Mathf.InverseLerp(0.01f, 0.07f, aboveWater));
                        highlandWeight *= 0.25f;
                    }

                    if (aboveWater < -0.01f)
                    {
                        shoreWeight += 0.35f;
                        grassWeight *= 0.15f;
                        grassVariationWeight *= 0.1f;
                    }

                    var total = shoreWeight + grassWeight + grassVariationWeight + rockWeight + highlandWeight;
                    if (total <= 0f)
                    {
                        alphas[y, x, GrassLayer] = 1f;
                        continue;
                    }

                    alphas[y, x, ShoreLayer] = shoreWeight / total;
                    alphas[y, x, GrassLayer] = grassWeight / total;
                    alphas[y, x, GrassVariationLayer] = grassVariationWeight / total;
                    alphas[y, x, RockLayer] = rockWeight / total;
                    alphas[y, x, HighlandLayer] = highlandWeight / total;
                }
            }

            terrainData.SetAlphamaps(0, 0, alphas);
        }
    }
}
