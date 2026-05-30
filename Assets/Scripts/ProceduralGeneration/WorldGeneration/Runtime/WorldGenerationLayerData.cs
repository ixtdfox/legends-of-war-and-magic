using System;
using System.IO;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Persistence;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime
{
    [DisallowMultipleComponent]
    public sealed class WorldGenerationLayerData : MonoBehaviour
    {
        [SerializeField] private int seed;
        [SerializeField] private string generatedJson;
        [SerializeField] private string savedJsonPath;

        public int Seed => seed;
        public string GeneratedJson => generatedJson;
        public string SavedJsonPath => savedJsonPath;

        public static WorldGenerationLayerData Attach(GenerationContext context)
        {
            if (context?.GeneratedRoot == null || context.WorldLayers == null)
            {
                return null;
            }

            var data = context.GeneratedRoot.gameObject.GetComponent<WorldGenerationLayerData>();
            if (data == null)
            {
                data = context.GeneratedRoot.gameObject.AddComponent<WorldGenerationLayerData>();
            }

            data.seed = context.Seed;
            data.generatedJson = JsonUtility.ToJson(GeneratedWorldFeatureSaveMapper.ToDto(context.WorldLayers, context.Seed), true);
            data.savedJsonPath = SaveJson(context.Seed, data.generatedJson);
            return data;
        }

        private static string SaveJson(int seed, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                var directory = Path.Combine(Application.persistentDataPath, "GeneratedLocations");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"location_world_layers_{seed}.json");
                File.WriteAllText(path, json);
                return path;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"Failed to save generated world layer data: {exception.Message}");
                return string.Empty;
            }
        }
    }
}
