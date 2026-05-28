using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    internal static class GeneratedTerrainVisuals
    {
        private const int TextureSize = 128;
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
                ResolveLayer(settings, TerrainSurfaceRole.Shore, "Generated Shore", new Color(0.32f, 0.29f, 0.22f, 1f), 7f),
                CreateLayer("Generated Grass", new Color(0.16f, 0.24f, 0.10f, 1f), 3.5f),
                CreateLayer("Generated Grass Variation", new Color(0.34f, 0.32f, 0.17f, 1f), 4.5f),
                ResolveLayer(settings, TerrainSurfaceRole.Rock, "Generated Rock", new Color(0.43f, 0.44f, 0.39f, 1f), 7f),
                CreateLayer("Generated Highland", new Color(0.39f, 0.34f, 0.21f, 1f), 5.5f)
            };

            terrainData.alphamapResolution = Mathf.Clamp(terrainData.heightmapResolution / 2, 64, 256);
            PaintTerrain(terrain, settings, seed);
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

            var surfaceColor = Color.Lerp(fallbackColor, surface.FallbackColor, 0.35f);
            if (surface.TerrainLayer != null)
            {
                var tileSize = surface.TileSize > 0f ? surface.TileSize : fallbackTileSize;
                return CreateLayerCopy(surface.TerrainLayer, surface.SurfaceName, surfaceColor, tileSize);
            }

            var fallbackSurfaceTileSize = surface.TileSize > 0f ? surface.TileSize : fallbackTileSize;
            return CreateLayer(surface.SurfaceName, surfaceColor, fallbackSurfaceTileSize);
        }

        private static TerrainLayer ResolveResourceLayer(string resourcePath, string fallbackName, Color fallbackColor, float fallbackTileSize)
        {
            var layer = Resources.Load<TerrainLayer>(resourcePath);
            return layer != null ? CreateLayerCopy(layer, fallbackName, fallbackColor, fallbackTileSize) : CreateLayer(fallbackName, fallbackColor, fallbackTileSize);
        }

        private static TerrainLayer CreateLayer(string layerName, Color color, float tileSize)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = $"{layerName} Texture",
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            var lowerName = layerName.ToLowerInvariant();
            var isVariation = lowerName.Contains("variation");
            var isHighland = lowerName.Contains("highland");
            var isShore = lowerName.Contains("shore");
            var isRock = lowerName.Contains("rock");
            var mossColor = isRock
                ? new Color(0.32f, 0.34f, 0.29f, 1f)
                : new Color(0.11f, 0.19f, 0.075f, 1f);
            var dryColor = isHighland || isVariation
                ? new Color(0.48f, 0.40f, 0.22f, 1f)
                : new Color(0.38f, 0.34f, 0.17f, 1f);
            var litterColor = isShore
                ? new Color(0.26f, 0.23f, 0.17f, 1f)
                : new Color(0.22f, 0.18f, 0.10f, 1f);
            var pixels = new Color[TextureSize * TextureSize];
            for (var i = 0; i < pixels.Length; i++)
            {
                var x = i % TextureSize;
                var y = i / TextureSize;
                var seed = layerName.GetHashCode();
                var broad = Mathf.PerlinNoise(x * 0.055f + seed * 0.0013f, y * 0.055f + seed * 0.0017f);
                var medium = Mathf.PerlinNoise(x * 0.18f + seed * 0.0021f, y * 0.18f + seed * 0.0029f);
                var fine = Mathf.PerlinNoise(x * 0.72f + seed * 0.0031f, y * 0.72f + seed * 0.0037f);
                var dryPatch = Mathf.PerlinNoise(x * 0.038f + seed * 0.0041f, y * 0.038f + seed * 0.0047f);
                var litterPatch = Mathf.PerlinNoise(x * 0.31f + seed * 0.0053f, y * 0.31f + seed * 0.0059f);
                var shade = Mathf.Lerp(0.66f, 1.04f, broad * 0.56f + medium * 0.32f + fine * 0.12f);
                var tone = Color.Lerp(mossColor, color, isRock ? 0.82f : 0.68f);
                tone = Color.Lerp(tone, dryColor, Mathf.SmoothStep(0.50f, 0.88f, dryPatch) * (isVariation || isHighland ? 0.58f : 0.38f));
                tone = Color.Lerp(tone, litterColor, Mathf.SmoothStep(0.66f, 0.96f, litterPatch) * 0.22f);
                pixels[i] = new Color(
                    Mathf.Clamp01(tone.r * shade),
                    Mathf.Clamp01(tone.g * shade),
                    Mathf.Clamp01(tone.b * shade),
                    1f);
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

            ApplyForestTerrainTone(layer, Color.white);
            return layer;
        }

        private static TerrainLayer CreateLayerCopy(TerrainLayer source, string layerName, Color color, float tileSize)
        {
            var layer = Object.Instantiate(source);
            layer.name = $"{layerName} Forest Tone";
            layer.hideFlags = HideFlags.HideAndDontSave;
            if (tileSize > 0f)
            {
                layer.tileSize = new Vector2(tileSize, tileSize);
            }

            ApplyForestTerrainTone(layer, color);
            return layer;
        }

        private static void ApplyForestTerrainTone(TerrainLayer layer, Color color)
        {
            if (layer == null)
            {
                return;
            }

            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(color.r, color.g, color.b, 1f);
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            layer.metallic = 0f;
            layer.smoothness = 0.12f;
            layer.specular = new Color(0.035f, 0.036f, 0.034f, 1f);
        }

        private static float Hash01(int seed, int x, int y)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)(x * 374761393);
                hash ^= (uint)(y * 668265263);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        private static void PaintTerrain(Terrain terrain, ProceduralLocationSettings settings, int seed)
        {
            var terrainData = terrain.terrainData;
            var terrainPosition = terrain.transform.position;
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
                    var worldX = terrainPosition.x + nx * terrainData.size.x;
                    var worldZ = terrainPosition.z + ny * terrainData.size.z;
                    var worldNx = Mathf.InverseLerp(-settings.WorldWidth * 0.5f, settings.WorldWidth * 0.5f, worldX);
                    var worldNz = Mathf.InverseLerp(-settings.WorldLength * 0.5f, settings.WorldLength * 0.5f, worldZ);
                    var normalizedHeight = terrainData.GetInterpolatedHeight(nx, ny) / terrainData.size.y;
                    var slope = Vector3.Angle(terrainData.GetInterpolatedNormal(nx, ny), Vector3.up);

                    var broadNoise = Mathf.PerlinNoise(worldNx * 15.5f + seedA, worldNz * 15.5f + seedB);
                    var fineNoise = Mathf.PerlinNoise(worldNx * 52.0f + seedB, worldNz * 52.0f + seedA);
                    var patchNoise = Mathf.PerlinNoise(worldNx * 7.5f + seedB * 1.7f, worldNz * 7.5f + seedA * 1.7f);
                    var ridgeNoise = Mathf.PerlinNoise(worldNx * 24.0f + seedA * 0.7f, worldNz * 24.0f + seedB * 0.7f);
                    var variation = (broadNoise - 0.5f) * 0.18f + (fineNoise - 0.5f) * 0.08f;
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
                        highElevation * (0.42f + broadNoise * 0.34f) * (1f - cliffWeight * 0.58f) +
                        soilPatch * 0.95f);

                    var grassVariationWeight = Mathf.Clamp01(
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.82f, patchNoise)) *
                        flatness *
                        (0.72f + fineNoise * 0.42f));
                    var grassWeight = Mathf.Clamp01(0.98f + broadNoise * 0.12f - shoreWeight * 1.30f - rockWeight * 1.05f - highlandWeight * 0.62f);

                    if (slope < 18f && aboveWater > 0.06f)
                    {
                        grassWeight += 0.12f + broadNoise * 0.10f;
                        grassVariationWeight += soilPatch * 0.16f;
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
