using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Pipeline
{
    /// <summary>
    /// A single unit of work in the procedural generation pipeline.
    /// </summary>
    public interface IGenerationStep
    {
        void Execute(GenerationContext context);
    }
}
