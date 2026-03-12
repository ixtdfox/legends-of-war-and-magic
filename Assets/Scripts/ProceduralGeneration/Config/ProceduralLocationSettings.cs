using System;
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

        [Header("Terrain Placeholder Settings")]
        [Min(2)]
        [SerializeField] private int terrainResolution = 256;

        [Min(1)]
        [SerializeField] private int terrainDensity = 1;

        public float WorldWidth => worldWidth;
        public float WorldLength => worldLength;
        public SeedMode SeedMode => seedMode;
        public int FixedSeed => fixedSeed;
        public int TerrainResolution => terrainResolution;
        public int TerrainDensity => terrainDensity;

        public Bounds GetWorldBounds()
        {
            // We center the world around the origin so systems can reason around (0,0,0).
            var center = Vector3.zero;
            var size = new Vector3(worldWidth, 0f, worldLength);
            return new Bounds(center, size);
        }
    }
}
