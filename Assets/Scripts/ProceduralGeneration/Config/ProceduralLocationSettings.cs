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
        }

        [Serializable]
        public sealed class SeedGenerationSettings
        {
            [SerializeField] private SeedMode seedMode = SeedMode.Random;
            [SerializeField] private int fixedSeed = 12345;

            public SeedMode SeedMode => seedMode;
            public int FixedSeed => fixedSeed;
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
            public bool UseEdgeFalloff => useEdgeFalloff;
            public float EdgeFalloffStart => edgeFalloffStart;
            public float EdgeFalloffStrength => edgeFalloffStrength;
        }

        [Serializable]
        public sealed class PropPlacementGenerationSettings
        {
            [SerializeField] private bool enablePropPlacement = true;
            [SerializeField] private List<PropCategoryPlacementSettings> propCategories = new();

            public bool EnablePropPlacement => enablePropPlacement;
            public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => propCategories;
        }

        [Header("Global")]
        [SerializeField] private GlobalGenerationSettings global = new();

        [Header("Seed")]
        [SerializeField] private SeedGenerationSettings seed = new();

        [Header("Terrain")]
        [SerializeField] private TerrainGenerationSettings terrain = new();

        [Header("Props")]
        [SerializeField] private PropPlacementGenerationSettings props = new();

        public GlobalGenerationSettings Global => global;
        public SeedGenerationSettings Seed => seed;
        public TerrainGenerationSettings Terrain => terrain;
        public PropPlacementGenerationSettings Props => props;

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
        public bool UseEdgeFalloff => terrain.UseEdgeFalloff;
        public float EdgeFalloffStart => terrain.EdgeFalloffStart;
        public float EdgeFalloffStrength => terrain.EdgeFalloffStrength;
        public bool EnablePropPlacement => props.EnablePropPlacement;
        public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => props.PropCategories;

        public Bounds GetWorldBounds()
        {
            var center = Vector3.zero;
            var size = new Vector3(global.WorldWidth, 0f, global.WorldLength);
            return new Bounds(center, size);
        }
    }
}
