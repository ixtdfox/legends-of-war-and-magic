using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Debug
{
    public sealed class WorldFeatureDebugApplier
    {
        public void Apply(GenerationContext context)
        {
            WorldGenerationDebugRoot.Ensure(context);
        }
    }
}
