using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Persistence
{
    public sealed class WorldFeaturePersistenceApplier
    {
        public void Apply(GenerationContext context)
        {
            WorldGenerationLayerData.Attach(context);
        }
    }
}
