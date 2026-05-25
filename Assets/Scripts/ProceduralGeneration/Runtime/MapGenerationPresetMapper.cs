using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    public static class MapGenerationPresetMapper
    {
        private const string DefaultSettingsResourcePath = "ProceduralGeneration/DefaultProceduralLocationSettings";

        public sealed class Result
        {
            public Result(ProceduralLocationSettings settings, int seed, string summary)
            {
                Settings = settings;
                Seed = seed;
                Summary = summary;
            }

            public ProceduralLocationSettings Settings { get; }
            public int Seed { get; }
            public string Summary { get; }
        }

        public static Result Build(MapGenerationRequest request)
        {
            var safeRequest = request ?? MapGenerationRequest.CreateDefault();
            var seed = safeRequest.ResolveSeed();
            var settings = CreateRuntimeSettings();
            settings.name = "Runtime Map Generation Settings";

            var sizePreset = ResolveSize(safeRequest.MapSize);
            var reliefPreset = ResolveRelief(safeRequest.Relief);
            var landPreset = ResolveLand(safeRequest.LandType, safeRequest.WaterAmount);
            var waterPreset = ResolveWater(safeRequest.WaterAmount, safeRequest.LandType, reliefPreset.TerrainHeight);

            settings.ConfigureGlobal(sizePreset.Width, sizePreset.Length);
            settings.ConfigureSeed(SeedMode.Fixed, seed);
            settings.ConfigureTerrain(
                sizePreset.HeightmapResolution,
                reliefPreset.TerrainHeight,
                reliefPreset.NoiseScale,
                reliefPreset.Octaves,
                reliefPreset.Persistence,
                reliefPreset.Lacunarity,
                reliefPreset.HeightMultiplier,
                landPreset.Shape,
                landPreset.UseEdgeFalloff,
                landPreset.FalloffStart,
                landPreset.FalloffStrength);
            settings.ConfigureWater(true, waterPreset.Level, sizePreset.WaterPadding, waterPreset.Color);
            settings.ConfigureProps(true, BuildPropCategories(settings, safeRequest.PropDensity));

            return new Result(settings, seed, safeRequest.BuildSummary(seed));
        }

        private static ProceduralLocationSettings CreateRuntimeSettings()
        {
            var baseSettings = Resources.Load<ProceduralLocationSettings>(DefaultSettingsResourcePath);
            var settings = baseSettings != null
                ? ScriptableObject.Instantiate(baseSettings)
                : ScriptableObject.CreateInstance<ProceduralLocationSettings>();

            settings.hideFlags = HideFlags.DontSave;
            return settings;
        }

        private static SizePreset ResolveSize(MapSizeOption option)
        {
            return option switch
            {
                MapSizeOption.Small => new SizePreset(500f, 500f, 257, 18f),
                MapSizeOption.Large => new SizePreset(1400f, 1400f, 513, 40f),
                _ => new SizePreset(900f, 900f, 513, 28f)
            };
        }

        private static ReliefPreset ResolveRelief(ReliefOption option)
        {
            return option switch
            {
                ReliefOption.Plains => new ReliefPreset(80f, 360f, 4, 0.34f, 1.85f, 0.50f),
                ReliefOption.Mountains => new ReliefPreset(260f, 225f, 6, 0.54f, 2.18f, 1.0f),
                _ => new ReliefPreset(145f, 285f, 5, 0.44f, 2.0f, 0.72f)
            };
        }

        private static LandPreset ResolveLand(LandTypeOption landType, WaterAmountOption waterAmount)
        {
            if (landType == LandTypeOption.Mainland)
            {
                return new LandPreset(TerrainLandShape.Mainland, false, 0.9f, 1.8f);
            }

            if (landType == LandTypeOption.Islands)
            {
                var start = waterAmount switch
                {
                    WaterAmountOption.Low => 0.73f,
                    WaterAmountOption.High => 0.56f,
                    _ => 0.64f
                };

                return new LandPreset(TerrainLandShape.Islands, true, start, 2.55f);
            }

            var archipelagoStart = waterAmount switch
            {
                WaterAmountOption.Low => 0.78f,
                WaterAmountOption.High => 0.61f,
                _ => 0.69f
            };

            return new LandPreset(TerrainLandShape.Archipelago, true, archipelagoStart, 3.15f);
        }

        private static WaterPreset ResolveWater(WaterAmountOption waterAmount, LandTypeOption landType, float terrainHeight)
        {
            var factor = waterAmount switch
            {
                WaterAmountOption.Low => 0.075f,
                WaterAmountOption.High => 0.205f,
                _ => 0.135f
            };

            if (landType == LandTypeOption.Islands)
            {
                factor += 0.018f;
            }
            else if (landType == LandTypeOption.Archipelago)
            {
                factor += 0.032f;
            }

            return new WaterPreset(Mathf.Max(1f, terrainHeight * factor), new Color(0.06f, 0.32f, 0.53f, 0.78f));
        }

        private static IReadOnlyList<PropCategoryPlacementSettings> BuildPropCategories(
            ProceduralLocationSettings settings,
            PropDensityOption densityOption)
        {
            var multiplier = densityOption switch
            {
                PropDensityOption.Low => 0.45f,
                PropDensityOption.High => 1.85f,
                _ => 1f
            };

            var categories = new List<PropCategoryPlacementSettings>();
            var sourceCategories = settings.PropCategories;
            if (sourceCategories != null && sourceCategories.Count > 0)
            {
                for (var i = 0; i < sourceCategories.Count; i++)
                {
                    var source = sourceCategories[i];
                    if (source == null)
                    {
                        continue;
                    }

                    categories.Add(CloneCategory(source, multiplier, settings.WaterLevel));
                }

                return categories;
            }

            var forest = new PropCategoryPlacementSettings();
            var density = densityOption switch
            {
                PropDensityOption.Low => 0.8f,
                PropDensityOption.High => 4.5f,
                _ => 2.0f
            };

            var minDistance = densityOption switch
            {
                PropDensityOption.Low => 16f,
                PropDensityOption.High => 8f,
                _ => 11f
            };

            forest.Configure(
                "Prototype Forest",
                true,
                RuntimePrototypePropFactory.GetForestPrefabs(),
                density,
                minDistance,
                new Vector2(0f, 34f),
                new Vector2(settings.WaterLevel + 1f, settings.TerrainHeight),
                new Vector2(0.75f, 1.35f),
                true,
                false,
                false,
                450f,
                8f);

            categories.Add(forest);
            return categories;
        }

        private static PropCategoryPlacementSettings CloneCategory(
            PropCategoryPlacementSettings source,
            float densityMultiplier,
            float waterLevel)
        {
            var clone = new PropCategoryPlacementSettings();
            var allowedHeight = source.AllowedHeightRange;
            allowedHeight.x = Mathf.Max(allowedHeight.x, waterLevel + 1f);
            clone.Configure(
                source.CategoryName,
                source.Enabled,
                source.Prefabs,
                source.DensityPer10kSqm * densityMultiplier,
                source.MinDistanceBetweenInstances,
                source.AllowedSlopeRange,
                allowedHeight,
                source.RandomScaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                source.MaxDrawDistance,
                source.AttemptsMultiplier);

            return clone;
        }

        private readonly struct SizePreset
        {
            public SizePreset(float width, float length, int heightmapResolution, float waterPadding)
            {
                Width = width;
                Length = length;
                HeightmapResolution = heightmapResolution;
                WaterPadding = waterPadding;
            }

            public float Width { get; }
            public float Length { get; }
            public int HeightmapResolution { get; }
            public float WaterPadding { get; }
        }

        private readonly struct ReliefPreset
        {
            public ReliefPreset(float terrainHeight, float noiseScale, int octaves, float persistence, float lacunarity, float heightMultiplier)
            {
                TerrainHeight = terrainHeight;
                NoiseScale = noiseScale;
                Octaves = octaves;
                Persistence = persistence;
                Lacunarity = lacunarity;
                HeightMultiplier = heightMultiplier;
            }

            public float TerrainHeight { get; }
            public float NoiseScale { get; }
            public int Octaves { get; }
            public float Persistence { get; }
            public float Lacunarity { get; }
            public float HeightMultiplier { get; }
        }

        private readonly struct LandPreset
        {
            public LandPreset(TerrainLandShape shape, bool useEdgeFalloff, float falloffStart, float falloffStrength)
            {
                Shape = shape;
                UseEdgeFalloff = useEdgeFalloff;
                FalloffStart = falloffStart;
                FalloffStrength = falloffStrength;
            }

            public TerrainLandShape Shape { get; }
            public bool UseEdgeFalloff { get; }
            public float FalloffStart { get; }
            public float FalloffStrength { get; }
        }

        private readonly struct WaterPreset
        {
            public WaterPreset(float level, Color color)
            {
                Level = level;
                Color = color;
            }

            public float Level { get; }
            public Color Color { get; }
        }
    }
}
