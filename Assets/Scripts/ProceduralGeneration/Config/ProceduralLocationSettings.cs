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
    /// Inspector-exposed settings used by the procedural location generator.
    /// </summary>
    [Serializable]
    public class ProceduralLocationSettings
    {
        [Header("World Bounds (meters)")]
        [Min(1f)]
        [SerializeField] private float worldWidth = 2000f;

        [Min(1f)]
        [SerializeField] private float worldLength = 2000f;

        [Header("Seed")]
        [SerializeField] private SeedMode seedMode = SeedMode.Random;

        [SerializeField] private int fixedSeed = 12345;

        [Header("Terrain")]
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

        [Header("Prop Placement")]
        [SerializeField] private bool enablePropPlacement = true;

        [SerializeField] private List<PropCategoryPlacementSettings> propCategories = new();

        public float WorldWidth => worldWidth;
        public float WorldLength => worldLength;
        public SeedMode SeedMode => seedMode;
        public int FixedSeed => fixedSeed;
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
        public bool EnablePropPlacement => enablePropPlacement;
        public IReadOnlyList<PropCategoryPlacementSettings> PropCategories => propCategories;

        public Bounds GetWorldBounds()
        {
            // We center the world around the origin so systems can reason around (0,0,0).
            var center = Vector3.zero;
            var size = new Vector3(worldWidth, 0f, worldLength);
            return new Bounds(center, size);
        }
    }
}
