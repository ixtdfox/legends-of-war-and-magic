using LegendsOfWarAndMagic.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != SceneNames.OutdoorsScene)
            {
                return;
            }

            if (Object.FindFirstObjectByType<ProceduralLocationGenerator>() != null)
            {
                return;
            }

            var generatorObject = new GameObject("ProceduralLocationGenerator");
            generatorObject.AddComponent<ProceduralLocationGenerator>();
        }
    }
}
