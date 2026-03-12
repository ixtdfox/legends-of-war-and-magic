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
        [Header("General")]
        [SerializeField] private bool generateOnStart = true;

        [SerializeField] private ProceduralLocationSettings settings = new();

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
            if (generatedContentRoot == null)
            {
                var root = new GameObject("GeneratedLocationRoot");
                root.transform.SetParent(transform, false);
                generatedContentRoot = root.transform;
            }

            var seed = GenerationSeedResolver.ResolveSeed(settings);
            Random.InitState(seed);

            var context = new GenerationContext(settings, seed, generatedContentRoot);
            BuildPipeline().Run(context);

            Debug.Log($"Procedural location generated. Seed={seed}, TerrainSize={settings.WorldWidth}x{settings.WorldLength}m Height={settings.TerrainHeight}m", this);
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
            if (settings == null)
            {
                return;
            }

            var bounds = settings.GetWorldBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 1f, bounds.size.z));
        }
    }
}
