using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Debug;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Persistence;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Presentation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Steps
{
    public sealed class WorldFeatureGenerationStep : IGenerationStep
    {
        private readonly WorldFeatureGenerationRequestFactory requestFactory;
        private readonly WorldFeatureGenerationPipeline pipeline;
        private readonly WorldFeatureTerrainApplier terrainApplier;
        private readonly WorldFeaturePresentationApplier presentationApplier;
        private readonly WorldFeaturePersistenceApplier persistenceApplier;
        private readonly WorldFeatureDebugApplier debugApplier;

        public WorldFeatureGenerationStep()
            : this(
                new WorldFeatureGenerationRequestFactory(),
                new WorldFeatureGenerationPipeline(null, null, null),
                new WorldFeatureTerrainApplier(),
                new WorldFeaturePresentationApplier(),
                new WorldFeaturePersistenceApplier(),
                new WorldFeatureDebugApplier())
        {
        }

        public WorldFeatureGenerationStep(
            WorldFeatureGenerationRequestFactory requestFactory,
            WorldFeatureGenerationPipeline pipeline,
            WorldFeatureTerrainApplier terrainApplier,
            WorldFeaturePresentationApplier presentationApplier,
            WorldFeaturePersistenceApplier persistenceApplier,
            WorldFeatureDebugApplier debugApplier)
        {
            this.requestFactory = requestFactory;
            this.pipeline = pipeline;
            this.terrainApplier = terrainApplier;
            this.presentationApplier = presentationApplier;
            this.persistenceApplier = persistenceApplier;
            this.debugApplier = debugApplier;
        }

        public void Execute(GenerationContext context)
        {
            if (context?.Settings == null || context.TerrainSampler == null)
            {
                return;
            }

            var result = pipeline.Generate(requestFactory.Create(context));
            context.WorldLayers = result.Layers;
            context.WorldMasks = result.Layers.Masks;

            terrainApplier.Apply(context);
            presentationApplier.Apply(context);
            debugApplier.Apply(context);
            persistenceApplier.Apply(context);

            context.RecordSpawn("Settlements", result.Layers.Settlements.Count);
            context.RecordSpawn("POI", result.Layers.PointsOfInterest.Count);
            context.RecordSpawn("RoadSegments", result.Layers.RoadNetwork.Segments.Count);
        }
    }
}
