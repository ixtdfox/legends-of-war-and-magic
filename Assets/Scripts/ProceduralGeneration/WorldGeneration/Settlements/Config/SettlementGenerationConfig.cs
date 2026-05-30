using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config
{
    [Serializable]
    public sealed class SettlementGenerationConfig
    {
        [SerializeField] private bool enabled = true;

        [Header("Counts")]
        [Min(0)]
        [SerializeField] private int minSettlements = 1;

        [Min(0)]
        [SerializeField] private int maxSettlements = 3;

        [Min(0)]
        [SerializeField] private int minCamps = 1;

        [Min(0)]
        [SerializeField] private int maxCamps = 3;

        [Range(0f, 1f)]
        [SerializeField] private float largeSettlementChance = 0.28f;

        [Range(0f, 1f)]
        [SerializeField] private float capitalChance = 0.03f;

        [Header("Placement")]
        [Min(16f)]
        [SerializeField] private float candidateSpacing = 115f;

        [Min(16f)]
        [SerializeField] private float minimumSettlementDistance = 260f;

        [Min(0f)]
        [SerializeField] private float waterSearchRadius = 180f;

        [Range(1f, 45f)]
        [SerializeField] private float maxBuildingSlope = 15f;

        [Range(1f, 45f)]
        [SerializeField] private float maxSiteSlope = 22f;

        [Min(0f)]
        [SerializeField] private float edgePadding = 160f;

        [Header("Layout")]
        [Min(1)]
        [SerializeField] private int layoutCandidateAttempts = 320;

        [Min(4f)]
        [SerializeField] private float plotPadding = 3.5f;

        [Min(0f)]
        [SerializeField] private float clearingRadiusPadding = 34f;

        [Header("Content")]
        [SerializeField] private SettlementBuildingCatalog buildingCatalog;

        public bool Enabled => enabled;
        public int MinSettlements => Mathf.Max(0, minSettlements);
        public int MaxSettlements => Mathf.Max(MinSettlements, maxSettlements);
        public int MinCamps => Mathf.Max(0, minCamps);
        public int MaxCamps => Mathf.Max(MinCamps, maxCamps);
        public float LargeSettlementChance => Mathf.Clamp01(largeSettlementChance);
        public float CapitalChance => Mathf.Clamp01(capitalChance);
        public float CandidateSpacing => Mathf.Max(16f, candidateSpacing);
        public float MinimumSettlementDistance => Mathf.Max(16f, minimumSettlementDistance);
        public float WaterSearchRadius => Mathf.Max(0f, waterSearchRadius);
        public float MaxBuildingSlope => Mathf.Clamp(maxBuildingSlope, 1f, 45f);
        public float MaxSiteSlope => Mathf.Clamp(maxSiteSlope, 1f, 45f);
        public float EdgePadding => Mathf.Max(0f, edgePadding);
        public int LayoutCandidateAttempts => Mathf.Max(1, layoutCandidateAttempts);
        public float PlotPadding => Mathf.Max(0f, plotPadding);
        public float ClearingRadiusPadding => Mathf.Max(0f, clearingRadiusPadding);
        public SettlementBuildingCatalog BuildingCatalog => buildingCatalog;

        public void ConfigureCatalog(SettlementBuildingCatalog catalog)
        {
            buildingCatalog = catalog;
        }
    }

    [Serializable]
    public sealed class SettlementTierConfig
    {
        [SerializeField] private SettlementTier tier = SettlementTier.Village;
        [SerializeField] private Vector2 radiusRange = new(55f, 95f);
        [SerializeField] private Vector2Int buildingCountRange = new(5, 12);
        [SerializeField] private BuildingType[] requiredBuildings = Array.Empty<BuildingType>();
        [SerializeField] private BuildingType[] optionalBuildings = Array.Empty<BuildingType>();
        [SerializeField] private float roadWidth = 3f;

        public SettlementTier Tier => tier;
        public Vector2 RadiusRange => NormalizeRange(radiusRange, 8f, 360f);
        public Vector2Int BuildingCountRange => NormalizeRange(buildingCountRange, 1, 220);
        public IReadOnlyList<BuildingType> RequiredBuildings => requiredBuildings;
        public IReadOnlyList<BuildingType> OptionalBuildings => optionalBuildings;
        public float RoadWidth => Mathf.Max(0.5f, roadWidth);

        public SettlementTierConfig()
        {
        }

        public SettlementTierConfig(
            SettlementTier tier,
            Vector2 radiusRange,
            Vector2Int buildingCountRange,
            IReadOnlyList<BuildingType> required,
            IReadOnlyList<BuildingType> optional,
            float roadWidth)
        {
            this.tier = tier;
            this.radiusRange = radiusRange;
            this.buildingCountRange = buildingCountRange;
            requiredBuildings = ToArray(required);
            optionalBuildings = ToArray(optional);
            this.roadWidth = roadWidth;
        }

        private static Vector2 NormalizeRange(Vector2 range, float minClamp, float maxClamp)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), minClamp, maxClamp);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), minClamp, maxClamp);
            return new Vector2(min, max);
        }

        private static Vector2Int NormalizeRange(Vector2Int range, int minClamp, int maxClamp)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), minClamp, maxClamp);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), minClamp, maxClamp);
            return new Vector2Int(min, max);
        }

        private static BuildingType[] ToArray(IReadOnlyList<BuildingType> values)
        {
            if (values == null || values.Count == 0)
            {
                return Array.Empty<BuildingType>();
            }

            var result = new BuildingType[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                result[i] = values[i];
            }

            return result;
        }
    }

    [Serializable]
    public sealed class SettlementBuildingConfigEntry
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private BuildingType type = BuildingType.House;
        [Min(1)]
        [SerializeField] private int level = 1;
        [SerializeField] private SettlementTier minSettlementTier = SettlementTier.Hamlet;
        [SerializeField] private Vector2 footprintSize = new(8f, 8f);
        [SerializeField] private BuildingTag[] tags = Array.Empty<BuildingTag>();
        [SerializeField] private string prefabKey;
        [SerializeField] private GameObject prefab;

        public bool Enabled => enabled;
        public string Id => id;
        public string DisplayName => displayName;
        public BuildingType Type => type;
        public int Level => Mathf.Max(1, level);
        public SettlementTier MinSettlementTier => minSettlementTier;
        public Vector2 FootprintSize => new(Mathf.Max(0.5f, footprintSize.x), Mathf.Max(0.5f, footprintSize.y));
        public IReadOnlyList<BuildingTag> Tags => tags;
        public string PrefabKey => prefabKey;
        public GameObject Prefab => prefab;

        public SettlementBuildingConfigEntry()
        {
        }

        public SettlementBuildingConfigEntry(
            BuildingType type,
            int level,
            SettlementTier minTier,
            Vector2 footprint,
            IReadOnlyList<BuildingTag> tags,
            string prefabKey,
            string displayName = null)
        {
            enabled = true;
            this.type = type;
            this.level = Mathf.Max(1, level);
            minSettlementTier = minTier;
            footprintSize = footprint;
            this.tags = tags == null ? Array.Empty<BuildingTag>() : new List<BuildingTag>(tags).ToArray();
            this.prefabKey = prefabKey ?? string.Empty;
            this.displayName = displayName ?? $"{type} Level {level}";
            id = $"{type}_Level{this.level}";
        }

        public void ConfigurePrefab(GameObject prefabAsset)
        {
            prefab = prefabAsset;
        }

        public GeneratedSettlementBuildingDefinition ToDefinition()
        {
            return new GeneratedSettlementBuildingDefinition(
                Id,
                DisplayName,
                Type,
                Level,
                MinSettlementTier,
                FootprintSize,
                Tags,
                PrefabKey);
        }
    }

    public static class DefaultSettlementBuildingCatalog
    {
        public static IReadOnlyList<GeneratedSettlementBuildingDefinition> CreateDefinitions()
        {
            var entries = new List<SettlementBuildingConfigEntry>
            {
                Entry(BuildingType.Tent, 1, SettlementTier.Camp, 5f, 4f, "Tent_Level1", BuildingTag.Temporary, BuildingTag.Residential),
                Entry(BuildingType.Campfire, 1, SettlementTier.Camp, 4f, 4f, "Campfire_Level1", BuildingTag.Temporary, BuildingTag.Service),
                Entry(BuildingType.Storage, 1, SettlementTier.Camp, 5f, 4f, "CratePile_Level1", BuildingTag.Economy, BuildingTag.Resource),
                Entry(BuildingType.Wall, 1, SettlementTier.Camp, 6f, 2f, "SimpleFence_Level1", BuildingTag.Defense),
                Entry(BuildingType.House, 1, SettlementTier.Hamlet, 8f, 8f, "SmallHouse_Level1", BuildingTag.Residential),
                Entry(BuildingType.Longhouse, 1, SettlementTier.Hamlet, 13f, 8f, "Longhouse_Level1", BuildingTag.Residential),
                Entry(BuildingType.Well, 1, SettlementTier.Hamlet, 5f, 5f, "Well_Level1", BuildingTag.Service),
                Entry(BuildingType.Storage, 1, SettlementTier.Hamlet, 9f, 8f, "Storage_Level1", BuildingTag.Economy, BuildingTag.Resource),
                Entry(BuildingType.Farm, 1, SettlementTier.Hamlet, 14f, 10f, "Barn_Level1", BuildingTag.Rural, BuildingTag.Resource),
                Entry(BuildingType.Blacksmith, 1, SettlementTier.Village, 10f, 9f, "Blacksmith_Level1", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.Watchtower, 1, SettlementTier.Hamlet, 6f, 6f, "WoodenWatchtower_Level1", BuildingTag.Military, BuildingTag.Defense),
                Entry(BuildingType.Wall, 1, SettlementTier.Hamlet, 8f, 2f, "WoodenFence_Level1", BuildingTag.Defense),
                Entry(BuildingType.House, 2, SettlementTier.Town, 10f, 9f, "House_Level2", BuildingTag.Residential),
                Entry(BuildingType.Tavern, 1, SettlementTier.Town, 13f, 10f, "Tavern_Level1", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.Market, 1, SettlementTier.Town, 16f, 14f, "MarketStall_Level1", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.Blacksmith, 2, SettlementTier.Town, 12f, 10f, "Blacksmith_Level2", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.Workshop, 1, SettlementTier.Town, 11f, 10f, "Workshop_Level1", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.TownHall, 1, SettlementTier.Town, 16f, 12f, "TownHall_Level1", BuildingTag.Civic),
                Entry(BuildingType.Gate, 1, SettlementTier.Town, 12f, 5f, "WoodenGate_Level1", BuildingTag.Defense),
                Entry(BuildingType.Wall, 2, SettlementTier.Town, 10f, 3f, "PalisadeWall_Level1", BuildingTag.Defense),
                Entry(BuildingType.House, 3, SettlementTier.City, 11f, 10f, "StoneHouse_Level1", BuildingTag.Residential),
                Entry(BuildingType.Temple, 1, SettlementTier.City, 18f, 14f, "Temple_Level1", BuildingTag.Sacred, BuildingTag.Service),
                Entry(BuildingType.Barracks, 1, SettlementTier.City, 16f, 12f, "Barracks_Level1", BuildingTag.Military),
                Entry(BuildingType.Gate, 2, SettlementTier.City, 16f, 6f, "CityGate_Level1", BuildingTag.Defense),
                Entry(BuildingType.Wall, 3, SettlementTier.City, 12f, 4f, "StoneWall_Level1", BuildingTag.Defense),
                Entry(BuildingType.TownHall, 2, SettlementTier.City, 22f, 16f, "TownHall_Level2", BuildingTag.Civic),
                Entry(BuildingType.Market, 2, SettlementTier.City, 22f, 18f, "MarketSquare_Level1", BuildingTag.Service, BuildingTag.Economy),
                Entry(BuildingType.Shrine, 1, SettlementTier.Village, 7f, 7f, "Shrine_Level1", BuildingTag.Sacred)
            };

            var definitions = new List<GeneratedSettlementBuildingDefinition>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                definitions.Add(entries[i].ToDefinition());
            }

            return definitions;
        }

        public static SettlementTierConfig CreateTierConfig(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => new SettlementTierConfig(
                    tier,
                    new Vector2(22f, 34f),
                    new Vector2Int(3, 5),
                    new[] { BuildingType.Campfire },
                    new[] { BuildingType.Tent, BuildingType.Storage, BuildingType.Wall },
                    1.8f),
                SettlementTier.Hamlet => new SettlementTierConfig(
                    tier,
                    new Vector2(45f, 70f),
                    new Vector2Int(5, 9),
                    new[] { BuildingType.House, BuildingType.Well, BuildingType.Storage },
                    new[] { BuildingType.Longhouse, BuildingType.Farm, BuildingType.Watchtower, BuildingType.Wall },
                    2.4f),
                SettlementTier.Village => new SettlementTierConfig(
                    tier,
                    new Vector2(70f, 110f),
                    new Vector2Int(9, 18),
                    new[] { BuildingType.House, BuildingType.Well, BuildingType.Storage },
                    new[] { BuildingType.Blacksmith, BuildingType.Farm, BuildingType.Shrine, BuildingType.Watchtower },
                    3.2f),
                SettlementTier.Town => new SettlementTierConfig(
                    tier,
                    new Vector2(115f, 170f),
                    new Vector2Int(18, 36),
                    new[] { BuildingType.TownHall, BuildingType.Market, BuildingType.Tavern, BuildingType.Blacksmith },
                    new[] { BuildingType.Workshop, BuildingType.Gate, BuildingType.Wall, BuildingType.Stable, BuildingType.Watchtower },
                    4.5f),
                SettlementTier.City => new SettlementTierConfig(
                    tier,
                    new Vector2(180f, 255f),
                    new Vector2Int(34, 72),
                    new[] { BuildingType.TownHall, BuildingType.Market, BuildingType.Temple, BuildingType.Barracks, BuildingType.Gate, BuildingType.Wall },
                    new[] { BuildingType.Workshop, BuildingType.Tavern, BuildingType.Blacksmith, BuildingType.Stable },
                    5.5f),
                SettlementTier.Capital => new SettlementTierConfig(
                    tier,
                    new Vector2(240f, 330f),
                    new Vector2Int(58, 120),
                    new[] { BuildingType.TownHall, BuildingType.Market, BuildingType.Temple, BuildingType.Barracks, BuildingType.Gate, BuildingType.Wall },
                    new[] { BuildingType.Workshop, BuildingType.Tavern, BuildingType.Blacksmith, BuildingType.Stable, BuildingType.Shrine },
                    6.5f),
                _ => CreateTierConfig(SettlementTier.Village)
            };
        }

        private static SettlementBuildingConfigEntry Entry(
            BuildingType type,
            int level,
            SettlementTier minTier,
            float width,
            float length,
            string prefabKey,
            params BuildingTag[] tags)
        {
            return new SettlementBuildingConfigEntry(type, level, minTier, new Vector2(width, length), tags, prefabKey);
        }
    }
}
