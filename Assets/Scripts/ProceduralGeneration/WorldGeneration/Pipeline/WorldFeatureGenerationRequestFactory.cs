using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public sealed class WorldFeatureGenerationRequestFactory
    {
        public WorldFeatureGenerationRequest Create(GenerationContext context)
        {
            return new WorldFeatureGenerationRequest(context.Settings, context.TerrainSampler, context.Seed);
        }
    }
}
