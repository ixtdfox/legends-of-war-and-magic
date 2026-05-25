using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    /// <summary>
    /// Determines how the generator seed is chosen.
    /// </summary>
    public enum SeedMode
    {
        Random,
        Fixed
    }

    /// <summary>
    /// High-level landmass shaping mode used by player-facing map presets.
    /// </summary>
    public enum TerrainLandShape
    {
        Mainland,
        Islands,
        Archipelago
    }

    /// <summary>
    /// Reusable preset asset for procedural location generation.
    /// Create multiple assets to support different location styles.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProceduralLocationSettings",
        menuName = "Legends of War and Magic/Procedural Generation/Location Settings",
        order = 0)]
    public sealed class ProceduralLocationSettings : ScriptableObject
    {
        [Serializable]
        public sealed class GlobalGenerationSettings
        {
            [Header("World Bounds (meters)")]
            [Min(1f)]
            [SerializeField] private float worldWidth = 2000f;

            [Min(1f)]
            [SerializeField] private float worldLength = 2000f;

            public float WorldWidth => worldWidth;
            public float WorldLength => worldLength;

            public void Configure(float width, float length)
            {
                worldWidth = Mathf.Max(1f, width);
                worldLength = Mathf.Max(1f, length);
            }
        }

        [Serializable]
        public sealed class SeedGenerationSettings
        {
            [SerializeField] private SeedMode seedMode = SeedMode.Random;
            [SerializeField] private int fixedSeed = 12345;

            public SeedMode SeedMode => seedMode;
            public int FixedSeed => fixedSeed;

            public void Configure(SeedMode mode, int seed)
            {
                seedMode = mode;
                fixedSeed = seed;
            }
        }

        [Serializable]
        public sealed class TerrainGenerationSettings
        {
            [Min(33)]
            [SerializeField] private int heightmapResolution = 513;

            [Min(1f)]
            [SerializeField] private float terrainHeight = 220f;

            [Min(0.001f)]
            [SerializeField] private float noiseScale = 320f;

            [Min(1)]
            [SerializeField] private int octaves = 5;

            [Range(0f, 1f)]
            [SerializeField] private float persistence = 0.45f;

            [Min(1f)]
            [SerializeField] private float lacunarity = 2f;

            [Min(0f)]
            [SerializeField] private float heightMultiplier = 1f;

            [SerializeField] private TerrainLandShape landShape = TerrainLandShape.Mainland;

            [Header("Edge Smoothing")]
            [SerializeField] private bool useEdgeFalloff = true;

            [Range(0f, 1f)]
            [SerializeField] private float edgeFalloffStart = 0.82f;

            [Range(0.1f, 10f)]
            [SerializeField] private float edgeFalloffStrength = 2.2f;

            public int HeightmapResolution => heightmapResolution;
            public float TerrainHeight => terrainHeight;
            public float NoiseScale => noiseScale;
            public int Octaves => octaves;
            public float Persistence => persistence;
            public float Lacunarity => lacunarity;
            public float HeightMultiplier => heightMultiplier;
            public TerrainLandShape LandShape => landShape;
            public bool UseEdgeFalloff => useEdgeFalloff;
            public float EdgeFalloffStart => edgeFalloffStart;
            public float EdgeFalloffStrength => edgeFalloffStrength;

            public void Configure(
                int resolution,
                float height,
                float scale,
                int octaveCount,
                float persistenceValue,
                float lacunarityValue,
                float multiplier,
                TerrainLandShape shape,
                bool edgeFalloff,
                float falloffStart,
                float falloffStrength)
            {
                heightmapResolution = Mathf.Max(33, resolution);
                terrainHeight = Mathf.Max(1f, height);
                noiseScale = Mathf.Max(0.001f, scale);
                octaves = Mathf.Max(1, octaveCount);
                persistence = Mathf.Clamp01(persistenceValue);
                lacunarity = Mathf.Max(1f, lacunarityValue);
                heightMultiplier = Mathf.Max(0f, multiplier);
                landShape = shape;
                useEdgeFalloff = edgeFalloff;
                edgeFalloffStart = Mathf.Clamp01(falloffStart);
                edgeFalloffStrength = Mathf.Clamp(falloffStrength, 0.1f, 10f);
            }
        }

        [Serializable]
        public sealed class PropPlacementGenerationSettings
        {
            [SerializeField] private bool enablePropPlacement = true;
            [SerializeField] private List<PropCategoryPlacementSettings> propCategories = new();

            public bool EnablePropPlacement => enablePropPlacement;
            public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => propCategories;

            public void Configure(bool enabled, IEnumerable<PropCategoryPlacementSettings> categories)
            {
                enablePropPlacement = enabled;
                propCategories = categories == null
                    ? new List<PropCategoryPlacementSettings>()
                    : new List<PropCategoryPlacementSettings>(categories);
            }
        }

        [Serializable]
        public sealed class WaterGenerationSettings
        {
            [SerializeField] private bool enabled = true;

            [Tooltip("World-space height of the water plane.")]
            [Min(0f)]
            [SerializeField] private float waterLevel = 18f;

            [Tooltip("Extra water plane padding beyond generated terrain bounds.")]
            [Min(0f)]
            [SerializeField] private float planePadding = 20f;

            [SerializeField] private Color waterColor = new(0.1f, 0.36f, 0.58f, 0.76f);

            public bool Enabled => enabled;
            public float WaterLevel => waterLevel;
            public float PlanePadding => planePadding;
            public Color WaterColor => waterColor;

            public void Configure(bool isEnabled, float level, float padding, Color color)
            {
                enabled = isEnabled;
                waterLevel = Mathf.Max(0f, level);
                planePadding = Mathf.Max(0f, padding);
                waterColor = color;
            }
        }

        [Header("Global")]
        [SerializeField] private GlobalGenerationSettings global = new();

        [Header("Seed")]
        [SerializeField] private SeedGenerationSettings seed = new();

        [Header("Terrain")]
        [SerializeField] private TerrainGenerationSettings terrain = new();

        [Header("Props")]
        [SerializeField] private PropPlacementGenerationSettings props = new();

        [Header("Water")]
        [SerializeField] private WaterGenerationSettings water = new();

        public GlobalGenerationSettings Global => global;
        public SeedGenerationSettings Seed => seed;
        public TerrainGenerationSettings Terrain => terrain;
        public PropPlacementGenerationSettings Props => props;
        public WaterGenerationSettings Water => water;

        public float WorldWidth => global.WorldWidth;
        public float WorldLength => global.WorldLength;
        public SeedMode SeedMode => seed.SeedMode;
        public int FixedSeed => seed.FixedSeed;
        public int HeightmapResolution => terrain.HeightmapResolution;
        public float TerrainHeight => terrain.TerrainHeight;
        public float NoiseScale => terrain.NoiseScale;
        public int Octaves => terrain.Octaves;
        public float Persistence => terrain.Persistence;
        public float Lacunarity => terrain.Lacunarity;
        public float HeightMultiplier => terrain.HeightMultiplier;
        public TerrainLandShape LandShape => terrain.LandShape;
        public bool UseEdgeFalloff => terrain.UseEdgeFalloff;
        public float EdgeFalloffStart => terrain.EdgeFalloffStart;
        public float EdgeFalloffStrength => terrain.EdgeFalloffStrength;
        public bool EnablePropPlacement => props.EnablePropPlacement;
        public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => props.PropCategories;
        public bool WaterEnabled => water.Enabled;
        public float WaterLevel => water.WaterLevel;
        public float WaterPlanePadding => water.PlanePadding;
        public Color WaterColor => water.WaterColor;

        public void ConfigureGlobal(float width, float length)
        {
            global.Configure(width, length);
        }

        public void ConfigureSeed(SeedMode mode, int seed)
        {
            this.seed.Configure(mode, seed);
        }

        public void ConfigureTerrain(
            int resolution,
            float height,
            float scale,
            int octaveCount,
            float persistenceValue,
            float lacunarityValue,
            float multiplier,
            TerrainLandShape shape,
            bool edgeFalloff,
            float falloffStart,
            float falloffStrength)
        {
            terrain.Configure(
                resolution,
                height,
                scale,
                octaveCount,
                persistenceValue,
                lacunarityValue,
                multiplier,
                shape,
                edgeFalloff,
                falloffStart,
                falloffStrength);
        }

        public void ConfigureProps(bool enabled, IEnumerable<PropCategoryPlacementSettings> categories)
        {
            props.Configure(enabled, categories);
        }

        public void ConfigureWater(bool enabled, float level, float padding, Color color)
        {
            water.Configure(enabled, level, padding, color);
        }

        public Bounds GetWorldBounds()
        {
            var center = Vector3.zero;
            var size = new Vector3(global.WorldWidth, 0f, global.WorldLength);
            return new Bounds(center, size);
        }
    }
}
