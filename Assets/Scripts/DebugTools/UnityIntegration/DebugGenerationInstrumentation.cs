using System.Collections.Generic;
using LegendsOfWarAndMagic.Generator.World;
using LegendsOfWarAndMagic.ProceduralGeneration;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.UnityIntegration
{
    public static class DebugGenerationInstrumentation
    {
        public static object BuildMapRequestSnapshot(MapGenerationRequest request, int seed)
        {
            var safeRequest = request ?? MapGenerationRequest.CreateDefault();
            return new
            {
                safeRequest.MapSize,
                safeRequest.LandType,
                safeRequest.WaterAmount,
                safeRequest.Relief,
                safeRequest.PropDensity,
                safeRequest.TreeDensity,
                safeRequest.GrassSaturation,
                safeRequest.GrassHighDetailDistance,
                safeRequest.GrassDrawDistance,
                safeRequest.SeedText,
                seed,
                debugEnabled = safeRequest.DebugEnabled
            };
        }

        public static object BuildWorldGenerationConfigSnapshot(WorldGenerationConfig config)
        {
            if (config == null)
            {
                return null;
            }

            return new
            {
                config.Seed,
                config.MinRegions,
                config.MaxRegions,
                config.PlayableLocationCount,
                config.ForcedShape,
                config.MapSampleWidth,
                config.MapSampleHeight,
                config.GeneratorVersion,
                config.DebugEnabled,
                snapshot = config.BuildSnapshot()
            };
        }

        public static object BuildLocationSettingsSnapshot(ProceduralLocationSettings settings, int seed)
        {
            if (settings == null)
            {
                return null;
            }

            return new
            {
                name = settings.name,
                seed,
                world = new
                {
                    settings.WorldWidth,
                    settings.WorldLength,
                    bounds = settings.GetWorldBounds()
                },
                terrain = new
                {
                    settings.HeightmapResolution,
                    settings.TerrainHeight,
                    settings.NoiseScale,
                    settings.Octaves,
                    settings.Persistence,
                    settings.Lacunarity,
                    settings.HeightMultiplier,
                    settings.LandShape,
                    settings.RidgeIntensity,
                    settings.ValleyIntensity,
                    settings.CliffIntensity,
                    settings.TerraceStrength,
                    settings.MicroReliefStrength,
                    settings.UseEdgeFalloff,
                    settings.EdgeFalloffStart,
                    settings.EdgeFalloffStrength
                },
                chunks = new
                {
                    settings.TerrainChunkStreamingEnabled,
                    settings.TerrainChunkSize,
                    settings.TerrainChunkHeightmapResolution,
                    settings.TerrainChunkLoadRadius,
                    settings.TerrainChunkUnloadBuffer,
                    settings.TerrainChunkInitialLoadRadius,
                    settings.TerrainChunkBootstrapPreloadRadius,
                    settings.TerrainChunkMaxBuildsPerFrame,
                    settings.TerrainChunkMaxUnloadsPerFrame,
                    settings.TerrainChunkLodPixelErrors
                },
                water = new
                {
                    settings.WaterEnabled,
                    settings.WaterLevel,
                    settings.WaterPlanePadding,
                    settings.WaterColor
                },
                details = new
                {
                    settings.TerrainDetailsEnabled,
                    settings.TerrainDetailDensityMultiplier,
                    settings.TerrainDetailResolution,
                    settings.TerrainDetailResolutionPerPatch
                },
                props = new
                {
                    settings.EnablePropPlacement,
                    categoryCount = settings.PropCategories?.Count ?? 0
                },
                grass = settings.GpuGrassSettings != null
                    ? new
                    {
                        settings.GpuGrassSettings.Enabled,
                        settings.GpuGrassSettings.NearDistance,
                        settings.GpuGrassSettings.MidDistance,
                        settings.GpuGrassSettings.FarVisualDistance,
                        settings.GpuGrassSettings.ClusterSize,
                        settings.GpuGrassSettings.PlacementSpacing,
                        settings.GpuGrassSettings.MaxVisibleNearInstances,
                        settings.GpuGrassSettings.MaxVisibleMidInstances,
                        settings.GpuGrassSettings.MaxVisibleGrassTriangles,
                        settings.GpuGrassSettings.DensityScale,
                        settings.GpuGrassSettings.MidDensityMultiplier,
                        settings.GpuGrassSettings.TerrainDetailDensityScale,
                        settings.GpuGrassSettings.NoiseOctaves,
                        settings.GpuGrassSettings.NoisePersistence,
                        settings.GpuGrassSettings.NoiseLacunarity
                    }
                    : null,
                generators = new
                {
                    locationGenerator = typeof(ProceduralLocationGenerator).FullName,
                    terrainSampler = typeof(ProceduralTerrainSampler).FullName
                }
            };
        }

        public static object BuildGeneratorSummary(ProceduralLocationGenerator generator)
        {
            if (generator == null)
            {
                return null;
            }

            var layers = generator.GeneratedWorldLayers;
            var terrainVertexCount = 0;
            var terrainTriangleCount = 0;
            var terrains = generator.GeneratedContentRoot != null
                ? generator.GeneratedContentRoot.GetComponentsInChildren<Terrain>(true)
                : Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var data = terrains[i] != null ? terrains[i].terrainData : null;
                if (data == null)
                {
                    continue;
                }

                var resolution = data.heightmapResolution;
                terrainVertexCount += resolution * resolution;
                terrainTriangleCount += Mathf.Max(0, (resolution - 1) * (resolution - 1) * 2);
            }

            return new
            {
                generator.LastUsedSeed,
                generator.LastGenerationSummary,
                terrainCount = terrains.Length,
                terrainVertexCount,
                terrainTriangleCount,
                loadedTerrainChunks = generator.TerrainChunkStreamer != null ? generator.TerrainChunkStreamer.LoadedChunkCount : 0,
                loadedPropChunks = generator.PropChunkStreamer != null ? generator.PropChunkStreamer.LoadedChunkCount : 0,
                settlements = layers?.Settlements?.Count ?? 0,
                pointsOfInterest = layers?.PointsOfInterest?.Count ?? 0,
                roads = layers?.RoadNetwork?.Segments?.Count ?? 0,
                roadNodes = layers?.RoadNetwork?.Nodes?.Count ?? 0,
                masks = new
                {
                    zones = layers?.Masks?.Zones?.Count ?? 0,
                    paths = layers?.Masks?.Paths?.Count ?? 0
                },
                spawnedByCategory = ExtractSpawnedCategories(generator.GeneratedContentRoot)
            };
        }

        public static void RecordGenerationCounters(ProceduralLocationGenerator generator)
        {
            var session = Core.DebugSessionManager.Current;
            if (session == null || generator == null)
            {
                return;
            }

            var layers = generator.GeneratedWorldLayers;
            session.Counters.Set("terrain.loadedChunks", generator.TerrainChunkStreamer != null ? generator.TerrainChunkStreamer.LoadedChunkCount : 0);
            session.Counters.Set("props.loadedChunks", generator.PropChunkStreamer != null ? generator.PropChunkStreamer.LoadedChunkCount : 0);
            session.Counters.Set("settlements.count", layers?.Settlements?.Count ?? 0);
            session.Counters.Set("poi.count", layers?.PointsOfInterest?.Count ?? 0);
            session.Counters.Set("roads.segments", layers?.RoadNetwork?.Segments?.Count ?? 0);
            session.Counters.Set("roads.nodes", layers?.RoadNetwork?.Nodes?.Count ?? 0);
            session.Counters.Set("masks.zones", layers?.Masks?.Zones?.Count ?? 0);
            session.Counters.Set("masks.paths", layers?.Masks?.Paths?.Count ?? 0);
        }

        private static object ExtractSpawnedCategories(Transform root)
        {
            if (root == null)
            {
                return new Dictionary<string, int>();
            }

            var counts = new Dictionary<string, int>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var name = transforms[i].name;
                var key = "other";
                if (name.Contains("Terrain")) key = "terrain";
                else if (name.Contains("Grass")) key = "grass";
                else if (name.Contains("Tree")) key = "trees";
                else if (name.Contains("Rock")) key = "rocks";
                else if (name.Contains("Road")) key = "roads";
                else if (name.Contains("Settlement")) key = "settlements";
                counts.TryGetValue(key, out var current);
                counts[key] = current + 1;
            }

            return counts;
        }
    }
}
