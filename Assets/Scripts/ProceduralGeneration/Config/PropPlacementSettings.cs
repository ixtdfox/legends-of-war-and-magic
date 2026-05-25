using System;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    /// <summary>
    /// Defines placement behavior for a single prop category (trees, rocks, bushes, etc.).
    /// </summary>
    [Serializable]
    public class PropCategoryPlacementSettings
    {
        [SerializeField] private string categoryName = "Trees";
        [SerializeField] private ProceduralPropRole role = ProceduralPropRole.Tree;
        [SerializeField] private bool enabled = true;
        [SerializeField] private GameObject[] prefabs = Array.Empty<GameObject>();

        [Tooltip("Instances per 10,000 square meters (100m x 100m).")]
        [Min(0f)]
        [SerializeField] private float densityPer10kSqm = 20f;

        [Min(0f)]
        [SerializeField] private float minDistanceBetweenInstances = 3f;

        [SerializeField] private Vector2 allowedSlopeRange = new(0f, 35f);
        [SerializeField] private Vector2 allowedHeightRange = new(0f, 220f);
        [SerializeField] private Vector2 randomScaleRange = new(0.9f, 1.2f);
        [SerializeField] private bool randomYRotation = true;

        [Tooltip("Logs a warning if a prefab in this category does not include an LODGroup component.")]
        [SerializeField] private bool warnIfMissingLodGroup = true;

        [Tooltip("If enabled, generation logs explicit warnings for prefabs without LODGroup in this category.")]
        [SerializeField] private bool expectLodGroup = false;

        [Tooltip("Optional max visible distance in world units. 0 disables generator-side distance culling.")]
        [Min(0f)]
        [SerializeField] private float maxDrawDistance = 0f;

        [Tooltip("Higher values try more candidates before giving up.")]
        [Range(1f, 20f)]
        [SerializeField] private float attemptsMultiplier = 6f;

        [Header("Biome Placement")]
        [SerializeField] private bool useClusterPlacement = true;

        [Range(0f, 1f)]
        [SerializeField] private float clusterThreshold = 0.52f;

        [Min(1f)]
        [SerializeField] private float clusterNoiseScale = 240f;

        [Range(0f, 4f)]
        [SerializeField] private float clusterStrength = 1f;

        [Range(0f, 3f)]
        [SerializeField] private float slopeAffinity = 0f;

        [Range(0f, 3f)]
        [SerializeField] private float shoreAffinity = 0f;

        [Range(0f, 3f)]
        [SerializeField] private float forestEdgeAffinity = 0f;

        public string CategoryName => string.IsNullOrWhiteSpace(categoryName) ? "Props" : categoryName;
        public ProceduralPropRole Role => role;
        public bool Enabled => enabled;
        public GameObject[] Prefabs => prefabs;
        public float DensityPer10kSqm => densityPer10kSqm;
        public float MinDistanceBetweenInstances => minDistanceBetweenInstances;
        public Vector2 AllowedSlopeRange => NormalizeRange(allowedSlopeRange, 0f, 90f);
        public Vector2 AllowedHeightRange => NormalizeRange(allowedHeightRange, float.MinValue, float.MaxValue);
        public Vector2 RandomScaleRange => NormalizeRange(randomScaleRange, 0.01f, 100f);
        public bool RandomYRotation => randomYRotation;
        public bool WarnIfMissingLodGroup => warnIfMissingLodGroup;
        public bool ExpectLodGroup => expectLodGroup;
        public float MaxDrawDistance => maxDrawDistance;
        public float AttemptsMultiplier => attemptsMultiplier;
        public bool UseClusterPlacement => useClusterPlacement;
        public float ClusterThreshold => Mathf.Clamp01(clusterThreshold);
        public float ClusterNoiseScale => Mathf.Max(1f, clusterNoiseScale);
        public float ClusterStrength => Mathf.Clamp(clusterStrength, 0f, 4f);
        public float SlopeAffinity => Mathf.Clamp(slopeAffinity, 0f, 3f);
        public float ShoreAffinity => Mathf.Clamp(shoreAffinity, 0f, 3f);
        public float ForestEdgeAffinity => Mathf.Clamp(forestEdgeAffinity, 0f, 3f);

        public void Configure(
            string name,
            bool isEnabled,
            GameObject[] categoryPrefabs,
            float density,
            float minDistance,
            Vector2 slopeRange,
            Vector2 heightRange,
            Vector2 scaleRange,
            bool randomRotation,
            bool warnForMissingLod,
            bool requireLod,
            float drawDistance,
            float placementAttemptsMultiplier)
        {
            categoryName = string.IsNullOrWhiteSpace(name) ? "Props" : name;
            enabled = isEnabled;
            prefabs = categoryPrefabs ?? Array.Empty<GameObject>();
            densityPer10kSqm = Mathf.Max(0f, density);
            minDistanceBetweenInstances = Mathf.Max(0f, minDistance);
            allowedSlopeRange = slopeRange;
            allowedHeightRange = heightRange;
            randomScaleRange = scaleRange;
            randomYRotation = randomRotation;
            warnIfMissingLodGroup = warnForMissingLod;
            expectLodGroup = requireLod;
            maxDrawDistance = Mathf.Max(0f, drawDistance);
            attemptsMultiplier = Mathf.Clamp(placementAttemptsMultiplier, 1f, 20f);
            role = GuessRole(categoryName);
            ConfigureDefaultBiomeForRole(role);
        }

        public void ConfigureRoleAndBiome(
            ProceduralPropRole propRole,
            bool clusterPlacement,
            float threshold,
            float noiseScale,
            float clusterWeight,
            float slopeWeight,
            float shoreWeight,
            float forestEdgeWeight)
        {
            role = propRole;
            useClusterPlacement = clusterPlacement;
            clusterThreshold = Mathf.Clamp01(threshold);
            clusterNoiseScale = Mathf.Max(1f, noiseScale);
            clusterStrength = Mathf.Clamp(clusterWeight, 0f, 4f);
            slopeAffinity = Mathf.Clamp(slopeWeight, 0f, 3f);
            shoreAffinity = Mathf.Clamp(shoreWeight, 0f, 3f);
            forestEdgeAffinity = Mathf.Clamp(forestEdgeWeight, 0f, 3f);
        }

        private void ConfigureDefaultBiomeForRole(ProceduralPropRole propRole)
        {
            switch (propRole)
            {
                case ProceduralPropRole.Tree:
                case ProceduralPropRole.ForestCoreTrees:
                    ConfigureRoleAndBiome(propRole, true, 0.50f, 260f, 1.45f, 0.05f, 0.05f, 0f);
                    break;
                case ProceduralPropRole.ForestAccentTrees:
                    ConfigureRoleAndBiome(propRole, true, 0.62f, 135f, 2.7f, 0.02f, 0.02f, 0.2f);
                    break;
                case ProceduralPropRole.Bushes:
                    ConfigureRoleAndBiome(propRole, true, 0.48f, 165f, 1.25f, 0.05f, 0.18f, 1.2f);
                    break;
                case ProceduralPropRole.GroundGrass:
                    ConfigureRoleAndBiome(propRole, true, 0.36f, 95f, 1.45f, 0.03f, 0.12f, 0.8f);
                    break;
                case ProceduralPropRole.GroundPlants:
                    ConfigureRoleAndBiome(propRole, true, 0.42f, 115f, 1.35f, 0.03f, 0.25f, 0.9f);
                    break;
                case ProceduralPropRole.Rock:
                case ProceduralPropRole.RocksSmallMedium:
                    ConfigureRoleAndBiome(propRole, true, 0.56f, 180f, 1.1f, 1.25f, 0.85f, 0.35f);
                    break;
                case ProceduralPropRole.RocksLarge:
                    ConfigureRoleAndBiome(propRole, true, 0.60f, 185f, 1.3f, 1.75f, 0.95f, 0.2f);
                    break;
                case ProceduralPropRole.Cliff:
                    ConfigureRoleAndBiome(propRole, true, 0.58f, 160f, 1.25f, 2.25f, 0.45f, 0f);
                    break;
                case ProceduralPropRole.ShorePlants:
                    ConfigureRoleAndBiome(propRole, true, 0.40f, 115f, 1.35f, 0.03f, 2.35f, 0.1f);
                    break;
                case ProceduralPropRole.Log:
                    ConfigureRoleAndBiome(propRole, true, 0.48f, 230f, 1.05f, 0.05f, 0.15f, 1.25f);
                    break;
                default:
                    ConfigureRoleAndBiome(propRole, true, 0.46f, 150f, 0.9f, 0.05f, 0.15f, 0.75f);
                    break;
            }
        }

        private static ProceduralPropRole GuessRole(string name)
        {
            var key = string.IsNullOrWhiteSpace(name) ? string.Empty : name.ToLowerInvariant();
            if (key.Contains("forest core"))
            {
                return ProceduralPropRole.ForestCoreTrees;
            }

            if (key.Contains("forest accent") || key.Contains("magical"))
            {
                return ProceduralPropRole.ForestAccentTrees;
            }

            if (key.Contains("shore"))
            {
                return ProceduralPropRole.ShorePlants;
            }

            if (key.Contains("bush"))
            {
                return ProceduralPropRole.Bushes;
            }

            if (key.Contains("ground grass") || key.Contains("grass"))
            {
                return ProceduralPropRole.GroundGrass;
            }

            if (key.Contains("ground plant") || key.Contains("flower") || key.Contains("meadow"))
            {
                return ProceduralPropRole.GroundPlants;
            }

            if (key.Contains("large rock") || key.Contains("big rock") || key.Contains("large stone") || key.Contains("big stone"))
            {
                return ProceduralPropRole.RocksLarge;
            }

            if (key.Contains("small") || key.Contains("medium") || key.Contains("stones"))
            {
                return ProceduralPropRole.RocksSmallMedium;
            }

            if (key.Contains("tree") || key.Contains("forest"))
            {
                return ProceduralPropRole.Tree;
            }

            if (key.Contains("cliff"))
            {
                return ProceduralPropRole.Cliff;
            }

            if (key.Contains("rock") || key.Contains("stone"))
            {
                return ProceduralPropRole.Rock;
            }

            if (key.Contains("log") || key.Contains("branch") || key.Contains("stump"))
            {
                return ProceduralPropRole.Log;
            }

            return ProceduralPropRole.GroundCover;
        }

        private static Vector2 NormalizeRange(Vector2 range, float minClamp, float maxClamp)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), minClamp, maxClamp);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), minClamp, maxClamp);
            return new Vector2(min, max);
        }
    }
}
