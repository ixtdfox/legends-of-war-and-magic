using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration
{
    /// <summary>
    /// Main entry point for procedural location generation.
    /// Attach this to a scene GameObject to generate the location automatically on play.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralLocationGenerator : MonoBehaviour
    {
        private const string DefaultSettingsResourcePath = "ProceduralGeneration/DefaultProceduralLocationSettings";

        [Header("General")]
        [SerializeField] private bool generateOnStart = true;

        [Tooltip("Reusable ScriptableObject preset that controls all generation parameters.")]
        [SerializeField] private ProceduralLocationSettings settings;

        [Header("Output")]
        [SerializeField] private Transform generatedContentRoot;

        private void Start()
        {
            if (!generateOnStart)
            {
                return;
            }

            Generate();
        }

        [ContextMenu("Generate Location")]
        public void Generate()
        {
            var activeSettings = ResolveSettings();
            if (activeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: no settings asset is assigned and no default resource was found.", this);
                return;
            }

            if (generatedContentRoot == null)
            {
                var root = new GameObject("GeneratedLocationRoot");
                root.transform.SetParent(transform, false);
                generatedContentRoot = root.transform;
            }

            var seed = GenerationSeedResolver.ResolveSeed(activeSettings);
            Random.InitState(seed);

            var context = new GenerationContext(activeSettings, seed, generatedContentRoot);
            BuildPipeline().Run(context);

            Debug.Log($"Procedural location generated. Preset={activeSettings.name}, Seed={seed}, TerrainSize={activeSettings.WorldWidth}x{activeSettings.WorldLength}m Height={activeSettings.TerrainHeight}m", this);
        }

        private ProceduralLocationSettings ResolveSettings()
        {
            if (settings != null)
            {
                return settings;
            }

            // Keeps auto-generation working in scenes where the component wasn't configured yet.
            settings = Resources.Load<ProceduralLocationSettings>(DefaultSettingsResourcePath);
            return settings;
        }

        private static GenerationPipeline BuildPipeline()
        {
            var steps = new List<IGenerationStep>
            {
                new ClearGeneratedContentStep(),
                new TerrainGenerationStep(),
                new PropPlacementStep(),
                new CreateBoundaryMarkersStep()
            };

            return new GenerationPipeline(steps);
        }

        private void OnDrawGizmosSelected()
        {
            var activeSettings = ResolveSettings();
            if (activeSettings == null)
            {
                return;
            }

            var bounds = activeSettings.GetWorldBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 1f, bounds.size.z));
        }
    }
}
