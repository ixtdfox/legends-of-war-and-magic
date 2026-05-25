using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    internal static class GeneratedTerrainVisuals
    {
        private const int TextureSize = 4;

        public static void Apply(Terrain terrain, ProceduralLocationSettings settings)
        {
            if (terrain == null || terrain.terrainData == null || settings == null)
            {
                return;
            }

            var terrainData = terrain.terrainData;
            terrainData.terrainLayers = new[]
            {
                CreateLayer("Generated Shore", new Color(0.55f, 0.48f, 0.31f, 1f), 9f),
                CreateLayer("Generated Grass", new Color(0.17f, 0.36f, 0.17f, 1f), 12f),
                CreateLayer("Generated Rock", new Color(0.35f, 0.36f, 0.34f, 1f), 10f),
                CreateLayer("Generated Highland", new Color(0.54f, 0.57f, 0.50f, 1f), 11f)
            };

            terrainData.alphamapResolution = Mathf.Clamp(terrainData.heightmapResolution / 2, 64, 256);
            PaintTerrain(terrainData, settings);
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

        private static void PaintTerrain(TerrainData terrainData, ProceduralLocationSettings settings)
        {
            var width = terrainData.alphamapWidth;
            var height = terrainData.alphamapHeight;
            var layerCount = terrainData.terrainLayers.Length;
            var alphas = new float[height, width, layerCount];
            var waterLevel01 = settings.WaterEnabled && settings.TerrainHeight > 0f
                ? Mathf.Clamp01(settings.WaterLevel / settings.TerrainHeight)
                : 0f;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var normalizedHeight = terrainData.GetInterpolatedHeight(nx, ny) / terrainData.size.y;
                    var slope = Vector3.Angle(terrainData.GetInterpolatedNormal(nx, ny), Vector3.up);

                    var shoreWeight = Mathf.Clamp01(1f - Mathf.Abs(normalizedHeight - waterLevel01) / 0.045f);
                    var rockWeight = Mathf.InverseLerp(24f, 48f, slope);
                    var highlandWeight = Mathf.InverseLerp(0.58f, 0.86f, normalizedHeight) * (1f - shoreWeight);
                    var grassWeight = Mathf.Max(0.12f, 1f - shoreWeight - rockWeight * 0.75f - highlandWeight * 0.65f);

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
