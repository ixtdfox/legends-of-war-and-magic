using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Core
{
    /// <summary>
    /// Shared runtime data for all procedural generation steps.
    /// </summary>
    public sealed class GenerationContext
    {
        public GenerationContext(ProceduralLocationSettings settings, int seed, Transform generatedRoot)
        {
            Settings = settings;
            Seed = seed;
            GeneratedRoot = generatedRoot;
            WorldBounds = settings.GetWorldBounds();
        }

        public ProceduralLocationSettings Settings { get; }
        public int Seed { get; }
        public Bounds WorldBounds { get; }
        public Transform GeneratedRoot { get; }
    }
}
