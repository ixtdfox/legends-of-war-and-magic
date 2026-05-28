using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Populates Unity Terrain details for grass and low ground cover without spawning thousands of GameObjects.
    /// </summary>
    public sealed class TerrainDetailGenerationStep : IGenerationStep
    {
        private const int MaxDensityPerCell = 28;

        public void Execute(GenerationContext context)
        {
            var settings = context.Settings;
            if (settings == null || !settings.TerrainDetailsEnabled || context.TerrainChunkStreamer != null)
            {
                return;
            }

            var terrains = context.GeneratedTerrains;
            if (terrains.Count == 0 && context.GeneratedTerrain != null)
            {
                ApplyToTerrain(settings, context.GeneratedTerrain, context.Seed, context.RecordTerrainDetailLayer);
                return;
            }

            for (var i = 0; i < terrains.Count; i++)
            {
                Action<string, int, int> recorder = null;
                if (i == 0)
                {
                    recorder = context.RecordTerrainDetailLayer;
                }

                ApplyToTerrain(settings, terrains[i], context.Seed, recorder);
            }
        }

        public static void ApplyToTerrain(
            ProceduralLocationSettings settings,
            Terrain terrain,
            int seed,
            Action<string, int, int> recordDetailLayer)
        {
            if (settings == null || terrain == null || terrain.terrainData == null || !settings.TerrainDetailsEnabled)
            {
                return;
            }

            var detailDefinitions = BuildDetailDefinitions(settings);
            if (detailDefinitions.Count == 0)
            {
                Debug.LogWarning("Terrain detail generation skipped: no terrain detail prototypes could be resolved.");
                return;
            }

            var terrainData = terrain.terrainData;
            var resolution = ResolveDetailResolution(settings, terrainData);
            var patchResolution = ResolveDetailResolutionPerPatch(settings, resolution);
            terrainData.SetDetailResolution(resolution, patchResolution);

            var prototypes = new DetailPrototype[detailDefinitions.Count];
            for (var i = 0; i < detailDefinitions.Count; i++)
            {
                prototypes[i] = detailDefinitions[i].CreatePrototype(seed + i * 7919);
            }

            terrainData.detailPrototypes = prototypes;
            var gpuGrassSettings = settings.GpuGrassSettings;
            terrain.detailObjectDistance = gpuGrassSettings.Enabled
                ? gpuGrassSettings.TerrainDetailFallbackDistance
                : Mathf.Max(terrain.detailObjectDistance, 180f);
            terrain.detailObjectDensity = Mathf.Max(terrain.detailObjectDensity, gpuGrassSettings.Enabled ? 0.75f : 1f);

            for (var layer = 0; layer < detailDefinitions.Count; layer++)
            {
                FillDetailLayer(settings, seed, terrain, terrainData, detailDefinitions[layer], layer, resolution, recordDetailLayer);
            }
        }

        private static int ResolveDetailResolution(ProceduralLocationSettings settings, TerrainData terrainData)
        {
            var requested = Mathf.Clamp(settings.TerrainDetailResolution, 32, 2048);
            if (!settings.TerrainChunkStreamingEnabled || terrainData == null)
            {
                return requested;
            }

            var xScale = terrainData.size.x / Mathf.Max(1f, settings.WorldWidth);
            var zScale = terrainData.size.z / Mathf.Max(1f, settings.WorldLength);
            var chunkScale = Mathf.Max(xScale, zScale);
            return Mathf.Clamp(Mathf.RoundToInt(requested * chunkScale), 32, 512);
        }

        private static int ResolveDetailResolutionPerPatch(ProceduralLocationSettings settings, int resolution)
        {
            var requested = settings != null ? settings.TerrainDetailResolutionPerPatch : 16;
            var patch = Mathf.Clamp(requested, 8, Mathf.Max(8, resolution));
            if (resolution % patch == 0)
            {
                return patch;
            }

            if (resolution % 32 == 0 && patch >= 32)
            {
                return 32;
            }

            if (resolution % 16 == 0)
            {
                return 16;
            }

            return 8;
        }

        private static List<RuntimeDetailDefinition> BuildDetailDefinitions(ProceduralLocationSettings settings)
        {
            var definitions = new List<RuntimeDetailDefinition>();

            var catalog = settings.AssetCatalog;
            if (catalog != null && catalog.HasTerrainDetails())
            {
                for (var i = 0; i < catalog.TerrainDetails.Count; i++)
                {
                    var detail = catalog.TerrainDetails[i];
                    if (detail == null || !detail.HasPrototype)
                    {
                        continue;
                    }

                    if (settings.GpuGrassSettings.Enabled && detail.Role == TerrainDetailRole.Grass)
                    {
                        continue;
                    }

                    definitions.Add(RuntimeDetailDefinition.FromCatalog(detail));
                }
            }

            if (definitions.Count == 0)
            {
                Debug.LogWarning(
                    "Terrain detail generation skipped: environment catalog has no Fristy terrain detail prototypes. " +
                    "Run Tools/Legends of War and Magic/Procedural Generation/Organize Fristy Nature Resources.");
            }

            return definitions;
        }

        private static void FillDetailLayer(
            ProceduralLocationSettings settings,
            int seed,
            Terrain terrain,
            TerrainData terrainData,
            RuntimeDetailDefinition definition,
            int layer,
            int resolution,
            Action<string, int, int> recordDetailLayer)
        {
            var values = new int[resolution, resolution];
            var occupiedCells = 0;
            var totalDensity = 0;
            var waterLevel01 = settings.WaterEnabled && settings.TerrainHeight > 0f
                ? Mathf.Clamp01(settings.WaterLevel / settings.TerrainHeight)
                : -1f;
            var seedA = (seed & 0xFFFF) * 0.00071f + layer * 17.31f;
            var seedB = ((seed >> 8) & 0xFFFF) * 0.00067f + layer * 29.17f;

            for (var y = 0; y < resolution; y++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var nx = (x + 0.5f) / resolution;
                    var nz = (y + 0.5f) / resolution;
                    var worldX = terrain.transform.position.x + nx * terrainData.size.x;
                    var worldZ = terrain.transform.position.z + nz * terrainData.size.z;
                    var worldNx = Mathf.InverseLerp(-settings.WorldWidth * 0.5f, settings.WorldWidth * 0.5f, worldX);
                    var worldNz = Mathf.InverseLerp(-settings.WorldLength * 0.5f, settings.WorldLength * 0.5f, worldZ);
                    var normalizedHeight = terrainData.GetInterpolatedHeight(nx, nz) / terrainData.size.y;
                    var aboveWater = normalizedHeight - waterLevel01;
                    if (aboveWater < 0.035f)
                    {
                        continue;
                    }

                    var slope = Vector3.Angle(terrainData.GetInterpolatedNormal(nx, nz), Vector3.up);
                    var mask = BuildPlacementMask(definition.Role, slope, normalizedHeight, aboveWater, worldNx, worldNz, seedA, seedB);
                    if (mask <= 0.01f)
                    {
                        continue;
                    }

                    var detailNoise = Mathf.PerlinNoise(worldNx * 92f + seedB, worldNz * 92f + seedA);
                    var stochastic = Hash01(seed, x, y, layer);
                    var density = definition.BaseDensity * settings.TerrainDetailDensityMultiplier * mask * Mathf.Lerp(0.45f, 1.2f, detailNoise);
                    var whole = Mathf.FloorToInt(density);
                    var fractional = density - whole;
                    if (stochastic < fractional)
                    {
                        whole++;
                    }

                    var clamped = Mathf.Clamp(whole, 0, MaxDensityPerCell);
                    if (clamped <= 0)
                    {
                        continue;
                    }

                    values[y, x] = clamped;
                    occupiedCells++;
                    totalDensity += clamped;
                }
            }

            terrainData.SetDetailLayer(0, 0, layer, values);
            recordDetailLayer?.Invoke(definition.Name, occupiedCells, totalDensity);
        }

        private static float BuildPlacementMask(
            TerrainDetailRole role,
            float slope,
            float normalizedHeight,
            float aboveWater,
            float nx,
            float nz,
            float seedA,
            float seedB)
        {
            var meadowNoise = Mathf.PerlinNoise(nx * 12.5f + seedA, nz * 12.5f + seedB);
            var patchNoise = Mathf.PerlinNoise(nx * 31.0f + seedB, nz * 31.0f + seedA);
            var slopeMask = 1f - SmoothRange(18f, 42f, slope);
            var shoreFade = SmoothRange(0.02f, 0.09f, aboveWater);
            var highFade = 1f - SmoothRange(0.78f, 0.96f, normalizedHeight);
            var baseMask = Mathf.Clamp01(slopeMask * shoreFade * highFade);

            switch (role)
            {
                case TerrainDetailRole.Flowers:
                    return baseMask * SmoothRange(0.45f, 0.82f, meadowNoise) * Mathf.Lerp(0.25f, 1f, patchNoise);
                case TerrainDetailRole.LowPlants:
                    return baseMask * Mathf.Lerp(0.45f, 1f, patchNoise) * (0.75f + SmoothRange(0.52f, 0.86f, meadowNoise) * 0.45f);
                default:
                    return baseMask * Mathf.Lerp(0.65f, 1.25f, patchNoise) * Mathf.Lerp(0.7f, 1.2f, meadowNoise);
            }
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }

        private static float Hash01(int seed, int x, int y, int layer)
        {
            unchecked
            {
                var hash = (uint)seed;
                hash ^= (uint)(x * 374761393);
                hash ^= (uint)(y * 668265263);
                hash ^= (uint)layer * 2246822519u;
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFF) / 16777215f;
            }
        }

        private readonly struct RuntimeDetailDefinition
        {
            private RuntimeDetailDefinition(
                TerrainDetailRole role,
                string name,
                GameObject prefab,
                Texture2D texture,
                DetailRenderMode renderMode,
                bool usePrototypeMesh,
                bool useInstancing,
                float baseDensity,
                Vector2 widthRange,
                Vector2 heightRange,
                Color healthyColor,
                Color dryColor,
                float noiseSpread)
            {
                Role = role;
                Name = name;
                Prefab = prefab;
                Texture = texture;
                RenderMode = renderMode;
                UsePrototypeMesh = usePrototypeMesh;
                UseInstancing = useInstancing;
                BaseDensity = baseDensity;
                WidthRange = widthRange;
                HeightRange = heightRange;
                HealthyColor = healthyColor;
                DryColor = dryColor;
                NoiseSpread = noiseSpread;
            }

            public TerrainDetailRole Role { get; }
            public string Name { get; }
            public GameObject Prefab { get; }
            public Texture2D Texture { get; }
            public DetailRenderMode RenderMode { get; }
            public bool UsePrototypeMesh { get; }
            public bool UseInstancing { get; }
            public float BaseDensity { get; }
            public Vector2 WidthRange { get; }
            public Vector2 HeightRange { get; }
            public Color HealthyColor { get; }
            public Color DryColor { get; }
            public float NoiseSpread { get; }

            public static RuntimeDetailDefinition FromCatalog(TerrainDetailDefinition definition)
            {
                return new RuntimeDetailDefinition(
                    definition.Role,
                    definition.DetailName,
                    definition.PrototypePrefab,
                    definition.PrototypeTexture,
                    definition.RenderMode,
                    definition.UsePrototypeMesh,
                    definition.UseInstancing,
                    definition.BaseDensity,
                    definition.WidthRange,
                    definition.HeightRange,
                    definition.HealthyColor,
                    definition.DryColor,
                    definition.NoiseSpread);
            }

            public DetailPrototype CreatePrototype(int noiseSeed)
            {
                var prototype = new DetailPrototype
                {
                    prototype = UsePrototypeMesh ? Prefab : null,
                    prototypeTexture = UsePrototypeMesh ? null : Texture,
                    renderMode = RenderMode,
                    usePrototypeMesh = UsePrototypeMesh,
                    useInstancing = UseInstancing,
                    minWidth = WidthRange.x,
                    maxWidth = WidthRange.y,
                    minHeight = HeightRange.x,
                    maxHeight = HeightRange.y,
                    healthyColor = HealthyColor,
                    dryColor = DryColor,
                    noiseSpread = NoiseSpread,
                    noiseSeed = noiseSeed,
                    alignToGround = 0.65f,
                    positionJitter = 0.72f,
                    density = 1f,
                    useDensityScaling = true
                };

                return prototype;
            }
        }
    }
}
