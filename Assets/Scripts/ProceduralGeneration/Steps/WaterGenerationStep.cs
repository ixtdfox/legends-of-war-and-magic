using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Adds a simple generated water plane at the configured world height.
    /// </summary>
    public sealed class WaterGenerationStep : IGenerationStep
    {
        public void Execute(GenerationContext context)
        {
            var settings = context.Settings;
            if (settings == null || !settings.WaterEnabled)
            {
                return;
            }

            var waterRoot = new GameObject("GeneratedWater").transform;
            waterRoot.SetParent(context.GeneratedRoot, false);

            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "WaterSurface";
            water.transform.SetParent(waterRoot, false);
            water.transform.position = new Vector3(0f, settings.WaterLevel, 0f);
            water.transform.localScale = new Vector3(
                (settings.WorldWidth + settings.WaterPlanePadding * 2f) / 10f,
                1f,
                (settings.WorldLength + settings.WaterPlanePadding * 2f) / 10f);

            var collider = water.GetComponent<Collider>();
            if (collider != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(collider);
                }
                else
#endif
                {
                    Object.Destroy(collider);
                }
            }

            water.GetComponent<Renderer>().sharedMaterial = CreateWaterMaterial(settings.WaterColor);
            context.GeneratedWater = water;
            context.RecordSpawn("Water", 1);
        }

        private static Material CreateWaterMaterial(Color color)
        {
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader)
            {
                name = "Generated Water Material",
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.86f);
            }

            return material;
        }
    }
}
