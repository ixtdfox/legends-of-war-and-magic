using System.Collections.Generic;
using LegendsOfWarAndMagic.DebugTools.Core;
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
                var step = _steps[i];
                using (DebugSessionManager.Profiler.Scope($"GenerationStep.{ResolveStepName(step)}", new
                {
                    index = i,
                    type = step.GetType().FullName,
                    seed = context?.Seed,
                    settings = context?.Settings != null ? context.Settings.name : string.Empty
                }))
                {
                    step.Execute(context);
                }
            }
        }

        private static string ResolveStepName(IGenerationStep step)
        {
            return step is CompositeGenerationStep composite ? composite.Name : step.GetType().Name;
        }
    }
}
