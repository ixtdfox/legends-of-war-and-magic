using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Bootstrap
{
    /// <summary>
    /// Optional safety bootstrap: ensures a generator exists when a scene starts.
    /// If one is already present, this does nothing.
    /// </summary>
    public static class ProceduralLocationBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGeneratorExists()
        {
            if (Object.FindFirstObjectByType<ProceduralLocationGenerator>() != null)
            {
                return;
            }

            var generatorObject = new GameObject("ProceduralLocationGenerator");
            generatorObject.AddComponent<ProceduralLocationGenerator>();
        }
    }
}
