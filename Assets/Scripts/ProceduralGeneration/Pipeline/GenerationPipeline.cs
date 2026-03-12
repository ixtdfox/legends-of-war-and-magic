using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Pipeline
{
    /// <summary>
    /// Ordered execution pipeline for procedural generation.
    /// </summary>
    public sealed class GenerationPipeline
    {
        private readonly IReadOnlyList<IGenerationStep> _steps;

        public GenerationPipeline(IReadOnlyList<IGenerationStep> steps)
        {
            _steps = steps;
        }

        public void Run(GenerationContext context)
        {
            for (var i = 0; i < _steps.Count; i++)
            {
                _steps[i].Execute(context);
            }
        }
    }
}
