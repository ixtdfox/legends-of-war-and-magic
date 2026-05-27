using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    public static class MapGenerationPresetMapper
    {
        private const string DefaultSettingsResourcePath = "ProceduralGeneration/DefaultProceduralLocationSettings";
        private const float TreeDensityAmplifier = 2f;

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
            settings.ConfigureAssetCatalog(ProceduralAssetCatalogResolver.LoadDefaultCatalog());

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
                reliefPreset.RidgeIntensity,
                reliefPreset.ValleyIntensity,
                reliefPreset.CliffIntensity,
                reliefPreset.TerraceStrength,
                reliefPreset.MicroReliefStrength,
                landPreset.UseEdgeFalloff,
                landPreset.FalloffStart,
                landPreset.FalloffStrength);
            settings.ConfigureWater(true, waterPreset.Level, sizePreset.WaterPadding, waterPreset.Color);
            settings.ConfigureForestRendering(ResolveForestQuality(safeRequest.PropDensity, safeRequest.TreeDensity));
            settings.ConfigureGpuGrass(BuildGpuGrassSettings(settings.GpuGrassSettings, safeRequest));
            settings.ConfigureProps(true, BuildPropCategories(settings, safeRequest.PropDensity, safeRequest.TreeDensity, settings.AssetCatalog));
            settings.ConfigureTerrainDetails(
                true,
                ResolveDetailDensity(safeRequest.PropDensity) *
                settings.ForestLodSettings.FoliageDensityScale *
                settings.GpuGrassSettings.TerrainDetailDensityScale,
                sizePreset.DetailResolution,
                64);

            return new Result(settings, seed, safeRequest.BuildSummary(seed));
        }

        private static GpuGrassSettings BuildGpuGrassSettings(GpuGrassSettings source, MapGenerationRequest request)
        {
            var settings = source != null
                ? source.Clone()
                : GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            var saturation = Mathf.Clamp01(request.GrassSaturation);
            var highDistance = Mathf.Clamp(request.GrassHighDetailDistance, 8f, 80f);
            var drawDistance = Mathf.Clamp(request.GrassDrawDistance, highDistance + 12f, 180f);
            var chunkSize = Mathf.Clamp(settings.ChunkSize, 8f, 24f);
            var spacing = Mathf.Lerp(0.95f, 0.38f, saturation);
            var visibleBudget = Mathf.RoundToInt(Mathf.Lerp(12000f, 120000f, saturation));
            var densityScale = Mathf.Lerp(0.25f, 1.55f, saturation);

            settings.Configure(
                settings.Enabled,
                drawDistance,
                highDistance,
                chunkSize,
                spacing,
                visibleBudget,
                densityScale,
                settings.TerrainDetailDensityScale,
                settings.TerrainDetailFallbackDistance,
                settings.WindStrength,
                settings.WindSpeed,
                settings.WindScale,
                settings.ReceiveShadows);
            return settings;
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
                MapSizeOption.Small => new SizePreset(500f, 500f, 257, 18f, 384),
                MapSizeOption.Large => new SizePreset(1400f, 1400f, 513, 40f, 640),
                _ => new SizePreset(900f, 900f, 513, 28f, 512)
            };
        }

        private static ReliefPreset ResolveRelief(ReliefOption option)
        {
            return option switch
            {
                ReliefOption.Plains => new ReliefPreset(95f, 390f, 4, 0.34f, 1.82f, 0.58f, 0.16f, 0.16f, 0.06f, 0.02f, 0.08f),
                ReliefOption.Mountains => new ReliefPreset(235f, 310f, 6, 0.49f, 2.05f, 0.88f, 0.66f, 0.42f, 0.62f, 0.12f, 0.15f),
                _ => new ReliefPreset(165f, 275f, 5, 0.46f, 2.04f, 0.78f, 0.50f, 0.32f, 0.42f, 0.08f, 0.14f)
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
            PropDensityOption densityOption,
            float treeDensity,
            ProceduralEnvironmentAssetCatalog assetCatalog)
        {
            var multiplier = densityOption switch
            {
                PropDensityOption.Low => 0.65f,
                PropDensityOption.High => 4.35f,
                _ => 1.55f
            };

            var categories = new List<PropCategoryPlacementSettings>();
            if (assetCatalog != null && assetCatalog.HasPropPrefabs())
            {
                for (var i = 0; i < assetCatalog.PropCategories.Count; i++)
                {
                    var category = assetCatalog.PropCategories[i];
                    if (category == null || !category.HasPrefabs)
                    {
                        continue;
                    }

                    var placement = category.CreatePlacementSettings(multiplier, settings.WaterLevel, settings.TerrainHeight);
                    placement = TuneRuntimePropPlacementIfNeeded(placement, densityOption, treeDensity);
                    placement = ApplyForestRenderBudget(placement, settings.ForestLodSettings);
                    if (placement == null)
                    {
                        continue;
                    }

                    categories.Add(placement);
                }

                if (categories.Count > 0)
                {
                    return categories;
                }
            }

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

                    if (ContainsPrototypePrefabs(source))
                    {
                        Debug.LogWarning(
                            $"Prop category '{source.CategoryName}' was skipped because it references prototype runtime prefabs. " +
                            "Rebuild the Fristy Resources environment catalog to generate production props.");
                        continue;
                    }

                    var placement = TuneRuntimePropPlacementIfNeeded(CloneCategory(source, multiplier, settings.WaterLevel), densityOption, treeDensity);
                    placement = ApplyForestRenderBudget(placement, settings.ForestLodSettings);
                    if (placement != null)
                    {
                        categories.Add(placement);
                    }
                }

                if (categories.Count > 0)
                {
                    return categories;
                }
            }

            Debug.LogError(
                "No production environment prop prefabs were resolved. " +
                "Generation will continue without props instead of spawning primitive prototype trees/rocks/grass. " +
                "Run Tools/Legends of War and Magic/Procedural Generation/Organize Fristy Nature Resources.");
            return categories;
        }

        private static ForestQualityLevel ResolveForestQuality(PropDensityOption densityOption, float treeDensity)
        {
            var normalizedTrees = Mathf.Clamp01(treeDensity);
            if (densityOption == PropDensityOption.Low && normalizedTrees < 0.45f)
            {
                return ForestQualityLevel.Medium;
            }

            if (densityOption == PropDensityOption.High || normalizedTrees >= 0.72f)
            {
                return ForestQualityLevel.High;
            }

            return ForestQualityLevel.High;
        }

        private static PropCategoryPlacementSettings ApplyForestRenderBudget(
            PropCategoryPlacementSettings source,
            ForestLodSettings forestLodSettings)
        {
            if (source == null || forestLodSettings == null)
            {
                return source;
            }

            var density = source.DensityPer10kSqm;
            var maxDrawDistance = source.MaxDrawDistance;

            if (IsCoreTreeRole(source.Role) || IsAccentTreeRole(source.Role))
            {
                maxDrawDistance = ResolveLimitedDrawDistance(maxDrawDistance, forestLodSettings.TreeCullDistance);
            }
            else if (IsForestCompanionRole(source.Role))
            {
                density *= forestLodSettings.FoliageDensityScale;
            }
            else
            {
                return source;
            }

            var tuned = new PropCategoryPlacementSettings();
            tuned.Configure(
                source.CategoryName,
                source.Enabled,
                source.Prefabs,
                density,
                source.MinDistanceBetweenInstances,
                source.AllowedSlopeRange,
                source.AllowedHeightRange,
                source.RandomScaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                maxDrawDistance,
                source.AttemptsMultiplier);

            tuned.ConfigureRoleAndBiome(
                source.Role,
                source.UseClusterPlacement,
                source.ClusterThreshold,
                source.ClusterNoiseScale,
                source.ClusterStrength,
                source.SlopeAffinity,
                source.ShoreAffinity,
                source.ForestEdgeAffinity);

            return tuned;
        }

        private static float ResolveLimitedDrawDistance(float categoryDistance, float forestCullDistance)
        {
            if (forestCullDistance <= 0f)
            {
                return categoryDistance;
            }

            return categoryDistance <= 0f
                ? forestCullDistance
                : Mathf.Min(categoryDistance, forestCullDistance);
        }

        private static PropCategoryPlacementSettings TuneRuntimePropPlacementIfNeeded(
            PropCategoryPlacementSettings source,
            PropDensityOption densityOption,
            float treeDensity)
        {
            var tuned = TuneTreePlacementIfNeeded(source, treeDensity);
            if (tuned == null || !ShouldUseDenseForestPropBudget(densityOption, treeDensity))
            {
                return tuned;
            }

            return TuneDenseForestCompanionPlacement(tuned);
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
            clone.ConfigureRoleAndBiome(
                source.Role,
                source.UseClusterPlacement,
                source.ClusterThreshold,
                source.ClusterNoiseScale,
                source.ClusterStrength,
                source.SlopeAffinity,
                source.ShoreAffinity,
                source.ForestEdgeAffinity);

            return clone;
        }

        private static PropCategoryPlacementSettings TuneTreePlacementIfNeeded(
            PropCategoryPlacementSettings source,
            float treeDensity)
        {
            if (source == null)
            {
                return source;
            }

            if (IsAccentTreeRole(source.Role))
            {
                return TuneAccentTreePlacement(source, treeDensity);
            }

            if (!IsCoreTreeRole(source.Role))
            {
                return source;
            }

            var normalized = Mathf.Clamp01(treeDensity);
            var densityMultiplier = ResolveTreeDensityMultiplier(normalized) * TreeDensityAmplifier;
            var minDistance = Mathf.Lerp(
                source.MinDistanceBetweenInstances * 1.28f,
                Mathf.Max(1.35f, source.MinDistanceBetweenInstances * 0.28f),
                normalized);
            var slopeRange = source.AllowedSlopeRange;
            slopeRange.y = Mathf.Lerp(Mathf.Min(slopeRange.y, 28f), Mathf.Max(slopeRange.y, 42f), normalized);
            var scaleRange = new Vector2(
                Mathf.Lerp(Mathf.Max(0.82f, source.RandomScaleRange.x), 0.78f, normalized),
                Mathf.Lerp(Mathf.Max(1.25f, source.RandomScaleRange.y), 1.46f, normalized));

            var tuned = new PropCategoryPlacementSettings();
            tuned.Configure(
                source.CategoryName,
                source.Enabled,
                source.Prefabs,
                source.DensityPer10kSqm * densityMultiplier,
                minDistance,
                slopeRange,
                source.AllowedHeightRange,
                scaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                Mathf.Lerp(source.MaxDrawDistance, 170f, normalized),
                Mathf.Lerp(8f, 18f, normalized));

            tuned.ConfigureRoleAndBiome(
                source.Role,
                true,
                Mathf.Lerp(0.62f, 0.14f, normalized),
                Mathf.Lerp(330f, 165f, normalized),
                Mathf.Lerp(0.9f, 3.45f, normalized),
                0.02f,
                0.02f,
                0f);

            return tuned;
        }

        private static PropCategoryPlacementSettings TuneAccentTreePlacement(
            PropCategoryPlacementSettings source,
            float treeDensity)
        {
            var normalized = Mathf.Clamp01(treeDensity);
            var densityMultiplier = Mathf.Lerp(0.35f, 3.6f, Mathf.SmoothStep(0f, 1f, normalized));
            var scaleRange = new Vector2(
                Mathf.Lerp(Mathf.Max(0.78f, source.RandomScaleRange.x), 0.76f, normalized),
                Mathf.Lerp(Mathf.Max(1.12f, source.RandomScaleRange.y), 1.32f, normalized));
            var tuned = new PropCategoryPlacementSettings();
            tuned.Configure(
                source.CategoryName,
                source.Enabled,
                source.Prefabs,
                source.DensityPer10kSqm * densityMultiplier,
                Mathf.Lerp(source.MinDistanceBetweenInstances * 1.25f, Mathf.Max(2.7f, source.MinDistanceBetweenInstances * 0.42f), normalized),
                source.AllowedSlopeRange,
                source.AllowedHeightRange,
                scaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                Mathf.Lerp(source.MaxDrawDistance, 170f, normalized),
                Mathf.Lerp(9f, 18f, normalized));

            tuned.ConfigureRoleAndBiome(
                source.Role,
                true,
                Mathf.Lerp(0.72f, 0.38f, normalized),
                Mathf.Lerp(160f, 120f, normalized),
                Mathf.Lerp(1.6f, 3.0f, normalized),
                0.02f,
                0.02f,
                0.2f);
            return tuned;
        }

        private static bool IsCoreTreeRole(ProceduralPropRole role)
        {
            return role == ProceduralPropRole.Tree || role == ProceduralPropRole.ForestCoreTrees;
        }

        private static bool IsAccentTreeRole(ProceduralPropRole role)
        {
            return role == ProceduralPropRole.ForestAccentTrees;
        }

        private static bool IsForestCompanionRole(ProceduralPropRole role)
        {
            return role == ProceduralPropRole.Bushes ||
                   role == ProceduralPropRole.GroundGrass ||
                   role == ProceduralPropRole.GroundPlants ||
                   role == ProceduralPropRole.ShorePlants;
        }

        private static bool ShouldUseDenseForestPropBudget(PropDensityOption densityOption, float treeDensity)
        {
            return densityOption == PropDensityOption.High || treeDensity >= 0.72f;
        }

        private static PropCategoryPlacementSettings TuneDenseForestCompanionPlacement(PropCategoryPlacementSettings source)
        {
            return source.Role switch
            {
                ProceduralPropRole.GroundGrass => null,
                ProceduralPropRole.Bushes => CloneCompanionCategory(source, 0.85f, 1.0f, 80f, 20f, 0.24f, 48f, 1),
                ProceduralPropRole.GroundPlants => CloneCompanionCategory(source, 0.48f, 1.0f, 60f, 20f, 0.20f, 46f, 1),
                ProceduralPropRole.ShorePlants => null,
                ProceduralPropRole.RocksSmallMedium => CloneCompanionCategory(source, 0.44f, 0.95f, 105f, 16f, 0.38f, 66f, 1),
                ProceduralPropRole.RocksLarge => CloneCompanionCategory(source, 0.42f, 0.95f, 125f, 16f, 0.44f, 70f, 1),
                ProceduralPropRole.Log => CloneCompanionCategory(source, 0.50f, 1.0f, 80f, 20f, 0.28f, 36f, 1),
                _ => source
            };
        }

        private static PropCategoryPlacementSettings CloneCompanionCategory(
            PropCategoryPlacementSettings source,
            float densityScale,
            float minDistanceScale,
            float maxDrawDistance,
            float attemptsMultiplier,
            float clusterThreshold,
            float slopeMax,
            int maxPrefabCount)
        {
            var slopeRange = source.AllowedSlopeRange;
            slopeRange.y = Mathf.Max(slopeRange.y, slopeMax);
            var prefabs = SelectBudgetCompanionPrefabs(source.Role, source.Prefabs, maxPrefabCount);

            var tuned = new PropCategoryPlacementSettings();
            tuned.Configure(
                source.CategoryName,
                source.Enabled,
                prefabs,
                source.DensityPer10kSqm * densityScale,
                source.MinDistanceBetweenInstances * minDistanceScale,
                slopeRange,
                source.AllowedHeightRange,
                source.RandomScaleRange,
                source.RandomYRotation,
                source.WarnIfMissingLodGroup,
                source.ExpectLodGroup,
                Mathf.Min(source.MaxDrawDistance, maxDrawDistance),
                Mathf.Max(source.AttemptsMultiplier, attemptsMultiplier));

            tuned.ConfigureRoleAndBiome(
                source.Role,
                source.UseClusterPlacement,
                Mathf.Min(source.ClusterThreshold, clusterThreshold),
                source.ClusterNoiseScale,
                source.ClusterStrength,
                source.SlopeAffinity,
                source.ShoreAffinity,
                source.ForestEdgeAffinity);

            return tuned;
        }

        private static GameObject[] SelectBudgetCompanionPrefabs(
            ProceduralPropRole role,
            GameObject[] prefabs,
            int maxPrefabCount)
        {
            if (prefabs == null || prefabs.Length == 0 || prefabs.Length <= maxPrefabCount)
            {
                return prefabs;
            }

            var selected = new List<GameObject>();
            AddPreferredBudgetPrefabs(role, prefabs, selected, maxPrefabCount, true);
            AddPreferredBudgetPrefabs(role, prefabs, selected, maxPrefabCount, false);
            return selected.ToArray();
        }

        private static void AddPreferredBudgetPrefabs(
            ProceduralPropRole role,
            GameObject[] prefabs,
            List<GameObject> selected,
            int maxPrefabCount,
            bool preferredOnly)
        {
            for (var i = 0; i < prefabs.Length && selected.Count < maxPrefabCount; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null || selected.Contains(prefab))
                {
                    continue;
                }

                var preferred = IsPreferredBudgetPrefab(role, prefab.name);
                if (preferredOnly != preferred)
                {
                    continue;
                }

                selected.Add(prefab);
            }
        }

        private static bool IsPreferredBudgetPrefab(ProceduralPropRole role, string prefabName)
        {
            var key = string.IsNullOrWhiteSpace(prefabName) ? string.Empty : prefabName.ToLowerInvariant();
            return role switch
            {
                ProceduralPropRole.Bushes => key.Contains("bush") && !key.Contains("vegetation_01"),
                ProceduralPropRole.RocksLarge => !key.Contains("boulder_03") && !key.Contains("vegetation"),
                _ => true
            };
        }

        private static bool ContainsPrototypePrefabs(PropCategoryPlacementSettings source)
        {
            if (source == null)
            {
                return false;
            }

            if (source.CategoryName.ToLowerInvariant().Contains("prototype"))
            {
                return true;
            }

            var prefabs = source.Prefabs;
            if (prefabs == null)
            {
                return false;
            }

            for (var i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                if (prefab != null && prefab.name.ToLowerInvariant().Contains("prototype"))
                {
                    return true;
                }
            }

            return false;
        }

        private static float ResolveTreeDensityMultiplier(float normalizedDensity)
        {
            if (normalizedDensity <= 0.72f)
            {
                return Mathf.Lerp(0.20f, 1.35f, normalizedDensity / 0.72f);
            }

            var highRange = Mathf.InverseLerp(0.72f, 1f, normalizedDensity);
            var eased = Mathf.SmoothStep(0f, 1f, highRange);
            return Mathf.Lerp(1.35f, 4.9f, eased);
        }

        private static float ResolveDetailDensity(PropDensityOption option)
        {
            return option switch
            {
                PropDensityOption.Low => 0.75f,
                PropDensityOption.High => 2.7f,
                _ => 1.45f
            };
        }

        private readonly struct SizePreset
        {
            public SizePreset(float width, float length, int heightmapResolution, float waterPadding, int detailResolution)
            {
                Width = width;
                Length = length;
                HeightmapResolution = heightmapResolution;
                WaterPadding = waterPadding;
                DetailResolution = detailResolution;
            }

            public float Width { get; }
            public float Length { get; }
            public int HeightmapResolution { get; }
            public float WaterPadding { get; }
            public int DetailResolution { get; }
        }

        private readonly struct ReliefPreset
        {
            public ReliefPreset(
                float terrainHeight,
                float noiseScale,
                int octaves,
                float persistence,
                float lacunarity,
                float heightMultiplier,
                float ridgeIntensity,
                float valleyIntensity,
                float cliffIntensity,
                float terraceStrength,
                float microReliefStrength)
            {
                TerrainHeight = terrainHeight;
                NoiseScale = noiseScale;
                Octaves = octaves;
                Persistence = persistence;
                Lacunarity = lacunarity;
                HeightMultiplier = heightMultiplier;
                RidgeIntensity = ridgeIntensity;
                ValleyIntensity = valleyIntensity;
                CliffIntensity = cliffIntensity;
                TerraceStrength = terraceStrength;
                MicroReliefStrength = microReliefStrength;
            }

            public float TerrainHeight { get; }
            public float NoiseScale { get; }
            public int Octaves { get; }
            public float Persistence { get; }
            public float Lacunarity { get; }
            public float HeightMultiplier { get; }
            public float RidgeIntensity { get; }
            public float ValleyIntensity { get; }
            public float CliffIntensity { get; }
            public float TerraceStrength { get; }
            public float MicroReliefStrength { get; }
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
