using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
using UnityEngine;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Generator.Location
{
    public interface ILocationTerrainGenerator
    {
        GeneratedLocationTerrain Generate(GeneratedWorld world, WorldLocation location, LocationGenerationConfig config);
    }

    public sealed class LocationGenerationConfig
    {
        public int HeightmapResolution { get; set; } = 1025;
        public float WorldSize { get; set; } = ProceduralLocationSettings.DefaultLocationSizeMeters;
        public float TreeDensity { get; set; } = 0.82f;
        public float GrassSaturation { get; set; } = 0.96f;
        public bool GenerateSceneData { get; set; } = true;

        public static LocationGenerationConfig CreateDefault()
        {
            return new LocationGenerationConfig();
        }
    }

    [Serializable]
    public sealed class LocationTerrainSaveDto
    {
        public string locationId;
        public string locationName;
        public string biome;
        public string locationType;
        public int terrainSeed;
        public int heightmapResolution;
        public float worldSize;
        public float terrainHeight;
        public float waterLevel;
        public string landShape;
        public GatewaySaveDto[] gateways;
    }

    [Serializable]
    public sealed class GatewaySaveDto
    {
        public string toLocationId;
        public string exitDirection;
        public string entryDirection;
        public float x;
        public float y;
        public float width;
        public float height;
    }

    public sealed class GeneratedLocationTerrain
    {
        public GeneratedLocationTerrain(LocationTerrainSaveDto dto)
        {
            Dto = dto ?? throw new ArgumentNullException(nameof(dto));
        }

        public LocationTerrainSaveDto Dto { get; }

        public string Save(string locationDirectory)
        {
            Directory.CreateDirectory(locationDirectory);
            var terrainPath = Path.Combine(locationDirectory, "terrain.json");
            var sceneDataPath = Path.Combine(locationDirectory, "scene_data.json");
            var json = JsonUtility.ToJson(Dto, true);
            File.WriteAllText(terrainPath, json);
            File.WriteAllText(sceneDataPath, json);
            return terrainPath;
        }
    }

    public sealed class LegacyLocationTerrainGenerator : ILocationTerrainGenerator
    {
        public GeneratedLocationTerrain Generate(GeneratedWorld world, WorldLocation location, LocationGenerationConfig config)
        {
            var safeConfig = config ?? LocationGenerationConfig.CreateDefault();
            var worldSize = ProceduralLocationSettings.DefaultLocationSizeMeters;
            var profile = LocationTerrainProfile.From(location, safeConfig);
            var dto = new LocationTerrainSaveDto
            {
                locationId = location.Id.Value,
                locationName = location.Name.Value,
                biome = location.DominantBiome.ToString(),
                locationType = location.Type.ToString(),
                terrainSeed = location.TerrainSeed,
                heightmapResolution = Mathf.Max(1025, safeConfig.HeightmapResolution),
                worldSize = worldSize,
                terrainHeight = profile.TerrainHeight,
                waterLevel = profile.WaterLevel,
                landShape = profile.LandShape.ToString(),
                gateways = location.Gateways.Select(gateway => new GatewaySaveDto
                {
                    toLocationId = gateway.ToLocationId.Value,
                    exitDirection = gateway.ExitDirection.ToString(),
                    entryDirection = gateway.EntryDirection.ToString(),
                    x = gateway.GatewayAreaNormalized.X,
                    y = gateway.GatewayAreaNormalized.Y,
                    width = gateway.GatewayAreaNormalized.Width,
                    height = gateway.GatewayAreaNormalized.Height
                }).ToArray()
            };

            return new GeneratedLocationTerrain(dto);
        }
    }

    public static class LocationTerrainSettingsFactory
    {
        public static ProceduralLocationSettings Create(WorldLocation location, LocationGenerationConfig config = null)
        {
            var safeConfig = config ?? LocationGenerationConfig.CreateDefault();
            var worldSize = ProceduralLocationSettings.DefaultLocationSizeMeters;
            var settings = ScriptableObject.CreateInstance<ProceduralLocationSettings>();
            settings.name = $"Runtime Location Settings - {location.Name.Value}";
            settings.hideFlags = HideFlags.DontSave;
            settings.ConfigureAssetCatalog(ProceduralAssetCatalogResolver.LoadDefaultCatalog());

            var profile = LocationTerrainProfile.From(location, safeConfig);
            settings.ConfigureGlobal(worldSize, worldSize);
            settings.ConfigureSeed(SeedMode.Fixed, location.TerrainSeed);
            settings.ConfigureTerrain(
                Mathf.Max(1025, safeConfig.HeightmapResolution),
                profile.TerrainHeight,
                profile.NoiseScale,
                profile.Octaves,
                profile.Persistence,
                profile.Lacunarity,
                profile.HeightMultiplier,
                profile.LandShape,
                profile.RidgeIntensity,
                profile.ValleyIntensity,
                profile.CliffIntensity,
                profile.TerraceStrength,
                profile.MicroReliefStrength,
                profile.UseEdgeFalloff,
                profile.EdgeFalloffStart,
                profile.EdgeFalloffStrength);
            settings.ConfigureTerrainChunks(
                true,
                500f,
                129,
                1,
                1,
                0,
                1,
                1,
                2,
                new Vector3(6f, 18f, 42f));
            settings.ConfigureWater(profile.WaterEnabled, profile.WaterLevel, 24f, profile.WaterColor);
            var propDensity = ResolvePropDensity(location);
            var treeDensity = ResolveTreeDensity(location, safeConfig);
            settings.ConfigureForestRendering(profile.ForestQuality);
            settings.ConfigureForestLodSettings(BuildForestLod(settings.ForestLodSettings, location, treeDensity));
            settings.ConfigureGpuGrass(BuildGrass(settings.GpuGrassSettings, safeConfig, location));
            settings.ConfigureProps(
                true,
                TuneGeneratedWorldPropCategories(
                    MapGenerationPresetMapper.BuildPropCategories(settings, propDensity, treeDensity, settings.AssetCatalog),
                    treeDensity));
            settings.ConfigureTerrainDetails(true, profile.DetailDensity, 384, 64);
            return settings;
        }

        private static PropDensityOption ResolvePropDensity(WorldLocation location)
        {
            return location.Type switch
            {
                LocationType.Forest or LocationType.Swamp or LocationType.RiverCrossing => PropDensityOption.High,
                LocationType.Desert or LocationType.MountainPass => PropDensityOption.Low,
                LocationType.Coast or LocationType.Island => PropDensityOption.Normal,
                _ => location.DominantBiome switch
                {
                    BiomeType.TemperateForest or BiomeType.DarkForest or BiomeType.Swamp or BiomeType.Riverlands => PropDensityOption.High,
                    BiomeType.Desert or BiomeType.AshDesert or BiomeType.Wasteland or BiomeType.SnowPeaks or BiomeType.Mountains => PropDensityOption.Low,
                    _ => PropDensityOption.Normal
                }
            };
        }

        private static float ResolveTreeDensity(WorldLocation location, LocationGenerationConfig config)
        {
            var requested = Mathf.Clamp01(config.TreeDensity);
            var biomeDensity = location.DominantBiome switch
            {
                BiomeType.TemperateForest => 0.70f,
                BiomeType.DarkForest => 0.78f,
                BiomeType.TropicalCoast => 0.56f,
                BiomeType.Grassland => 0.38f,
                BiomeType.Highlands => 0.34f,
                BiomeType.Mountains => 0.10f,
                BiomeType.SnowPeaks => 0.04f,
                BiomeType.Swamp => 0.66f,
                BiomeType.Riverlands => 0.58f,
                BiomeType.LakeDistrict => 0.46f,
                BiomeType.Desert => 0.03f,
                BiomeType.AshDesert => 0.02f,
                BiomeType.Wasteland => 0.08f,
                BiomeType.RockyCoast => 0.12f,
                _ => 0.30f
            };

            var locationBoost = location.Type switch
            {
                LocationType.Forest => 1.12f,
                LocationType.Swamp => 1.03f,
                LocationType.RiverCrossing => 0.94f,
                LocationType.Coast => 0.70f,
                LocationType.Island => 0.62f,
                LocationType.MountainPass => 0.45f,
                LocationType.Desert => 0.18f,
                _ => 0.86f
            };

            return Mathf.Clamp(requested * 0.10f + biomeDensity * locationBoost, 0.02f, 0.82f);
        }

        private static ForestLodSettings BuildForestLod(ForestLodSettings source, WorldLocation location, float treeDensity)
        {
            var settings = source != null ? source.Clone() : ForestLodSettings.CreatePreset(ForestQualityLevel.High);
            var forest01 = Mathf.InverseLerp(0.10f, 0.82f, Mathf.Clamp01(treeDensity));
            var sparseBiome = location.DominantBiome is BiomeType.Desert or BiomeType.AshDesert or BiomeType.Wasteland or BiomeType.SnowPeaks;

            var lod0 = Mathf.Lerp(52f, 76f, forest01);
            var lod1 = Mathf.Lerp(130f, 185f, forest01);
            var lod2 = Mathf.Lerp(255f, 360f, forest01);
            var cull = Mathf.Lerp(330f, 440f, forest01);
            if (sparseBiome || location.Type == LocationType.Desert)
            {
                lod1 = Mathf.Min(lod1, 150f);
                lod2 = Mathf.Min(lod2, 260f);
                cull = Mathf.Min(cull, 330f);
            }
            else if (location.Type == LocationType.MountainPass)
            {
                lod1 = Mathf.Min(lod1, 190f);
                lod2 = Mathf.Min(lod2, 340f);
                cull = Mathf.Min(cull, 410f);
            }

            settings.Configure(
                lod0,
                lod1,
                lod2,
                cull,
                Mathf.Lerp(90f, 135f, forest01),
                Mathf.RoundToInt(Mathf.Lerp(1200f, 2400f, forest01)),
                Mathf.RoundToInt(Mathf.Lerp(420f, 900f, forest01)),
                true,
                false,
                false,
                Mathf.Lerp(0.82f, 1.12f, forest01));
            return settings;
        }

        private static IReadOnlyList<PropCategoryPlacementSettings> TuneGeneratedWorldPropCategories(
            IReadOnlyList<PropCategoryPlacementSettings> categories,
            float treeDensity)
        {
            var tuned = new List<PropCategoryPlacementSettings>();
            if (categories == null)
            {
                return tuned;
            }

            var forest01 = Mathf.InverseLerp(0.10f, 0.62f, Mathf.Clamp01(treeDensity));
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                if (category == null)
                {
                    continue;
                }

                var density = category.DensityPer10kSqm;
                var minDistance = category.MinDistanceBetweenInstances;
                var drawDistance = category.MaxDrawDistance;
                var attempts = category.AttemptsMultiplier;
                var slopeRange = category.AllowedSlopeRange;
                var clusterThreshold = category.ClusterThreshold;
                var clusterStrength = category.ClusterStrength;
                var clusterNoiseScale = category.ClusterNoiseScale;

                switch (category.Role)
                {
                    case ProceduralPropRole.Tree:
                    case ProceduralPropRole.ForestCoreTrees:
                        density *= Mathf.Lerp(1.14f, 1.62f, forest01);
                        minDistance *= Mathf.Lerp(0.96f, 0.72f, forest01);
                        drawDistance = LimitDrawDistance(drawDistance, Mathf.Lerp(330f, 440f, forest01));
                        attempts = Mathf.Min(Mathf.Max(attempts, Mathf.Lerp(5.5f, 7.5f, forest01)), 8.5f);
                        slopeRange.y = Mathf.Max(slopeRange.y, Mathf.Lerp(36f, 46f, forest01));
                        clusterThreshold = Mathf.Min(clusterThreshold, Mathf.Lerp(0.24f, 0.10f, forest01));
                        clusterStrength = Mathf.Min(Mathf.Max(clusterStrength, Mathf.Lerp(2.2f, 3.35f, forest01)), 3.5f);
                        clusterNoiseScale = Mathf.Min(clusterNoiseScale, Mathf.Lerp(220f, 140f, forest01));
                        break;
                    case ProceduralPropRole.ForestAccentTrees:
                        density *= Mathf.Lerp(1.0f, 1.25f, forest01);
                        minDistance *= Mathf.Lerp(0.98f, 0.88f, forest01);
                        drawDistance = LimitDrawDistance(drawDistance, Mathf.Lerp(300f, 400f, forest01));
                        attempts = Mathf.Min(Mathf.Max(attempts, Mathf.Lerp(5.0f, 7.0f, forest01)), 8f);
                        clusterThreshold = Mathf.Min(clusterThreshold, Mathf.Lerp(0.42f, 0.30f, forest01));
                        clusterStrength = Mathf.Min(Mathf.Max(clusterStrength, Mathf.Lerp(1.9f, 2.9f, forest01)), 3.1f);
                        break;
                    case ProceduralPropRole.Rock:
                    case ProceduralPropRole.RocksSmallMedium:
                        drawDistance = LimitDrawDistance(drawDistance, 72f);
                        break;
                    case ProceduralPropRole.RocksLarge:
                        drawDistance = LimitDrawDistance(drawDistance, 112f);
                        break;
                    case ProceduralPropRole.Cliff:
                        drawDistance = LimitDrawDistance(drawDistance, 155f);
                        break;
                    case ProceduralPropRole.Bushes:
                        drawDistance = LimitDrawDistance(drawDistance, 95f);
                        break;
                    case ProceduralPropRole.GroundPlants:
                    case ProceduralPropRole.GroundCover:
                        drawDistance = LimitDrawDistance(drawDistance, 68f);
                        break;
                    case ProceduralPropRole.ShorePlants:
                        drawDistance = LimitDrawDistance(drawDistance, 95f);
                        break;
                    case ProceduralPropRole.Log:
                        drawDistance = LimitDrawDistance(drawDistance, 58f);
                        break;
                }

                tuned.Add(ClonePropCategory(
                    category,
                    density,
                    minDistance,
                    slopeRange,
                    drawDistance,
                    attempts,
                    clusterThreshold,
                    clusterNoiseScale,
                    clusterStrength));
            }

            return tuned;
        }

        private static float LimitDrawDistance(float current, float maximum)
        {
            return current <= 0f ? maximum : Mathf.Min(current, maximum);
        }

        private static PropCategoryPlacementSettings ClonePropCategory(
            PropCategoryPlacementSettings source,
            float density,
            float minDistance,
            Vector2 slopeRange,
            float drawDistance,
            float attempts,
            float clusterThreshold,
            float clusterNoiseScale,
            float clusterStrength)
        {
            var clone = new PropCategoryPlacementSettings();
            clone.Configure(
                source.CategoryName,
                source.Enabled || source.Role == ProceduralPropRole.ForestAccentTrees,
                SelectGeneratedWorldPrefabs(source.Role, source.Prefabs),
                density,
                minDistance,
                slopeRange,
                source.AllowedHeightRange,
                source.RandomScaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                drawDistance,
                attempts);
            clone.ConfigureRoleAndBiome(
                source.Role,
                source.UseClusterPlacement,
                clusterThreshold,
                clusterNoiseScale,
                clusterStrength,
                source.SlopeAffinity,
                source.ShoreAffinity,
                source.ForestEdgeAffinity);
            return clone;
        }

        private static GameObject[] SelectGeneratedWorldPrefabs(ProceduralPropRole role, GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length <= 1)
            {
                return prefabs;
            }

            return prefabs;
        }

        private static GpuGrassSettings BuildGrass(GpuGrassSettings source, LocationGenerationConfig config, WorldLocation location)
        {
            var settings = source != null ? source.Clone() : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            var saturation = Mathf.Clamp01(config.GrassSaturation);
            var biomeGrass = location.DominantBiome switch
            {
                BiomeType.Desert or BiomeType.AshDesert or BiomeType.Wasteland => 0.32f,
                BiomeType.Mountains or BiomeType.SnowPeaks => 0.40f,
                BiomeType.Swamp or BiomeType.Riverlands or BiomeType.TemperateForest or BiomeType.DarkForest => 1.0f,
                _ => 0.78f
            };
            var density = saturation * biomeGrass;
            var thresholdLow = Mathf.Lerp(0.18f, 0.10f, density);
            var thresholdHigh = Mathf.Lerp(0.50f, 0.38f, density);
            var noiseContrast = Mathf.Lerp(1.32f, 1.45f, density);
            settings.ConfigureBudget(
                settings.Enabled,
                Mathf.Clamp(settings.NearDistance, 18f, 30f),
                Mathf.Clamp(settings.MidDistance, 52f, 80f),
                Mathf.Clamp(settings.FarVisualDistance, 150f, 230f),
                Mathf.Clamp(settings.ClusterSize, 8f, 12f),
                Mathf.Lerp(0.42f, 0.23f, density),
                Mathf.RoundToInt(Mathf.Lerp(720000f, 1600000f, density)),
                Mathf.RoundToInt(Mathf.Lerp(30000f, 68000f, density)),
                Mathf.RoundToInt(Mathf.Lerp(36000f, 90000f, density)),
                Mathf.Lerp(1.05f, 1.65f, density),
                Mathf.Lerp(0.48f, 0.72f, density),
                settings.TerrainDetailDensityScale,
                settings.TerrainDetailFallbackDistance,
                settings.WindStrength,
                settings.WindSpeed,
                settings.WindScale,
                settings.EnableGrassShadows,
                settings.EnableTerrainDensityTint,
                settings.DensityGridResolution,
                settings.MacroNoiseScale,
                settings.MicroNoiseScale,
                settings.NoiseOctaves,
                settings.NoisePersistence,
                settings.NoiseLacunarity,
                thresholdLow,
                thresholdHigh,
                noiseContrast);
            return settings;
        }
    }

    public sealed class LocationTerrainProfile
    {
        public float TerrainHeight { get; private set; }
        public float NoiseScale { get; private set; }
        public int Octaves { get; private set; }
        public float Persistence { get; private set; }
        public float Lacunarity { get; private set; }
        public float HeightMultiplier { get; private set; }
        public TerrainLandShape LandShape { get; private set; }
        public float RidgeIntensity { get; private set; }
        public float ValleyIntensity { get; private set; }
        public float CliffIntensity { get; private set; }
        public float TerraceStrength { get; private set; }
        public float MicroReliefStrength { get; private set; }
        public bool UseEdgeFalloff { get; private set; }
        public float EdgeFalloffStart { get; private set; }
        public float EdgeFalloffStrength { get; private set; }
        public bool WaterEnabled { get; private set; }
        public float WaterLevel { get; private set; }
        public Color WaterColor { get; private set; }
        public ForestQualityLevel ForestQuality { get; private set; }
        public float DetailDensity { get; private set; }

        public static LocationTerrainProfile From(WorldLocation location, LocationGenerationConfig config)
        {
            var profile = new LocationTerrainProfile
            {
                TerrainHeight = 145f,
                NoiseScale = 260f,
                Octaves = 5,
                Persistence = 0.45f,
                Lacunarity = 2.0f,
                HeightMultiplier = 0.82f,
                LandShape = TerrainLandShape.Mainland,
                RidgeIntensity = 0.42f,
                ValleyIntensity = 0.28f,
                CliffIntensity = 0.24f,
                TerraceStrength = 0.06f,
                MicroReliefStrength = 0.12f,
                UseEdgeFalloff = false,
                EdgeFalloffStart = 0.88f,
                EdgeFalloffStrength = 1.8f,
                WaterEnabled = true,
                WaterLevel = 14f,
                WaterColor = new Color(0.07f, 0.29f, 0.46f, 0.74f),
                ForestQuality = ForestQualityLevel.High,
                DetailDensity = 0.85f
            };

            switch (location.Type)
            {
                case LocationType.Coast:
                case LocationType.Island:
                    profile.LandShape = location.Type == LocationType.Island ? TerrainLandShape.Islands : TerrainLandShape.Mainland;
                    profile.UseEdgeFalloff = location.Type == LocationType.Island;
                    profile.WaterLevel = 20f;
                    profile.HeightMultiplier = 0.72f;
                    profile.RidgeIntensity = 0.28f;
                    break;
                case LocationType.MountainPass:
                    profile.TerrainHeight = 230f;
                    profile.NoiseScale = 230f;
                    profile.HeightMultiplier = 0.94f;
                    profile.RidgeIntensity = 0.70f;
                    profile.CliffIntensity = 0.58f;
                    profile.TerraceStrength = 0.10f;
                    profile.WaterLevel = 10f;
                    profile.DetailDensity = 0.48f;
                    break;
                case LocationType.Swamp:
                    profile.TerrainHeight = 80f;
                    profile.HeightMultiplier = 0.48f;
                    profile.WaterLevel = 18f;
                    profile.ValleyIntensity = 0.44f;
                    profile.DetailDensity = 1.0f;
                    break;
                case LocationType.Desert:
                    profile.TerrainHeight = 120f;
                    profile.NoiseScale = 320f;
                    profile.RidgeIntensity = 0.25f;
                    profile.WaterLevel = 5f;
                    profile.DetailDensity = 0.28f;
                    profile.ForestQuality = ForestQualityLevel.Low;
                    break;
                case LocationType.Forest:
                    profile.WaterEnabled = false;
                    profile.WaterLevel = 0f;
                    profile.HeightMultiplier = 0.76f;
                    profile.RidgeIntensity = 0.36f;
                    profile.ValleyIntensity = 0.22f;
                    profile.MicroReliefStrength = 0.10f;
                    profile.DetailDensity = 1.0f;
                    profile.ForestQuality = ForestQualityLevel.High;
                    break;
                case LocationType.RiverCrossing:
                    profile.TerrainHeight = 105f;
                    profile.WaterLevel = 16f;
                    profile.ValleyIntensity = 0.52f;
                    profile.DetailDensity = 1.05f;
                    break;
            }

            if (location.DominantBiome == BiomeType.SnowPeaks)
            {
                profile.TerrainHeight = Mathf.Max(profile.TerrainHeight, 240f);
                profile.WaterLevel = 4f;
                profile.DetailDensity *= 0.35f;
            }

            return profile;
        }
    }
}
