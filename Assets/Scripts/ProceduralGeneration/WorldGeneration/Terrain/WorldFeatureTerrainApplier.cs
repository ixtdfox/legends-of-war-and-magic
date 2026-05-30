using System.Diagnostics;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation;
using UnityTerrain = UnityEngine.Terrain;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain
{
    public sealed class WorldFeatureTerrainApplier
    {
        public void Apply(GenerationContext context)
        {
            if (context?.Settings == null || context.WorldLayers == null)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            if (context.TerrainChunkStreamer is GeneratedTerrainChunkStreamer chunkStreamer)
            {
                chunkStreamer.ApplyWorldGenerationLayers(context.WorldLayers);
            }
            else
            {
                ApplyTerrainAdaptation(context);
                RepaintTerrains(context);
            }

            if (context.WorldLayers.StageTimings is System.Collections.Generic.IDictionary<string, double> timings)
            {
                timings["Terrain adaptation"] = stopwatch.Elapsed.TotalMilliseconds;
            }
        }

        private static void ApplyTerrainAdaptation(GenerationContext context)
        {
            var terrainSampler = context.TerrainSampler;
            var settlementAdjustedSampler = new SettlementAdjustedTerrainSampler(
                terrainSampler,
                context.Settings,
                context.WorldLayers.Settlements);
            var roadCarvingContext = RoadTerrainCarvingContext.Build(
                context.Settings,
                settlementAdjustedSampler,
                context.Settings.Roads,
                context.WorldLayers.RoadNetwork);

            var terrains = context.GeneratedTerrains;
            for (var i = 0; i < terrains.Count; i++)
            {
                ApplyTerrainAdaptation(context, terrains[i], roadCarvingContext, terrainSampler);
            }
        }

        private static void ApplyTerrainAdaptation(
            GenerationContext context,
            UnityTerrain terrain,
            RoadTerrainCarvingContext roadCarvingContext,
            IProceduralTerrainSampler baseSampler)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            var data = terrain.terrainData;
            var resolution = data.heightmapResolution;
            var heights = data.GetHeights(0, 0, resolution, resolution);
            var terrainPosition = terrain.transform.position;
            SettlementTerrainCarver.Apply(
                heights,
                terrainPosition.x,
                terrainPosition.z,
                data.size.x,
                data.size.z,
                context.Settings,
                context.WorldLayers.Settlements,
                baseSampler);

            TerrainRoadCarver.Apply(
                heights,
                terrainPosition.x,
                terrainPosition.z,
                data.size.x,
                data.size.z,
                context.Settings,
                roadCarvingContext);

            data.SetHeights(0, 0, heights);
        }

        private static void RepaintTerrains(GenerationContext context)
        {
            var terrains = context.GeneratedTerrains;
            for (var i = 0; i < terrains.Count; i++)
            {
                GeneratedTerrainVisuals.Apply(
                    terrains[i],
                    context.Settings,
                    context.Seed,
                    context.WorldMasks,
                    context.WorldLayers.RoadNetwork);
            }
        }
    }
}
