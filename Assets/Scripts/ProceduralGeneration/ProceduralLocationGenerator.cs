using System.Collections.Generic;
using System.Text;
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

        [Header("Debug & Iteration")]
        [SerializeField] private bool showBoundsGizmo = true;
        [SerializeField] private bool verboseDebugLogging = true;

        [Header("Output")]
        [SerializeField] private Transform generatedContentRoot;

        [SerializeField] private int lastUsedSeed;

        public int LastUsedSeed => lastUsedSeed;

        private void Start()
        {
            if (!generateOnStart)
            {
                return;
            }

            GenerateWithSettingsSeed();
        }

        [ContextMenu("Generate Location")]
        public void Generate()
        {
            GenerateWithSettingsSeed();
        }

        [ContextMenu("Generate/Regenerate (Settings Seed Mode)")]
        public void GenerateWithSettingsSeed()
        {
            var activeSettings = ResolveSettings();
            if (activeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: no settings asset is assigned and no default resource was found.", this);
                return;
            }

            GenerateInternal(activeSettings, GenerationSeedResolver.ResolveSeed(activeSettings));
        }

        [ContextMenu("Generate/Regenerate (Same Seed)")]
        public void RegenerateWithSameSeed()
        {
            var activeSettings = ResolveSettings();
            if (activeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: no settings asset is assigned and no default resource was found.", this);
                return;
            }

            var seedToUse = lastUsedSeed != 0 ? lastUsedSeed : GenerationSeedResolver.ResolveSeed(activeSettings);
            GenerateInternal(activeSettings, seedToUse);
        }

        [ContextMenu("Generate/Regenerate (New Random Seed)")]
        public void RegenerateWithRandomSeed()
        {
            var activeSettings = ResolveSettings();
            if (activeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: no settings asset is assigned and no default resource was found.", this);
                return;
            }

            GenerateInternal(activeSettings, GenerationSeedResolver.GenerateRandomSeed());
        }

        [ContextMenu("Clear Generated Content")]
        public void ClearGeneratedContent()
        {
            if (generatedContentRoot == null)
            {
                return;
            }

            ClearGeneratedContentStep.Clear(generatedContentRoot);
        }

        private void GenerateInternal(ProceduralLocationSettings activeSettings, int seed)
        {
            EnsureGeneratedRoot();

            Random.InitState(seed);

            var context = new GenerationContext(activeSettings, seed, generatedContentRoot);
            BuildPipeline().Run(context);
            lastUsedSeed = seed;

            if (verboseDebugLogging)
            {
                Debug.Log(BuildGenerationSummary(context), this);
            }
        }

        private string BuildGenerationSummary(GenerationContext context)
        {
            var settingsSummary = $"Procedural location generated. Preset={context.Settings.name}, Seed={context.Seed}, TerrainSize={context.Settings.WorldWidth}x{context.Settings.WorldLength}m Height={context.Settings.TerrainHeight}m";
            if (context.SpawnedByCategory.Count == 0)
            {
                return $"{settingsSummary}, SpawnedCategories=None";
            }

            var builder = new StringBuilder();
            builder.Append(settingsSummary);
            builder.Append(", SpawnCounts=");

            var first = true;
            foreach (var pair in context.SpawnedByCategory)
            {
                if (!first)
                {
                    builder.Append(" | ");
                }

                builder.Append(pair.Key);
                builder.Append(':');
                builder.Append(pair.Value);
                first = false;
            }

            return builder.ToString();
        }

        private void EnsureGeneratedRoot()
        {
            if (generatedContentRoot != null)
            {
                return;
            }

            var root = new GameObject("GeneratedLocationRoot");
            root.transform.SetParent(transform, false);
            generatedContentRoot = root.transform;
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
            if (activeSettings == null || !showBoundsGizmo)
            {
                return;
            }

            var bounds = activeSettings.GetWorldBounds();
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 1f, bounds.size.z));
        }
    }
}
