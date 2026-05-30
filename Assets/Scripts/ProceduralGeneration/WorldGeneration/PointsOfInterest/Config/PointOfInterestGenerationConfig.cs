using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config
{
    [Serializable]
    public sealed class PointOfInterestGenerationConfig
    {
        [SerializeField] private bool enabled = true;

        [Min(0)]
        [SerializeField] private int minPointsOfInterest = 4;

        [Min(0)]
        [SerializeField] private int maxPointsOfInterest = 9;

        [Min(16f)]
        [SerializeField] private float minimumDistanceBetweenPoi = 150f;

        [Min(16f)]
        [SerializeField] private float minimumDistanceFromSettlements = 85f;

        [Range(1f, 45f)]
        [SerializeField] private float maxSiteSlope = 32f;

        [Min(1)]
        [SerializeField] private int candidateAttempts = 420;

        [SerializeField] private PointOfInterestCatalog catalog;

        public bool Enabled => enabled;
        public int MinPointsOfInterest => Mathf.Max(0, minPointsOfInterest);
        public int MaxPointsOfInterest => Mathf.Max(MinPointsOfInterest, maxPointsOfInterest);
        public float MinimumDistanceBetweenPoi => Mathf.Max(16f, minimumDistanceBetweenPoi);
        public float MinimumDistanceFromSettlements => Mathf.Max(16f, minimumDistanceFromSettlements);
        public float MaxSiteSlope => Mathf.Clamp(maxSiteSlope, 1f, 45f);
        public int CandidateAttempts => Mathf.Max(1, candidateAttempts);
        public PointOfInterestCatalog Catalog => catalog;

        public void ConfigureCatalog(PointOfInterestCatalog pointOfInterestCatalog)
        {
            catalog = pointOfInterestCatalog;
        }
    }

    [Serializable]
    public sealed class PointOfInterestConfigEntry
    {
        [SerializeField] private bool enabled = true;
        [SerializeField] private PointOfInterestType type = PointOfInterestType.Ruins;
        [SerializeField] private string displayName;
        [SerializeField] private string prefabKey;
        [SerializeField] private GameObject prefab;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private Vector2 radiusRange = new(16f, 30f);
        [SerializeField] private Vector2Int dangerRange = new(1, 4);
        [Range(0f, 10f)]
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool connectByRoad = true;

        public bool Enabled => enabled;
        public PointOfInterestType Type => type;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
        public string PrefabKey => prefabKey ?? string.Empty;
        public GameObject Prefab => prefab;
        public IReadOnlyList<string> Tags => tags;
        public Vector2 RadiusRange => NormalizeRange(radiusRange, 4f, 160f);
        public Vector2Int DangerRange => NormalizeRange(dangerRange, 0, 10);
        public float Weight => Mathf.Max(0f, weight);
        public bool ConnectByRoad => connectByRoad;

        public PointOfInterestConfigEntry()
        {
        }

        public PointOfInterestConfigEntry(
            PointOfInterestType type,
            string displayName,
            string prefabKey,
            IReadOnlyList<string> tags,
            Vector2 radiusRange,
            Vector2Int dangerRange,
            float weight,
            bool connectByRoad)
        {
            enabled = true;
            this.type = type;
            this.displayName = displayName ?? type.ToString();
            this.prefabKey = prefabKey ?? string.Empty;
            this.tags = tags == null ? Array.Empty<string>() : new List<string>(tags).ToArray();
            this.radiusRange = radiusRange;
            this.dangerRange = dangerRange;
            this.weight = Mathf.Max(0f, weight);
            this.connectByRoad = connectByRoad;
        }

        public void ConfigurePrefab(GameObject prefabAsset)
        {
            prefab = prefabAsset;
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
    }

    public static class DefaultPointOfInterestCatalog
    {
        public static IReadOnlyList<PointOfInterestConfigEntry> CreateEntries()
        {
            return new[]
            {
                Entry(PointOfInterestType.CaveEntrance, "Cave Entrance", "CaveEntrance_Level1", new[] { "dungeon", "cave", "hidden" }, 16f, 28f, 2, 6, 1.2f, true),
                Entry(PointOfInterestType.BanditCamp, "Bandit Camp", "BanditCamp_Level1", new[] { "hostile", "camp", "quest" }, 18f, 30f, 2, 6, 1.4f, true),
                Entry(PointOfInterestType.AncientTemple, "Ancient Temple", "AncientTemple_Level1", new[] { "sacred", "ruins", "quest" }, 24f, 42f, 3, 8, 0.7f, true),
                Entry(PointOfInterestType.AbandonedHouse, "Abandoned House", "AbandonedHouse_Level1", new[] { "abandoned", "quest" }, 12f, 22f, 0, 3, 1.1f, true),
                Entry(PointOfInterestType.Ruins, "Ruins", "Ruins_Level1", new[] { "ruins", "abandoned" }, 20f, 36f, 1, 5, 1.2f, true),
                Entry(PointOfInterestType.Shrine, "Shrine", "Shrine_Level1", new[] { "sacred", "quest" }, 10f, 18f, 0, 3, 1.0f, true),
                Entry(PointOfInterestType.StoneCircle, "Stone Circle", "StoneCircle_Level1", new[] { "sacred", "ancient" }, 18f, 30f, 1, 4, 0.8f, false),
                Entry(PointOfInterestType.Watchtower, "Ruined Watchtower", "Watchtower_Ruined_Level1", new[] { "military", "ruins" }, 12f, 22f, 1, 5, 1.0f, true),
                Entry(PointOfInterestType.Graveyard, "Graveyard", "Graveyard_Level1", new[] { "undead", "sacred", "quest" }, 18f, 30f, 2, 6, 0.85f, true),
                Entry(PointOfInterestType.MonsterNest, "Monster Nest", "BanditCamp_Level1", new[] { "hostile", "monster", "hidden" }, 16f, 28f, 3, 8, 0.9f, false),
                Entry(PointOfInterestType.ResourceNode, "Mine", "ResourceNode_Mine_Level1", new[] { "resource", "mine" }, 14f, 26f, 0, 4, 1.2f, true),
                Entry(PointOfInterestType.HiddenCache, "Hidden Cache", "StoneCircle_Level1", new[] { "hidden", "treasure" }, 8f, 14f, 0, 2, 0.7f, false),
                Entry(PointOfInterestType.Portal, "Ancient Portal", "AncientTemple_Level1", new[] { "magic", "quest", "ancient" }, 14f, 24f, 4, 9, 0.25f, true),
                Entry(PointOfInterestType.Obelisk, "Obelisk", "AncientTemple_Level1", new[] { "magic", "ancient" }, 10f, 18f, 2, 7, 0.35f, false)
            };
        }

        private static PointOfInterestConfigEntry Entry(
            PointOfInterestType type,
            string name,
            string prefabKey,
            IReadOnlyList<string> tags,
            float minRadius,
            float maxRadius,
            int minDanger,
            int maxDanger,
            float weight,
            bool connectByRoad)
        {
            return new PointOfInterestConfigEntry(
                type,
                name,
                prefabKey,
                tags,
                new Vector2(minRadius, maxRadius),
                new Vector2Int(minDanger, maxDanger),
                weight,
                connectByRoad);
        }
    }
}
