using System.Collections.Generic;
using System.Diagnostics;
using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public sealed class WorldFeatureGenerationPipeline
    {
        private readonly SettlementGenerator settlementGenerator;
        private readonly PointOfInterestGenerator pointOfInterestGenerator;
        private readonly RoadNetworkGenerator roadNetworkGenerator;

        public WorldFeatureGenerationPipeline(
            SettlementGenerator settlementGenerator,
            PointOfInterestGenerator pointOfInterestGenerator,
            RoadNetworkGenerator roadNetworkGenerator)
        {
            this.settlementGenerator = settlementGenerator ?? new SettlementGenerator();
            this.pointOfInterestGenerator = pointOfInterestGenerator ?? new PointOfInterestGenerator();
            this.roadNetworkGenerator = roadNetworkGenerator ?? new RoadNetworkGenerator();
        }

        public WorldFeatureGenerationResult Generate(WorldFeatureGenerationRequest request)
        {
            var timings = new Dictionary<string, double>();
            var masks = new WorldGenerationMaskSet(32f);
            var stopwatch = Stopwatch.StartNew();

            IReadOnlyList<GeneratedSettlement> settlements;
            using (DebugSessionManager.Profiler.Scope("WorldFeatureGeneration.Settlements", new { request.Seed }))
            {
                settlements = settlementGenerator.Generate(
                    request.Settings,
                    request.TerrainSampler,
                    request.Settings.Settlements,
                    masks,
                    request.Seed);
            }
            timings["Settlement generation"] = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            IReadOnlyList<GeneratedPointOfInterest> pointsOfInterest;
            using (DebugSessionManager.Profiler.Scope("WorldFeatureGeneration.PointsOfInterest", new
            {
                request.Seed,
                settlementCount = settlements.Count
            }))
            {
                pointsOfInterest = pointOfInterestGenerator.Generate(
                    request.Settings,
                    request.TerrainSampler,
                    request.Settings.PointsOfInterest,
                    settlements,
                    masks,
                    request.Seed);
            }
            timings["POI generation"] = stopwatch.Elapsed.TotalMilliseconds;

            stopwatch.Restart();
            GeneratedRoadNetwork roads;
            using (DebugSessionManager.Profiler.Scope("WorldFeatureGeneration.Roads", new
            {
                request.Seed,
                settlementCount = settlements.Count,
                poiCount = pointsOfInterest.Count
            }))
            {
                roads = roadNetworkGenerator.Generate(
                    request.Settings,
                    request.TerrainSampler,
                    request.Settings.Roads,
                    request.Settings.PointsOfInterest,
                    settlements,
                    pointsOfInterest,
                    masks,
                    request.Seed);
            }
            timings["Road generation"] = stopwatch.Elapsed.TotalMilliseconds;

            var layers = new WorldGenerationLayers(settlements, pointsOfInterest, roads, masks, timings);
            return new WorldFeatureGenerationResult(layers);
        }
    }
}
