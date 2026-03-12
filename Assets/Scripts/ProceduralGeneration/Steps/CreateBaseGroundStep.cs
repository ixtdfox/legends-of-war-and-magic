using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Creates a simple base plane that fills the generated world bounds.
    /// This is intentionally minimal so future terrain/biome layers can replace it.
    /// </summary>
    public sealed class CreateBaseGroundStep : IGenerationStep
    {
        private const float UnityPlaneSize = 10f;

        public void Execute(GenerationContext context)
        {
            var bounds = context.WorldBounds;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GeneratedGround";
            ground.transform.SetParent(context.GeneratedRoot, false);
            ground.transform.position = Vector3.zero;

            ground.transform.localScale = new Vector3(
                bounds.size.x / UnityPlaneSize,
                1f,
                bounds.size.z / UnityPlaneSize);

            // A subtle randomized tint helps visually confirm different seeds/runs.
            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                var color = new Color(
                    Random.Range(0.2f, 0.35f),
                    Random.Range(0.45f, 0.65f),
                    Random.Range(0.2f, 0.35f));
                renderer.material.color = color;
            }
        }
    }
}
