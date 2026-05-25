using System.Collections.Generic;
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
                children[i].Execute(context);
            }
        }
    }
}
