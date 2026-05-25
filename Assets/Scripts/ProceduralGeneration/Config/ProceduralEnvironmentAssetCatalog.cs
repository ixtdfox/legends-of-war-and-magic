using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    public enum TerrainSurfaceRole
    {
        Shore,
        Ground,
        Rock,
        Highland
    }

    public enum ProceduralPropRole
    {
        Tree,
        Rock,
        Cliff,
        GroundCover,
        Log,
        ForestCoreTrees,
        ForestAccentTrees,
        Bushes,
        GroundGrass,
        GroundPlants,
        RocksSmallMedium,
        RocksLarge,
        ShorePlants
    }

    public enum TerrainDetailRole
    {
        Grass,
        Flowers,
        LowPlants
    }

    [CreateAssetMenu(
        fileName = "ProceduralEnvironmentAssetCatalog",
        menuName = "Legends of War and Magic/Procedural Generation/Environment Asset Catalog",
        order = 1)]
    public sealed class ProceduralEnvironmentAssetCatalog : ScriptableObject
    {
        [SerializeField] private TerrainSurfaceDefinition[] terrainSurfaces = Array.Empty<TerrainSurfaceDefinition>();
        [SerializeField] private ProceduralPropCategoryDefinition[] propCategories = Array.Empty<ProceduralPropCategoryDefinition>();
        [SerializeField] private TerrainDetailDefinition[] terrainDetails = Array.Empty<TerrainDetailDefinition>();

        public IReadOnlyList<TerrainSurfaceDefinition> TerrainSurfaces => terrainSurfaces;
        public IReadOnlyList<ProceduralPropCategoryDefinition> PropCategories => propCategories;
        public IReadOnlyList<TerrainDetailDefinition> TerrainDetails => terrainDetails;

        public TerrainSurfaceDefinition FindTerrainSurface(TerrainSurfaceRole role)
        {
            for (var i = 0; i < terrainSurfaces.Length; i++)
            {
                if (terrainSurfaces[i] != null && terrainSurfaces[i].Role == role)
                {
                    return terrainSurfaces[i];
                }
            }

            return null;
        }

        public bool HasPropPrefabs()
        {
            for (var i = 0; i < propCategories.Length; i++)
            {
                if (propCategories[i] != null && propCategories[i].HasPrefabs)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasTerrainDetails()
        {
            for (var i = 0; i < terrainDetails.Length; i++)
            {
                if (terrainDetails[i] != null && terrainDetails[i].HasPrototype)
                {
                    return true;
                }
            }

            return false;
        }

        public void Configure(
            IEnumerable<TerrainSurfaceDefinition> surfaces,
            IEnumerable<ProceduralPropCategoryDefinition> categories,
            IEnumerable<TerrainDetailDefinition> details = null)
        {
            terrainSurfaces = surfaces == null
                ? Array.Empty<TerrainSurfaceDefinition>()
                : new List<TerrainSurfaceDefinition>(surfaces).ToArray();

            propCategories = categories == null
                ? Array.Empty<ProceduralPropCategoryDefinition>()
                : new List<ProceduralPropCategoryDefinition>(categories).ToArray();

            terrainDetails = details == null
                ? Array.Empty<TerrainDetailDefinition>()
                : new List<TerrainDetailDefinition>(details).ToArray();
        }
    }

    [Serializable]
    public sealed class TerrainDetailDefinition
    {
        [SerializeField] private TerrainDetailRole role = TerrainDetailRole.Grass;
        [SerializeField] private string detailName = "Grass";
        [SerializeField] private GameObject prototypePrefab;
        [SerializeField] private Texture2D prototypeTexture;
        [SerializeField] private DetailRenderMode renderMode = DetailRenderMode.VertexLit;
        [SerializeField] private bool usePrototypeMesh = true;
        [SerializeField] private bool useInstancing = true;

        [Min(0f)]
        [SerializeField] private float baseDensity = 8f;

        [SerializeField] private Vector2 widthRange = new(0.45f, 0.85f);
        [SerializeField] private Vector2 heightRange = new(0.45f, 0.9f);
        [SerializeField] private Color healthyColor = new(0.18f, 0.42f, 0.14f, 1f);
        [SerializeField] private Color dryColor = new(0.42f, 0.36f, 0.17f, 1f);

        [Min(0.01f)]
        [SerializeField] private float noiseSpread = 0.45f;

        public TerrainDetailRole Role => role;
        public string DetailName => string.IsNullOrWhiteSpace(detailName) ? role.ToString() : detailName;
        public GameObject PrototypePrefab => prototypePrefab;
        public Texture2D PrototypeTexture => prototypeTexture;
        public DetailRenderMode RenderMode => renderMode;
        public bool UsePrototypeMesh => usePrototypeMesh;
        public bool UseInstancing => useInstancing;
        public float BaseDensity => Mathf.Max(0f, baseDensity);
        public Vector2 WidthRange => NormalizeRange(widthRange, 0.01f, 12f);
        public Vector2 HeightRange => NormalizeRange(heightRange, 0.01f, 12f);
        public Color HealthyColor => healthyColor;
        public Color DryColor => dryColor;
        public float NoiseSpread => Mathf.Max(0.01f, noiseSpread);
        public bool HasPrototype => prototypePrefab != null || prototypeTexture != null;

        public void ConfigureMesh(
            TerrainDetailRole detailRole,
            string name,
            GameObject prefab,
            float density,
            Vector2 width,
            Vector2 height,
            Color healthy,
            Color dry,
            float spread)
        {
            role = detailRole;
            detailName = string.IsNullOrWhiteSpace(name) ? detailRole.ToString() : name;
            prototypePrefab = prefab;
            prototypeTexture = null;
            usePrototypeMesh = true;
            useInstancing = true;
            renderMode = DetailRenderMode.VertexLit;
            baseDensity = Mathf.Max(0f, density);
            widthRange = width;
            heightRange = height;
            healthyColor = healthy;
            dryColor = dry;
            noiseSpread = Mathf.Max(0.01f, spread);
        }

        public void ConfigureTexture(
            TerrainDetailRole detailRole,
            string name,
            Texture2D texture,
            float density,
            Vector2 width,
            Vector2 height,
            Color healthy,
            Color dry,
            float spread)
        {
            role = detailRole;
            detailName = string.IsNullOrWhiteSpace(name) ? detailRole.ToString() : name;
            prototypePrefab = null;
            prototypeTexture = texture;
            usePrototypeMesh = false;
            useInstancing = false;
            renderMode = DetailRenderMode.GrassBillboard;
            baseDensity = Mathf.Max(0f, density);
            widthRange = width;
            heightRange = height;
            healthyColor = healthy;
            dryColor = dry;
            noiseSpread = Mathf.Max(0.01f, spread);
        }

        private static Vector2 NormalizeRange(Vector2 range, float minClamp, float maxClamp)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), minClamp, maxClamp);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), minClamp, maxClamp);
            return new Vector2(min, max);
        }
    }

    [Serializable]
    public sealed class TerrainSurfaceDefinition
    {
        [SerializeField] private TerrainSurfaceRole role;
        [SerializeField] private string surfaceName = "Surface";
        [SerializeField] private TerrainLayer terrainLayer;
        [SerializeField] private Color fallbackColor = Color.white;
        [Min(1f)]
        [SerializeField] private float tileSize = 10f;

        public TerrainSurfaceRole Role => role;
        public string SurfaceName => string.IsNullOrWhiteSpace(surfaceName) ? role.ToString() : surfaceName;
        public TerrainLayer TerrainLayer => terrainLayer;
        public Color FallbackColor => fallbackColor;
        public float TileSize => Mathf.Max(1f, tileSize);

        public void Configure(TerrainSurfaceRole surfaceRole, string name, TerrainLayer layer, Color color, float tileWorldSize)
        {
            role = surfaceRole;
            surfaceName = string.IsNullOrWhiteSpace(name) ? surfaceRole.ToString() : name;
            terrainLayer = layer;
            fallbackColor = color;
            tileSize = Mathf.Max(1f, tileWorldSize);
        }
    }

    [Serializable]
    public sealed class ProceduralPropCategoryDefinition
    {
        [SerializeField] private ProceduralPropRole role = ProceduralPropRole.Tree;
        [SerializeField] private string categoryName = "Props";
        [SerializeField] private bool enabled = true;
        [SerializeField] private GameObject[] prefabs = Array.Empty<GameObject>();

        [Tooltip("Instances per 10,000 square meters before UI density multiplier is applied.")]
        [Min(0f)]
        [SerializeField] private float baseDensityPer10kSqm = 1f;

        [Min(0f)]
        [SerializeField] private float minDistanceBetweenInstances = 8f;

        [SerializeField] private Vector2 allowedSlopeRange = new(0f, 35f);
        [SerializeField] private Vector2 randomScaleRange = new(0.85f, 1.2f);
        [SerializeField] private bool randomYRotation = true;
        [Min(0f)]
        [SerializeField] private float maxDrawDistance = 450f;
        [Range(1f, 20f)]
        [SerializeField] private float attemptsMultiplier = 8f;

        public ProceduralPropRole Role => role;
        public string CategoryName => string.IsNullOrWhiteSpace(categoryName) ? role.ToString() : categoryName;
        public bool Enabled => enabled;
        public GameObject[] Prefabs => prefabs;
        public bool HasPrefabs => prefabs != null && prefabs.Length > 0;

        public PropCategoryPlacementSettings CreatePlacementSettings(
            float densityMultiplier,
            float waterLevel,
            float terrainHeight)
        {
            var settings = new PropCategoryPlacementSettings();
            var heightRange = ResolveHeightRange(role, waterLevel, terrainHeight);
            settings.Configure(
                CategoryName,
                enabled,
                prefabs,
                baseDensityPer10kSqm * Mathf.Max(0f, densityMultiplier),
                minDistanceBetweenInstances,
                allowedSlopeRange,
                heightRange,
                randomScaleRange,
                randomYRotation,
                false,
                false,
                maxDrawDistance,
                attemptsMultiplier);
            ApplyBiomeDefaults(settings, role, densityMultiplier);
            return settings;
        }

        public void Configure(
            ProceduralPropRole propRole,
            string name,
            bool isEnabled,
            GameObject[] categoryPrefabs,
            float density,
            float minDistance,
            Vector2 slopeRange,
            Vector2 scaleRange,
            bool randomRotation,
            float drawDistance,
            float placementAttemptsMultiplier)
        {
            role = propRole;
            categoryName = string.IsNullOrWhiteSpace(name) ? propRole.ToString() : name;
            enabled = isEnabled;
            prefabs = categoryPrefabs ?? Array.Empty<GameObject>();
            baseDensityPer10kSqm = Mathf.Max(0f, density);
            minDistanceBetweenInstances = Mathf.Max(0f, minDistance);
            allowedSlopeRange = slopeRange;
            randomScaleRange = scaleRange;
            randomYRotation = randomRotation;
            maxDrawDistance = Mathf.Max(0f, drawDistance);
            attemptsMultiplier = Mathf.Clamp(placementAttemptsMultiplier, 1f, 20f);
        }

        private static void ApplyBiomeDefaults(
            PropCategoryPlacementSettings settings,
            ProceduralPropRole propRole,
            float densityMultiplier)
        {
            var highDensity = densityMultiplier > 2.5f;
            switch (propRole)
            {
                case ProceduralPropRole.Tree:
                case ProceduralPropRole.ForestCoreTrees:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.42f : 0.48f, 270f, 1.8f, 0.05f, 0.05f, 0f);
                    break;
                case ProceduralPropRole.ForestAccentTrees:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.54f : 0.66f, 135f, 2.7f, 0.02f, 0.02f, 0.15f);
                    break;
                case ProceduralPropRole.Bushes:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.36f : 0.48f, 165f, 1.45f, 0.04f, 0.15f, 1.25f);
                    break;
                case ProceduralPropRole.GroundGrass:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.24f : 0.36f, 95f, 1.7f, 0.02f, 0.12f, 0.8f);
                    break;
                case ProceduralPropRole.GroundPlants:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.28f : 0.42f, 110f, 1.65f, 0.02f, 0.25f, 0.95f);
                    break;
                case ProceduralPropRole.Rock:
                case ProceduralPropRole.RocksSmallMedium:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.48f : 0.54f, 175f, 1.25f, 1.55f, 1.1f, 0.25f);
                    break;
                case ProceduralPropRole.RocksLarge:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.54f : 0.60f, 185f, 1.45f, 1.95f, 1.25f, 0.15f);
                    break;
                case ProceduralPropRole.Cliff:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.48f : 0.55f, 145f, 1.45f, 2.45f, 0.35f, 0f);
                    break;
                case ProceduralPropRole.ShorePlants:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.30f : 0.40f, 115f, 1.35f, 0.02f, 2.6f, 0.1f);
                    break;
                case ProceduralPropRole.Log:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.42f : 0.50f, 210f, 1.1f, 0.05f, 0.15f, 1.3f);
                    break;
                default:
                    settings.ConfigureRoleAndBiome(propRole, true, highDensity ? 0.38f : 0.46f, 155f, 1f, 0.05f, 0.12f, 0.9f);
                    break;
            }
        }

        private static Vector2 ResolveHeightRange(ProceduralPropRole propRole, float waterLevel, float terrainHeight)
        {
            switch (propRole)
            {
                case ProceduralPropRole.ShorePlants:
                    return new Vector2(waterLevel + 0.05f, Mathf.Min(terrainHeight, waterLevel + Mathf.Max(5.5f, terrainHeight * 0.08f)));
                case ProceduralPropRole.Cliff:
                    return new Vector2(waterLevel + 0.4f, terrainHeight);
                case ProceduralPropRole.Rock:
                case ProceduralPropRole.RocksSmallMedium:
                case ProceduralPropRole.RocksLarge:
                    return new Vector2(waterLevel + 0.25f, terrainHeight);
                default:
                    return new Vector2(waterLevel + 0.75f, terrainHeight);
            }
        }
    }
}
