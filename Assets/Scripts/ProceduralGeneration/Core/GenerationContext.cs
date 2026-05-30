using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Core
{
    /// <summary>
    /// Shared runtime data for all procedural generation steps.
    /// </summary>
    public sealed class GenerationContext
    {
        public GenerationContext(ProceduralLocationSettings settings, int seed, Transform generatedRoot)
        {
            Settings = settings;
            Seed = seed;
            GeneratedRoot = generatedRoot;
            WorldBounds = settings.GetWorldBounds();
        }

        public ProceduralLocationSettings Settings { get; }
        public int Seed { get; }
        public Bounds WorldBounds { get; }
        public Transform GeneratedRoot { get; }
        public Terrain GeneratedTerrain { get; set; }
        public IProceduralTerrainSampler TerrainSampler { get; set; }
        public MonoBehaviour TerrainChunkStreamer { get; set; }
        public MonoBehaviour PropChunkStreamer { get; set; }
        public GameObject GeneratedWater { get; set; }
        public bool HasPropExclusion { get; set; }
        public Vector3 PropExclusionCenter { get; set; }
        public float PropExclusionRadius { get; set; }
        public WorldGenerationLayers WorldLayers { get; set; }
        public WorldGenerationMaskSet WorldMasks { get; set; }
        public IReadOnlyList<Terrain> GeneratedTerrains => generatedTerrains;

        public IReadOnlyDictionary<string, int> SpawnedByCategory => spawnedByCategory;
        public IReadOnlyDictionary<string, int> RejectedByReason => rejectedByReason;
        public IReadOnlyDictionary<string, DetailLayerSummary> TerrainDetailSummaries => terrainDetailSummaries;

        public void RecordSpawn(string category, int count = 1)
        {
            if (count <= 0)
            {
                return;
            }

            var safeCategory = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category;
            if (!spawnedByCategory.TryAdd(safeCategory, count))
            {
                spawnedByCategory[safeCategory] += count;
            }
        }

        public void RecordRejected(string category, string reason, int count = 1)
        {
            if (count <= 0)
            {
                return;
            }

            var safeCategory = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category;
            var safeReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason;
            var key = $"{safeCategory}/{safeReason}";
            if (!rejectedByReason.TryAdd(key, count))
            {
                rejectedByReason[key] += count;
            }
        }

        public void RecordTerrainDetailLayer(string detailName, int occupiedCells, int totalDensity)
        {
            if (string.IsNullOrWhiteSpace(detailName))
            {
                detailName = "Terrain Details";
            }

            terrainDetailSummaries[detailName] = new DetailLayerSummary(occupiedCells, totalDensity);
        }

        public void AddGeneratedTerrain(Terrain terrain)
        {
            if (terrain == null || generatedTerrains.Contains(terrain))
            {
                return;
            }

            generatedTerrains.Add(terrain);
            if (GeneratedTerrain == null)
            {
                GeneratedTerrain = terrain;
            }
        }

        private readonly Dictionary<string, int> spawnedByCategory = new();
        private readonly Dictionary<string, int> rejectedByReason = new();
        private readonly Dictionary<string, DetailLayerSummary> terrainDetailSummaries = new();
        private readonly List<Terrain> generatedTerrains = new();

        public readonly struct DetailLayerSummary
        {
            public DetailLayerSummary(int occupiedCells, int totalDensity)
            {
                OccupiedCells = occupiedCells;
                TotalDensity = totalDensity;
            }

            public int OccupiedCells { get; }
            public int TotalDensity { get; }
        }
    }
}
