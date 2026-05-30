using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public sealed class GeneratedWorldFeatures
    {
        public GeneratedWorldFeatures(
            IReadOnlyList<GeneratedSettlement> settlements,
            IReadOnlyList<GeneratedPointOfInterest> pointsOfInterest,
            GeneratedRoadNetwork roadNetwork,
            WorldGenerationMaskSet masks,
            IReadOnlyDictionary<string, double> stageTimings)
        {
            Settlements = settlements ?? Array.Empty<GeneratedSettlement>();
            PointsOfInterest = pointsOfInterest ?? Array.Empty<GeneratedPointOfInterest>();
            RoadNetwork = roadNetwork ?? new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), Array.Empty<GeneratedRoadSegment>());
            Masks = masks ?? new WorldGenerationMaskSet();
            StageTimings = stageTimings ?? new Dictionary<string, double>();
        }

        public IReadOnlyList<GeneratedSettlement> Settlements { get; }
        public IReadOnlyList<GeneratedPointOfInterest> PointsOfInterest { get; }
        public GeneratedRoadNetwork RoadNetwork { get; }
        public WorldGenerationMaskSet Masks { get; }
        public IReadOnlyDictionary<string, double> StageTimings { get; }
    }
}
