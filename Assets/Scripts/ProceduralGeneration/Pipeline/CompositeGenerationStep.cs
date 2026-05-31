using System.Collections.Generic;
using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Pipeline
{
    /// <summary>
    /// Composite node for grouping generation steps without flattening domain responsibilities.
    /// </summary>
    public sealed class CompositeGenerationStep : IGenerationStep
    {
        private readonly IReadOnlyList<IGenerationStep> children;

        public CompositeGenerationStep(string name, IReadOnlyList<IGenerationStep> childSteps)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Composite" : name;
            children = childSteps ?? new List<IGenerationStep>();
        }

        public string Name { get; }

        public void Execute(GenerationContext context)
        {
            for (var i = 0; i < children.Count; i++)
            {
                var step = children[i];
                using (DebugSessionManager.Profiler.Scope($"GenerationStep.{ResolveStepName(step)}", new
                {
                    parent = Name,
                    index = i,
                    type = step.GetType().FullName,
                    seed = context?.Seed
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
