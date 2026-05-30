using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Presentation
{
    public sealed class WorldFeaturePresentationApplier
    {
        public void Apply(GenerationContext context)
        {
            WorldFeatureRuntimeSpawner.Spawn(context);
        }
    }
}
