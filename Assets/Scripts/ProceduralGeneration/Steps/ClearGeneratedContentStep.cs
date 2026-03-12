using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Removes previously generated objects so regeneration produces a clean result.
    /// </summary>
    public sealed class ClearGeneratedContentStep : IGenerationStep
    {
        public void Execute(GenerationContext context)
        {
            var root = context.GeneratedRoot;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
