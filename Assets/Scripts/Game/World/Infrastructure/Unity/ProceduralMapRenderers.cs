using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.Generator.Location;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using UnityEngine;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;
using static LegendsOfWarAndMagic.Game.World.Infrastructure.Unity.MapRendererCommon;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Unity
{
    public sealed class TextureWorldMapRenderer : IWorldMapRenderer
    {
        public WorldMapRenderResult Render(GeneratedWorld world, string outputPath, WorldMapRenderSettings settings)
        {
            var safeSettings = settings ?? new WorldMapRenderSettings();
            var width = safeSettings.Width;
            var height = safeSettings.Height;
            var raster = WorldMapRaster.Build(world, width, height);
            var pixels = new Color[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = Index(x, y, width);
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var paper = PaperColor(nx, ny, world.Seed.Value);
                    var grain = Fractal01(nx * 38f, ny * 38f, world.Seed.Value + 7109, 3, 0.52f, 2.1f) - 0.5f;

                    if (!raster.Land[index])
                    {
                        var shallow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.18f, 0.04f, raster.ShapeScore[index]));
                        var seaNoise = Fractal01(nx * 8.5f, ny * 8.5f, world.Seed.Value + 911, 4, 0.55f, 2.0f);
                        var deepSea = new Color(0.12f, 0.31f, 0.42f, 1f);
                        var coastalSea = new Color(0.35f, 0.58f, 0.61f, 1f);
                        var water = Color.Lerp(deepSea, coastalSea, shallow * 0.72f + seaNoise * 0.10f);
                        pixels[index] = Color.Lerp(water, paper, 0.15f + Mathf.Abs(grain) * 0.08f);
                        continue;
                    }

                    var biomeColor = BiomeMapColor(raster.Biomes[index]);
                    var elevation = raster.Elevation[index];
                    var hillShade = HillShade(raster.Elevation, raster.Land, x, y, width, height, 1.15f);
                    var coastFade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.00f, 0.09f, raster.ShapeScore[index]));
                    var color = Color.Lerp(paper, biomeColor, 0.74f);
                    color = ScaleColor(color, hillShade + elevation * 0.16f + grain * 0.08f);

                    if (coastFade < 0.55f)
                    {
                        color = Color.Lerp(new Color(0.78f, 0.69f, 0.45f, 1f), color, coastFade);
                    }

                    pixels[index] = color;
                }
            }

            PaintCoastRim(pixels, raster, width, height);
            PaintBiomeTexture(pixels, raster, width, height, world.Seed.Value);
            PaintLakes(pixels, width, height, world.Lakes, world.Seed.Value);
            PaintRivers(pixels, width, height, world.Rivers);
            PaintMountains(pixels, width, height, world.MountainRanges);
            PaintConnections(pixels, width, height, world.Locations);
            PaintLocationMarkers(pixels, width, height, world.Locations);
            PaintMapBorder(pixels, width, height);

            SavePng(pixels, width, height, "Generated World Map", outputPath);
            var markers = world.Locations
                .Select(location => new WorldMapPoint(location.Id, location.WorldMapPosition, location.Name.Value))
                .ToArray();
            return new WorldMapRenderResult(outputPath, markers);
        }
    }

    public sealed class TextureLocationMapRenderer : ILocationMapRenderer
    {
        public LocationMapRenderResult Render(GeneratedWorld world, WorldLocation location, string outputPath, LocationMapRenderSettings settings)
        {
            var safeSettings = settings ?? new LocationMapRenderSettings();
            var width = safeSettings.Width;
            var height = safeSettings.Height;
            var pixels = new Color[width * height];
            var terrainSettings = LocationTerrainSettingsFactory.Create(location);
            var heightMap = TerrainGenerationStep.BuildPreviewHeightMap(terrainSettings.HeightmapResolution, terrainSettings, location.TerrainSeed);
            var waterLevel = terrainSettings.WaterEnabled ? terrainSettings.WaterLevel / Mathf.Max(1f, terrainSettings.TerrainHeight) : -1f;
            var baseBiome = BiomeMapColor(location.DominantBiome);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var index = Index(x, y, width);
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var elevation = SampleHeight(heightMap, nx, ny);
                    var shade = LocalHillShade(heightMap, nx, ny);
                    var slope = LocalSlope(heightMap, nx, ny);
                    var grain = Fractal01(nx * 42f, ny * 42f, location.TerrainSeed + 5003, 3, 0.52f, 2.0f) - 0.5f;

                    if (terrainSettings.WaterEnabled && elevation <= waterLevel)
                    {
                        var depth = Mathf.Clamp01((waterLevel - elevation) * 8f);
                        var water = Color.Lerp(new Color(0.23f, 0.50f, 0.56f, 1f), new Color(0.08f, 0.28f, 0.44f, 1f), depth);
                        pixels[index] = ScaleColor(water, 0.94f + grain * 0.05f);
                        continue;
                    }

                    var color = Color.Lerp(new Color(0.72f, 0.66f, 0.48f, 1f), baseBiome, 0.78f);
                    color = Color.Lerp(color, new Color(0.56f, 0.53f, 0.45f, 1f), Mathf.Clamp01(slope * 1.4f));
                    color = Color.Lerp(color, new Color(0.90f, 0.90f, 0.82f, 1f), Mathf.SmoothStep(0.70f, 0.96f, elevation));
                    color = ScaleColor(color, shade + grain * 0.08f);

                    var contour = Mathf.Abs(Mathf.Repeat(elevation * 16f, 1f) - 0.5f);
                    if (contour > 0.47f && elevation > waterLevel + 0.02f)
                    {
                        color = Color.Lerp(color, new Color(0.18f, 0.15f, 0.10f, 1f), 0.18f);
                    }

                    pixels[index] = color;
                }
            }

            PaintLocalWaterEdges(pixels, heightMap, waterLevel, terrainSettings.WaterEnabled, width, height);
            PaintLocalFeatures(pixels, width, height, location, waterLevel);
            PaintGateways(pixels, width, height, location.Gateways);
            PaintMapBorder(pixels, width, height);
            SavePng(pixels, width, height, $"Location Map - {location.Name.Value}", outputPath);
            DestroySettings(terrainSettings);
            return new LocationMapRenderResult(outputPath);
        }
    }

    internal sealed class WorldMapRaster
    {
        private WorldMapRaster(int width, int height)
        {
            Land = new bool[width * height];
            Elevation = new float[width * height];
            ShapeScore = new float[width * height];
            Biomes = new BiomeType[width * height];
        }

        public bool[] Land { get; }
        public float[] Elevation { get; }
        public float[] ShapeScore { get; }
        public BiomeType[] Biomes { get; }

        public static WorldMapRaster Build(GeneratedWorld world, int width, int height)
        {
            var raster = new WorldMapRaster(width, height);
            var regions = BuildRegionInfos(world).ToArray();
            var seed = world.Seed.Value;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var index = Index(x, y, width);
                    var noise = Fractal01(nx * 5.6f, ny * 5.6f, seed + 11, 4, 0.55f, 2.05f);
                    var detail = Fractal01(nx * 14.0f, ny * 14.0f, seed + 29, 3, 0.52f, 2.1f);
                    var score = ShapeScore(world.Shape.Type, nx, ny, noise, detail);
                    score = Mathf.Max(score, LocationLandAnchor(world.Locations, nx, ny));

                    var land = score > 0f;
                    raster.ShapeScore[index] = score;
                    raster.Land[index] = land;
                    if (!land)
                    {
                        raster.Elevation[index] = 0f;
                        raster.Biomes[index] = BiomeType.LakeDistrict;
                        continue;
                    }

                    var elevation = MacroElevation(world.Shape.Type, nx, ny, score, seed);
                    elevation = Mathf.Clamp01(elevation + MountainInfluence(world.MountainRanges, nx, ny));
                    raster.Elevation[index] = elevation;
                    raster.Biomes[index] = ChooseBiome(world, regions, nx, ny, elevation, score);
                }
            }

            return raster;
        }

        private static IEnumerable<RegionInfo> BuildRegionInfos(GeneratedWorld world)
        {
            foreach (var region in world.Regions)
            {
                var points = region.Boundary.Points.ToArray();
                var center = points.Length == 0
                    ? RegionCenterFromLocations(world, region)
                    : new MapPoint(points.Average(point => point.X), points.Average(point => point.Y));
                yield return new RegionInfo(center, region.DominantBiome, region.SecondaryBiomes.ToArray());
            }
        }

        private static MapPoint RegionCenterFromLocations(GeneratedWorld world, WorldRegion region)
        {
            var locations = world.Locations.Where(location => location.RegionId.Equals(region.Id)).ToArray();
            return locations.Length == 0
                ? new MapPoint(0.5f, 0.5f)
                : new MapPoint(locations.Average(location => location.WorldMapPosition.X), locations.Average(location => location.WorldMapPosition.Y));
        }

        private static float LocationLandAnchor(IEnumerable<WorldLocation> locations, float x, float y)
        {
            var score = -1f;
            foreach (var location in locations)
            {
                var dx = x - location.WorldMapPosition.X;
                var dy = y - location.WorldMapPosition.Y;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                score = Mathf.Max(score, 0.050f - distance * 1.65f);
            }

            return score;
        }

        private static BiomeType ChooseBiome(GeneratedWorld world, IReadOnlyList<RegionInfo> regions, float x, float y, float elevation, float coastScore)
        {
            if (elevation > 0.86f)
            {
                return BiomeType.SnowPeaks;
            }

            if (elevation > 0.69f)
            {
                return BiomeType.Mountains;
            }

            var nearest = regions.Count > 0 ? regions[0] : new RegionInfo(new MapPoint(0.5f, 0.5f), BiomeType.Grassland, Array.Empty<BiomeType>());
            var best = float.MaxValue;
            var warpX = (Fractal01(x * 4.1f, y * 4.1f, world.Seed.Value + 331, 3, 0.5f, 2f) - 0.5f) * 0.07f;
            var warpY = (Fractal01(x * 4.4f, y * 4.4f, world.Seed.Value + 349, 3, 0.5f, 2f) - 0.5f) * 0.07f;
            foreach (var region in regions)
            {
                var dx = x + warpX - region.Center.X;
                var dy = y + warpY - region.Center.Y;
                var distance = dx * dx + dy * dy;
                if (distance < best)
                {
                    best = distance;
                    nearest = region;
                }
            }

            if (coastScore < 0.06f)
            {
                return nearest.DominantBiome == BiomeType.Desert ? BiomeType.RockyCoast : BiomeType.TropicalCoast;
            }

            if (nearest.SecondaryBiomes.Count > 0)
            {
                var blendNoise = Fractal01(x * 10.3f, y * 10.3f, world.Seed.Value + 411, 2, 0.55f, 2f);
                if (blendNoise > 0.68f)
                {
                    var secondaryIndex = Mathf.Min(nearest.SecondaryBiomes.Count - 1, Mathf.FloorToInt((blendNoise - 0.68f) * nearest.SecondaryBiomes.Count / 0.32f));
                    return nearest.SecondaryBiomes[secondaryIndex];
                }
            }

            return nearest.DominantBiome;
        }

        private readonly struct RegionInfo
        {
            public RegionInfo(MapPoint center, BiomeType dominantBiome, IReadOnlyList<BiomeType> secondaryBiomes)
            {
                Center = center;
                DominantBiome = dominantBiome;
                SecondaryBiomes = secondaryBiomes ?? Array.Empty<BiomeType>();
            }

            public MapPoint Center { get; }
            public BiomeType DominantBiome { get; }
            public IReadOnlyList<BiomeType> SecondaryBiomes { get; }
        }
    }

    internal static class MapRendererCommon
    {
        public static int Index(int x, int y, int width)
        {
            return y * width + x;
        }

        public static void SavePng(Color[] pixels, int width, int height, string name, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        public static void DestroySettings(ProceduralLocationSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(settings);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        public static Color PaperColor(float x, float y, int seed)
        {
            var n = Fractal01(x * 18f, y * 18f, seed + 1033, 3, 0.52f, 2.05f);
            var vignette = Mathf.Clamp01(Mathf.Max(Mathf.Abs(x - 0.5f), Mathf.Abs(y - 0.5f)) * 1.45f);
            var paper = Color.Lerp(new Color(0.75f, 0.67f, 0.47f, 1f), new Color(0.89f, 0.80f, 0.58f, 1f), n * 0.65f);
            return Color.Lerp(paper, new Color(0.48f, 0.37f, 0.22f, 1f), Mathf.SmoothStep(0.62f, 1f, vignette) * 0.22f);
        }

        public static float Fractal01(float x, float y, int seed, int octaves, float persistence, float lacunarity)
        {
            var value = 0f;
            var amplitude = 1f;
            var frequency = 1f;
            var maxValue = 0f;
            var ox = (seed & 0xFFFF) * 0.0137f;
            var oy = ((seed >> 8) & 0xFFFF) * 0.0113f;

            for (var octave = 0; octave < octaves; octave++)
            {
                value += Mathf.PerlinNoise(x * frequency + ox + octave * 17.37f, y * frequency + oy - octave * 13.19f) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue);
        }

        public static float ShapeScore(WorldShapeType shape, float x, float y, float noise, float detail)
        {
            var cx = x - 0.5f;
            var cy = y - 0.5f;
            var radial = Mathf.Sqrt(cx * cx + cy * cy) / 0.7071067f;
            var elliptical = Mathf.Sqrt(cx * cx * 0.82f + cy * cy * 1.18f) / 0.7071067f;
            var coastNoise = (noise - 0.5f) * 0.32f + (detail - 0.5f) * 0.12f;

            return shape switch
            {
                WorldShapeType.HugeIsland => 0.58f - radial + coastNoise,
                WorldShapeType.IslandArchipelago => Mathf.Max(0.20f - Mathf.Abs(y - (0.56f + (noise - 0.5f) * 0.20f)), 0.34f - radial) + (noise - 0.42f) * 0.70f + (detail - 0.5f) * 0.22f,
                WorldShapeType.ContinentPart => 0.52f - x + coastNoise + (noise - 0.5f) * 0.24f,
                WorldShapeType.Peninsula => Mathf.Max(0.44f - x, 0.28f - Mathf.Abs(y - 0.52f - (noise - 0.5f) * 0.18f) - x * 0.18f) + coastNoise * 0.7f,
                WorldShapeType.PeninsulaAndIslands => Mathf.Max(Mathf.Max(0.38f - x, 0.22f - Mathf.Abs(y - 0.48f)), noise > 0.62f && x > 0.48f ? 0.14f + (noise - 0.62f) * 1.1f - radial * 0.22f : -0.4f) + coastNoise * 0.9f,
                WorldShapeType.TwoPeninsulas => Mathf.Max(0.24f - x, Mathf.Max(0.26f - Mathf.Abs(y - 0.30f - (noise - 0.5f) * 0.08f), 0.26f - Mathf.Abs(y - 0.72f + (noise - 0.5f) * 0.08f)) - Mathf.Abs(x - 0.42f) * 0.26f) + coastNoise * 0.62f,
                WorldShapeType.BrokenCoast => 0.46f - x + (noise - 0.48f) * 0.72f + (detail - 0.5f) * 0.22f,
                WorldShapeType.InlandSeaRegion => 0.32f - radial > 0f ? -(0.32f - radial) * 1.3f + coastNoise * 0.2f : 0.78f - elliptical + coastNoise,
                WorldShapeType.MountainRingBasin => 0.72f - radial + coastNoise * 0.65f,
                WorldShapeType.RiverDeltaRegion => 0.58f - x * 0.72f - Mathf.Abs(y - 0.50f) * 0.24f + coastNoise,
                _ => 0.56f - radial + coastNoise
            };
        }

        public static float MacroElevation(WorldShapeType shape, float x, float y, float coastScore, int seed)
        {
            var macro = Fractal01(x * 3.2f, y * 3.2f, seed + 101, 5, 0.55f, 2.0f);
            var ridges = Fractal01(x * 8.4f, y * 8.4f, seed + 173, 4, 0.50f, 2.15f);
            var coastalCut = coastScore < 0.08f ? 0.18f * Mathf.InverseLerp(0.08f, -0.04f, coastScore) : 0f;
            return Mathf.Clamp01(macro * 0.54f + ridges * ridges * 0.32f + ShapeElevation(shape, x, y) - coastalCut);
        }

        public static float ShapeElevation(WorldShapeType shape, float x, float y)
        {
            var cx = x - 0.5f;
            var cy = y - 0.5f;
            var radial = Mathf.Sqrt(cx * cx + cy * cy);
            return shape switch
            {
                WorldShapeType.MountainRingBasin => SmoothRange(0.28f, 0.47f, radial) * (1f - SmoothRange(0.48f, 0.70f, radial)) * 0.42f,
                WorldShapeType.RiverDeltaRegion => (1f - x) * 0.16f - Mathf.Max(0f, x - 0.66f) * 0.20f,
                WorldShapeType.ContinentPart => (1f - x) * 0.18f,
                WorldShapeType.Peninsula => (1f - x) * 0.10f,
                WorldShapeType.HugeIsland => Mathf.Max(0f, 0.40f - radial) * 0.38f,
                _ => 0.08f
            };
        }

        public static float MountainInfluence(IEnumerable<MountainRange> ranges, float x, float y)
        {
            var influence = 0f;
            foreach (var range in ranges)
            {
                foreach (var point in range.Points)
                {
                    var dx = x - point.X;
                    var dy = y - point.Y;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    influence = Mathf.Max(influence, Mathf.SmoothStep(0.12f, 0f, distance) * 0.34f * Mathf.Clamp01(range.Intensity + 0.35f));
                }
            }

            return influence;
        }

        public static Color BiomeMapColor(BiomeType biome)
        {
            return biome switch
            {
                BiomeType.TemperateForest => new Color(0.31f, 0.48f, 0.28f, 1f),
                BiomeType.DarkForest => new Color(0.18f, 0.31f, 0.23f, 1f),
                BiomeType.Grassland => new Color(0.58f, 0.66f, 0.34f, 1f),
                BiomeType.Highlands => new Color(0.47f, 0.53f, 0.37f, 1f),
                BiomeType.Mountains => new Color(0.50f, 0.47f, 0.40f, 1f),
                BiomeType.Swamp => new Color(0.31f, 0.40f, 0.25f, 1f),
                BiomeType.Desert => new Color(0.74f, 0.61f, 0.34f, 1f),
                BiomeType.AshDesert => new Color(0.43f, 0.39f, 0.34f, 1f),
                BiomeType.Tundra => new Color(0.63f, 0.68f, 0.60f, 1f),
                BiomeType.SnowPeaks => new Color(0.86f, 0.86f, 0.78f, 1f),
                BiomeType.TropicalCoast => new Color(0.37f, 0.60f, 0.36f, 1f),
                BiomeType.RockyCoast => new Color(0.51f, 0.49f, 0.38f, 1f),
                BiomeType.Riverlands => new Color(0.42f, 0.58f, 0.34f, 1f),
                BiomeType.LakeDistrict => new Color(0.40f, 0.57f, 0.47f, 1f),
                BiomeType.Wasteland => new Color(0.49f, 0.43f, 0.28f, 1f),
                _ => new Color(0.55f, 0.60f, 0.38f, 1f)
            };
        }

        public static Color ScaleColor(Color color, float scale)
        {
            return new Color(Mathf.Clamp01(color.r * scale), Mathf.Clamp01(color.g * scale), Mathf.Clamp01(color.b * scale), color.a);
        }

        public static float HillShade(float[] heights, bool[] land, int x, int y, int width, int height, float strength)
        {
            var left = heights[Index(Mathf.Max(0, x - 1), y, width)];
            var right = heights[Index(Mathf.Min(width - 1, x + 1), y, width)];
            var down = heights[Index(x, Mathf.Max(0, y - 1), width)];
            var up = heights[Index(x, Mathf.Min(height - 1, y + 1), width)];
            var dx = left - right;
            var dy = down - up;
            var slope = Mathf.Abs(dx) + Mathf.Abs(dy);
            return Mathf.Clamp(0.92f + (dx * -0.85f + dy * 1.15f) * strength - slope * 0.18f, 0.62f, 1.24f);
        }

        public static float LocalHillShade(float[,] heightMap, float x, float y)
        {
            var step = 1f / Mathf.Max(32f, heightMap.GetLength(0) - 1);
            var left = SampleHeight(heightMap, x - step, y);
            var right = SampleHeight(heightMap, x + step, y);
            var down = SampleHeight(heightMap, x, y - step);
            var up = SampleHeight(heightMap, x, y + step);
            var dx = left - right;
            var dy = down - up;
            var slope = Mathf.Abs(dx) + Mathf.Abs(dy);
            return Mathf.Clamp(0.92f + dx * -2.8f + dy * 3.4f - slope * 0.55f, 0.54f, 1.34f);
        }

        public static float LocalSlope(float[,] heightMap, float x, float y)
        {
            var step = 1f / Mathf.Max(32f, heightMap.GetLength(0) - 1);
            var left = SampleHeight(heightMap, x - step, y);
            var right = SampleHeight(heightMap, x + step, y);
            var down = SampleHeight(heightMap, x, y - step);
            var up = SampleHeight(heightMap, x, y + step);
            return Mathf.Clamp01((Mathf.Abs(left - right) + Mathf.Abs(down - up)) * 5.5f);
        }

        public static float SampleHeight(float[,] heightMap, float x, float y)
        {
            var width = heightMap.GetLength(1);
            var height = heightMap.GetLength(0);
            var px = Mathf.Clamp01(x) * (width - 1);
            var py = Mathf.Clamp01(y) * (height - 1);
            var x0 = Mathf.FloorToInt(px);
            var y0 = Mathf.FloorToInt(py);
            var x1 = Mathf.Min(width - 1, x0 + 1);
            var y1 = Mathf.Min(height - 1, y0 + 1);
            var tx = px - x0;
            var ty = py - y0;
            var a = Mathf.Lerp(heightMap[y0, x0], heightMap[y0, x1], tx);
            var b = Mathf.Lerp(heightMap[y1, x0], heightMap[y1, x1], tx);
            return Mathf.Lerp(a, b, ty);
        }

        public static void PaintCoastRim(Color[] pixels, WorldMapRaster raster, int width, int height)
        {
            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var index = Index(x, y, width);
                    if (!raster.Land[index] || !TouchesWater(raster.Land, x, y, width, height))
                    {
                        continue;
                    }

                    BlendCircle(pixels, width, height, x, y, 2, new Color(0.85f, 0.74f, 0.49f, 1f), 0.62f);
                    BlendCircle(pixels, width, height, x, y, 1, new Color(0.29f, 0.20f, 0.12f, 1f), 0.18f);
                }
            }
        }

        public static void PaintBiomeTexture(Color[] pixels, WorldMapRaster raster, int width, int height, int seed)
        {
            for (var y = 12; y < height - 12; y += 12)
            {
                for (var x = 12; x < width - 12; x += 12)
                {
                    var index = Index(x, y, width);
                    if (!raster.Land[index])
                    {
                        continue;
                    }

                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var n = Fractal01(nx * 18f, ny * 18f, seed + 9901, 2, 0.5f, 2f);
                    var biome = raster.Biomes[index];
                    if ((biome == BiomeType.TemperateForest || biome == BiomeType.DarkForest || biome == BiomeType.TropicalCoast) && n > 0.43f)
                    {
                        DrawTreeIcon(pixels, width, height, x, y, 4, biome == BiomeType.DarkForest ? new Color(0.09f, 0.18f, 0.12f, 1f) : new Color(0.17f, 0.31f, 0.14f, 1f));
                    }
                    else if ((biome == BiomeType.Desert || biome == BiomeType.AshDesert || biome == BiomeType.Wasteland) && n > 0.50f)
                    {
                        DrawDune(pixels, width, height, x, y, 10, new Color(0.45f, 0.30f, 0.14f, 1f));
                    }
                    else if ((biome == BiomeType.Swamp || biome == BiomeType.Riverlands) && n > 0.56f)
                    {
                        BlendCircle(pixels, width, height, x, y, 3, new Color(0.12f, 0.23f, 0.17f, 1f), 0.35f);
                    }
                }
            }
        }

        public static void PaintLakes(Color[] pixels, int width, int height, IEnumerable<Lake> lakes, int seed)
        {
            foreach (var lake in lakes)
            {
                var points = lake.Points.ToArray();
                if (points.Length == 0)
                {
                    continue;
                }

                var cx = Mathf.RoundToInt(points.Average(point => point.X) * (width - 1));
                var cy = Mathf.RoundToInt(points.Average(point => point.Y) * (height - 1));
                var radius = Mathf.Max(8, Mathf.RoundToInt(width * lake.Radius));
                DrawIrregularDisc(pixels, width, height, cx, cy, radius + 3, new Color(0.13f, 0.24f, 0.20f, 1f), 0.30f, seed + radius);
                DrawIrregularDisc(pixels, width, height, cx, cy, radius, new Color(0.10f, 0.36f, 0.50f, 1f), 0.92f, seed + radius * 3);
                DrawIrregularDisc(pixels, width, height, cx, cy, Mathf.Max(2, radius / 3), new Color(0.37f, 0.62f, 0.66f, 1f), 0.35f, seed + radius * 7);
            }
        }

        public static void PaintRivers(Color[] pixels, int width, int height, IEnumerable<River> rivers)
        {
            foreach (var river in rivers)
            {
                var thickness = Mathf.Max(3, Mathf.RoundToInt(width * river.Width));
                DrawPolyline(pixels, width, height, river.Points, new Color(0.10f, 0.19f, 0.15f, 1f), thickness + 3, 0.42f);
                DrawPolyline(pixels, width, height, river.Points, new Color(0.08f, 0.31f, 0.52f, 1f), thickness, 0.92f);
                DrawPolyline(pixels, width, height, river.Points, new Color(0.47f, 0.68f, 0.70f, 1f), Mathf.Max(1, thickness / 2), 0.28f);
            }
        }

        public static void PaintMountains(Color[] pixels, int width, int height, IEnumerable<MountainRange> ranges)
        {
            foreach (var range in ranges)
            {
                var previous = default(MapPoint);
                var hasPrevious = false;
                foreach (var point in range.Points)
                {
                    if (hasPrevious)
                    {
                        DrawLine(pixels, width, height, previous, point, new Color(0.22f, 0.18f, 0.13f, 1f), 2, 0.22f);
                    }

                    DrawMountainIcon(pixels, width, height, Mathf.RoundToInt(point.X * (width - 1)), Mathf.RoundToInt(point.Y * (height - 1)), Mathf.RoundToInt(Mathf.Lerp(13f, 22f, Mathf.Clamp01(range.Intensity))), range.Intensity);
                    previous = point;
                    hasPrevious = true;
                }
            }
        }

        public static void PaintConnections(Color[] pixels, int width, int height, IReadOnlyCollection<WorldLocation> locations)
        {
            var byId = locations.ToDictionary(location => location.Id.Value);
            var drawn = new HashSet<string>();
            foreach (var location in locations)
            {
                foreach (var connection in location.Connections)
                {
                    var keyA = location.Id.Value;
                    var keyB = connection.To.Value;
                    var key = string.CompareOrdinal(keyA, keyB) < 0 ? $"{keyA}|{keyB}" : $"{keyB}|{keyA}";
                    if (!drawn.Add(key) || !byId.TryGetValue(connection.To.Value, out var target))
                    {
                        continue;
                    }

                    var boat = connection.Type == LocationConnectionType.BoatRoute;
                    var color = boat ? new Color(0.06f, 0.22f, 0.35f, 1f) : new Color(0.32f, 0.21f, 0.11f, 1f);
                    DrawDashedLine(pixels, width, height, location.WorldMapPosition, target.WorldMapPosition, color, boat ? 2 : 3, boat ? 0.48f : 0.62f, boat ? 18 : 12);
                }
            }
        }

        public static void PaintLocationMarkers(Color[] pixels, int width, int height, IReadOnlyCollection<WorldLocation> locations)
        {
            foreach (var location in locations)
            {
                var x = Mathf.RoundToInt(location.WorldMapPosition.X * (width - 1));
                var y = Mathf.RoundToInt(location.WorldMapPosition.Y * (height - 1));
                var radius = location.IsStartLocation ? 11 : 8;
                BlendCircle(pixels, width, height, x + 2, y - 2, radius + 2, new Color(0.08f, 0.05f, 0.02f, 1f), 0.32f);
                BlendCircle(pixels, width, height, x, y, radius + 2, new Color(0.15f, 0.09f, 0.04f, 1f), 0.95f);
                BlendCircle(pixels, width, height, x, y, radius - 2, location.IsStartLocation ? new Color(0.96f, 0.75f, 0.25f, 1f) : new Color(0.73f, 0.47f, 0.18f, 1f), 0.98f);
                BlendCircle(pixels, width, height, x, y, Mathf.Max(2, radius / 3), new Color(1f, 0.88f, 0.42f, 1f), 0.50f);
            }
        }

        public static void PaintLocalWaterEdges(Color[] pixels, float[,] heightMap, float waterLevel, bool waterEnabled, int width, int height)
        {
            if (!waterEnabled)
            {
                return;
            }

            for (var y = 2; y < height - 2; y++)
            {
                for (var x = 2; x < width - 2; x++)
                {
                    var nx = x / (float)(width - 1);
                    var ny = y / (float)(height - 1);
                    var hereWater = SampleHeight(heightMap, nx, ny) <= waterLevel;
                    var nearWater = false;
                    var nearLand = false;
                    for (var oy = -1; oy <= 1; oy++)
                    {
                        for (var ox = -1; ox <= 1; ox++)
                        {
                            var neighbor = SampleHeight(heightMap, (x + ox) / (float)(width - 1), (y + oy) / (float)(height - 1)) <= waterLevel;
                            nearWater |= neighbor;
                            nearLand |= !neighbor;
                        }
                    }

                    if (!hereWater && nearWater)
                    {
                        BlendCircle(pixels, width, height, x, y, 2, new Color(0.84f, 0.74f, 0.48f, 1f), 0.42f);
                    }
                    else if (hereWater && nearLand)
                    {
                        BlendCircle(pixels, width, height, x, y, 2, new Color(0.38f, 0.61f, 0.62f, 1f), 0.30f);
                    }
                }
            }
        }

        public static void PaintLocalFeatures(Color[] pixels, int width, int height, WorldLocation location, float waterLevel)
        {
            if (location.Type == LocationType.Coast)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = x / (float)(width - 1);
                    var shore = 0.16f + (Fractal01(nx * 4f, location.TerrainSeed * 0.001f, location.TerrainSeed + 41, 3, 0.5f, 2f) - 0.5f) * 0.06f;
                    FillRect(pixels, width, height, x / (float)width, 0f, (x + 1) / (float)width, shore, new Color(0.09f, 0.30f, 0.48f, 1f), 0.86f);
                }
            }

            if (location.Type == LocationType.Island)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var nx = x / (float)(width - 1) * 2f - 1f;
                        var ny = y / (float)(height - 1) * 2f - 1f;
                        var edge = Mathf.Sqrt(nx * nx + ny * ny);
                        if (edge > 0.84f)
                        {
                            BlendPixel(pixels, width, height, x, y, new Color(0.10f, 0.32f, 0.50f, 1f), Mathf.Clamp01((edge - 0.84f) * 5f));
                        }
                    }
                }
            }

            if (location.Type == LocationType.RiverCrossing || location.DominantBiome == BiomeType.Riverlands)
            {
                DrawLine(pixels, width, height, new MapPoint(0.10f, 0.78f), new MapPoint(0.90f, 0.24f), new Color(0.07f, 0.22f, 0.18f, 1f), 10, 0.35f);
                DrawLine(pixels, width, height, new MapPoint(0.10f, 0.78f), new MapPoint(0.90f, 0.24f), new Color(0.08f, 0.32f, 0.52f, 1f), 7, 0.90f);
            }

            if (location.Type == LocationType.MountainPass || location.DominantBiome == BiomeType.Mountains || location.DominantBiome == BiomeType.SnowPeaks)
            {
                for (var i = 0; i < 22; i++)
                {
                    var x = Mathf.RoundToInt((0.08f + i * 0.04f) * (width - 1));
                    var y = Mathf.RoundToInt((0.70f + Mathf.Sin(i * 1.4f) * 0.08f) * (height - 1));
                    DrawMountainIcon(pixels, width, height, x, y, 18, 0.75f);
                }
            }
        }

        public static void PaintGateways(Color[] pixels, int width, int height, IEnumerable<LocationGateway> gateways)
        {
            foreach (var gateway in gateways)
            {
                var rect = gateway.GatewayAreaNormalized;
                FillRect(pixels, width, height, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height, new Color(0.96f, 0.76f, 0.23f, 1f), 0.92f);
                DrawRect(pixels, width, height, rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height, new Color(0.23f, 0.13f, 0.05f, 1f), 3, 0.70f);
            }
        }

        public static void PaintMapBorder(Color[] pixels, int width, int height)
        {
            DrawRect(pixels, width, height, 0.005f, 0.005f, 0.995f, 0.995f, new Color(0.18f, 0.11f, 0.05f, 1f), 4, 0.55f);
            DrawRect(pixels, width, height, 0.018f, 0.018f, 0.982f, 0.982f, new Color(0.76f, 0.60f, 0.32f, 1f), 2, 0.30f);
        }

        public static void DrawPolyline(Color[] pixels, int width, int height, IReadOnlyCollection<MapPoint> points, Color color, int thickness, float alpha)
        {
            var array = points.ToArray();
            for (var i = 1; i < array.Length; i++)
            {
                DrawLine(pixels, width, height, array[i - 1], array[i], color, thickness, alpha);
            }
        }

        public static void DrawLine(Color[] pixels, int width, int height, MapPoint from, MapPoint to, Color color, int thickness, float alpha)
        {
            var x0 = Mathf.RoundToInt(from.X * (width - 1));
            var y0 = Mathf.RoundToInt(from.Y * (height - 1));
            var x1 = Mathf.RoundToInt(to.X * (width - 1));
            var y1 = Mathf.RoundToInt(to.Y * (height - 1));
            DrawLinePixels(pixels, width, height, x0, y0, x1, y1, color, thickness, alpha);
        }

        public static void DrawDashedLine(Color[] pixels, int width, int height, MapPoint from, MapPoint to, Color color, int thickness, float alpha, int dashLength)
        {
            var x0 = Mathf.RoundToInt(from.X * (width - 1));
            var y0 = Mathf.RoundToInt(from.Y * (height - 1));
            var x1 = Mathf.RoundToInt(to.X * (width - 1));
            var y1 = Mathf.RoundToInt(to.Y * (height - 1));
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (var i = 0; i <= steps; i++)
            {
                if ((i / Mathf.Max(1, dashLength)) % 2 != 0)
                {
                    continue;
                }

                var t = steps == 0 ? 0f : i / (float)steps;
                var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                BlendCircle(pixels, width, height, x, y, thickness, color, alpha);
            }
        }

        public static void DrawLinePixels(Color[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color color, int thickness, float alpha)
        {
            var dx = Mathf.Abs(x1 - x0);
            var dy = Mathf.Abs(y1 - y0);
            var sx = x0 < x1 ? 1 : -1;
            var sy = y0 < y1 ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                BlendCircle(pixels, width, height, x0, y0, thickness, color, alpha);
                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                var e2 = err * 2;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        public static void FillRect(Color[] pixels, int width, int height, float minX, float minY, float maxX, float maxY, Color color, float alpha)
        {
            var x0 = Mathf.Clamp(Mathf.RoundToInt(minX * width), 0, width - 1);
            var y0 = Mathf.Clamp(Mathf.RoundToInt(minY * height), 0, height - 1);
            var x1 = Mathf.Clamp(Mathf.RoundToInt(maxX * width), 0, width - 1);
            var y1 = Mathf.Clamp(Mathf.RoundToInt(maxY * height), 0, height - 1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    BlendPixel(pixels, width, height, x, y, color, alpha);
                }
            }
        }

        public static void DrawRect(Color[] pixels, int width, int height, float minX, float minY, float maxX, float maxY, Color color, int thickness, float alpha)
        {
            DrawLine(pixels, width, height, new MapPoint(minX, minY), new MapPoint(maxX, minY), color, thickness, alpha);
            DrawLine(pixels, width, height, new MapPoint(maxX, minY), new MapPoint(maxX, maxY), color, thickness, alpha);
            DrawLine(pixels, width, height, new MapPoint(maxX, maxY), new MapPoint(minX, maxY), color, thickness, alpha);
            DrawLine(pixels, width, height, new MapPoint(minX, maxY), new MapPoint(minX, minY), color, thickness, alpha);
        }

        public static void DrawIrregularDisc(Color[] pixels, int width, int height, int centerX, int centerY, int radius, Color color, float alpha, int seed)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    var distance = Mathf.Sqrt(x * x + y * y) / Mathf.Max(1f, radius);
                    var angleNoise = Fractal01((centerX + x) * 0.035f, (centerY + y) * 0.035f, seed, 2, 0.55f, 2.0f);
                    var edge = 0.86f + (angleNoise - 0.5f) * 0.22f;
                    if (distance <= edge)
                    {
                        BlendPixel(pixels, width, height, centerX + x, centerY + y, color, alpha * Mathf.SmoothStep(1f, 0.72f, distance));
                    }
                }
            }
        }

        public static void BlendCircle(Color[] pixels, int width, int height, int centerX, int centerY, int radius, Color color, float alpha)
        {
            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        BlendPixel(pixels, width, height, centerX + x, centerY + y, color, alpha);
                    }
                }
            }
        }

        public static void BlendPixel(Color[] pixels, int width, int height, int x, int y, Color color, float alpha)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return;
            }

            var index = Index(x, y, width);
            pixels[index] = Color.Lerp(pixels[index], color, Mathf.Clamp01(alpha));
        }

        public static bool TouchesWater(bool[] land, int x, int y, int width, int height)
        {
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0)
                    {
                        continue;
                    }

                    var px = x + ox;
                    var py = y + oy;
                    if (px < 0 || py < 0 || px >= width || py >= height || !land[Index(px, py, width)])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static void DrawMountainIcon(Color[] pixels, int width, int height, int cx, int cy, int size, float intensity)
        {
            var dark = new Color(0.20f, 0.17f, 0.13f, 1f);
            var mid = new Color(0.47f, 0.42f, 0.34f, 1f);
            var snow = new Color(0.86f, 0.84f, 0.73f, 1f);
            for (var y = 0; y < size; y++)
            {
                var half = Mathf.RoundToInt((1f - y / (float)size) * size * 0.46f);
                for (var x = -half; x <= half; x++)
                {
                    var t = y / (float)size;
                    var shade = x < 0 ? dark : mid;
                    var color = Color.Lerp(shade, snow, Mathf.SmoothStep(0.72f, 1f, t) * Mathf.Clamp01(intensity));
                    BlendPixel(pixels, width, height, cx + x, cy - y + size / 2, color, 0.72f);
                }
            }
        }

        public static void DrawTreeIcon(Color[] pixels, int width, int height, int cx, int cy, int size, Color color)
        {
            BlendCircle(pixels, width, height, cx, cy + size / 2, size, color, 0.62f);
            DrawLinePixels(pixels, width, height, cx, cy - size, cx, cy + size / 2, new Color(0.16f, 0.09f, 0.04f, 1f), 1, 0.35f);
        }

        public static void DrawDune(Color[] pixels, int width, int height, int cx, int cy, int size, Color color)
        {
            for (var x = -size; x <= size; x++)
            {
                var y = Mathf.RoundToInt(Mathf.Sin((x + size) / (float)(size * 2) * Mathf.PI) * size * 0.34f);
                BlendCircle(pixels, width, height, cx + x, cy + y, 1, color, 0.22f);
            }
        }

        public static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }
    }
}
