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
            Clear(context.GeneratedRoot);
        }

        public static void Clear(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child);
                    continue;
                }
#endif
                Object.Destroy(child);
            }
        }
    }
}
