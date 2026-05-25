using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    internal static class GeneratedTerrainVisuals
    {
        private const int TextureSize = 4;

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
                    var variation = (broadNoise - 0.5f) * 0.32f + (fineNoise - 0.5f) * 0.13f;
                    var aboveWater = normalizedHeight - waterLevel01;

                    var wetShore = Mathf.Clamp01(1f - Mathf.Abs(aboveWater) / 0.028f);
                    var lowBank = Mathf.Clamp01(1f - Mathf.Abs(aboveWater - 0.028f) / 0.04f);
                    var shoreWeight = Mathf.Clamp01(wetShore * 1.25f + lowBank * 0.24f);

                    var cliffWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(32f, 58f, slope));
                    var rockWeight = Mathf.Clamp01(cliffWeight * 1.75f + Mathf.InverseLerp(0.56f, 0.86f, normalizedHeight) * 0.55f + variation * 0.28f);
                    var highlandWeight = Mathf.Clamp01(Mathf.InverseLerp(0.48f, 0.92f, normalizedHeight) * (0.75f + broadNoise * 0.38f) * (1f - wetShore));
                    var grassWeight = Mathf.Clamp01(1.15f - shoreWeight * 1.2f - rockWeight * 0.95f - highlandWeight * 0.55f);

                    if (slope < 18f && aboveWater > 0.06f)
                    {
                        grassWeight += 0.22f + broadNoise * 0.15f;
                    }

                    if (slope > 45f)
                    {
                        rockWeight += Mathf.InverseLerp(45f, 70f, slope) * 1.6f;
                        grassWeight *= 0.35f;
                    }

                    if (aboveWater < 0.025f)
                    {
                        grassWeight *= Mathf.Clamp01(Mathf.InverseLerp(-0.01f, 0.055f, aboveWater));
                        highlandWeight *= 0.25f;
                    }

                    var total = shoreWeight + grassWeight + rockWeight + highlandWeight;
                    if (total <= 0f)
                    {
                        alphas[y, x, 1] = 1f;
                        continue;
                    }

                    alphas[y, x, 0] = shoreWeight / total;
                    alphas[y, x, 1] = grassWeight / total;
                    alphas[y, x, 2] = rockWeight / total;
                    alphas[y, x, 3] = highlandWeight / total;
                }
            }

            terrainData.SetAlphamaps(0, 0, alphas);
        }
    }
}
