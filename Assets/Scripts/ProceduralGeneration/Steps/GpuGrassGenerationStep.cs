using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    public sealed class GpuGrassGenerationStep : IGenerationStep
    {
        public void Execute(GenerationContext context)
        {
            var settings = context.Settings;
            if (settings == null)
            {
                return;
            }

            var grassSettings = settings.GpuGrassSettings;
            if (context.GeneratedTerrain == null || grassSettings == null || !grassSettings.Enabled)
            {
                return;
            }

            var grassObject = new GameObject("GeneratedGpuGrass");
            grassObject.transform.SetParent(context.GeneratedRoot, false);
            var renderer = grassObject.AddComponent<GeneratedGpuGrassRenderer>();
            renderer.Initialize(context.GeneratedTerrain, context.Seed, grassSettings, settings.WaterLevel);
            context.RecordSpawn("GpuGrass", renderer.ChunkCount);
        }
    }
}
