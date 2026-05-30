using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Steps;
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
        public Terrain GeneratedTerrain { get; private set; }
        public IProceduralTerrainSampler GeneratedTerrainSampler { get; private set; }
        public GeneratedTerrainChunkStreamer TerrainChunkStreamer { get; private set; }
        public GeneratedPropChunkStreamer PropChunkStreamer { get; private set; }
        public Transform GeneratedContentRoot => generatedContentRoot;
        public ProceduralLocationSettings CurrentSettings => settings;
        public WorldGenerationLayers GeneratedWorldLayers { get; private set; }
        public string LastGenerationSummary { get; private set; }
        public bool GenerateOnStart
        {
            get => generateOnStart;
            set => generateOnStart = value;
        }

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

        public void GenerateFromSettings(ProceduralLocationSettings runtimeSettings, int seed)
        {
            if (runtimeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: runtime settings are null.", this);
                return;
            }

            settings = runtimeSettings;
            GenerateInternal(runtimeSettings, seed);
        }

        public IEnumerator GenerateFromSettingsRoutine(
            ProceduralLocationSettings runtimeSettings,
            int seed,
            Action<string, float> progress)
        {
            if (runtimeSettings == null)
            {
                Debug.LogError("Procedural generation aborted: runtime settings are null.", this);
                progress?.Invoke("Ошибка настроек генерации", 1f);
                yield break;
            }

            settings = runtimeSettings;
            yield return GenerateInternalRoutine(runtimeSettings, seed, progress);
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
            GeneratedWorldLayers = null;
        }

        private void GenerateInternal(ProceduralLocationSettings activeSettings, int seed)
        {
            EnsureGeneratedRoot();

            UnityEngine.Random.InitState(seed);

            var context = new GenerationContext(activeSettings, seed, generatedContentRoot);
            BuildPipeline().Run(context);
            FinalizeGeneration(context);

            if (verboseDebugLogging)
            {
                Debug.Log(LastGenerationSummary, this);
            }
        }

        private IEnumerator GenerateInternalRoutine(
            ProceduralLocationSettings activeSettings,
            int seed,
            Action<string, float> progress)
        {
            EnsureGeneratedRoot();

            UnityEngine.Random.InitState(seed);

            var context = new GenerationContext(activeSettings, seed, generatedContentRoot);

            progress?.Invoke("Очищаем предыдущую локацию...", 0.02f);
            new ClearGeneratedContentStep().Execute(context);
            yield return null;

            progress?.Invoke("Создаём карту высот и первый чанк...", 0.12f);
            new TerrainGenerationStep().Execute(context);
            yield return null;

            progress?.Invoke("Планируем поселения, дороги и места интереса...", 0.20f);
            new WorldFeatureGenerationStep().Execute(context);
            yield return null;

            progress?.Invoke("Готовим детали поверхности...", 0.24f);
            new TerrainDetailGenerationStep().Execute(context);
            yield return null;

            progress?.Invoke("Готовим траву...", 0.30f);
            new GpuGrassGenerationStep().Execute(context);
            yield return null;

            progress?.Invoke("Добавляем воду...", 0.34f);
            new WaterGenerationStep().Execute(context);
            yield return null;

            var propStep = new PropPlacementStep();
            var propRoutine = propStep.ExecuteRoutine(
                context,
                (message, propProgress) =>
                {
                    progress?.Invoke(message, Mathf.Lerp(0.36f, 0.92f, Mathf.Clamp01(propProgress)));
                });
            while (propRoutine.MoveNext())
            {
                yield return null;
            }

            progress?.Invoke("Ставим границы локации...", 0.96f);
            new CreateBoundaryMarkersStep().Execute(context);
            yield return null;

            FinalizeGeneration(context);
            progress?.Invoke("Локация сгенерирована", 1f);

            if (verboseDebugLogging)
            {
                Debug.Log(LastGenerationSummary, this);
            }
        }

        private void FinalizeGeneration(GenerationContext context)
        {
            lastUsedSeed = context.Seed;
            GeneratedTerrain = context.GeneratedTerrain;
            GeneratedTerrainSampler = context.TerrainSampler;
            TerrainChunkStreamer = context.TerrainChunkStreamer as GeneratedTerrainChunkStreamer;
            PropChunkStreamer = context.PropChunkStreamer as GeneratedPropChunkStreamer;
            GeneratedWorldLayers = context.WorldLayers;
            LastGenerationSummary = BuildGenerationSummary(context);
        }

        private string BuildGenerationSummary(GenerationContext context)
        {
            var settingsSummary = $"Procedural location generated. Preset={context.Settings.name}, Seed={context.Seed}, TerrainSize={context.Settings.WorldWidth}x{context.Settings.WorldLength}m Height={context.Settings.TerrainHeight}m, LandShape={context.Settings.LandShape}, WaterLevel={context.Settings.WaterLevel:0.##}m";
            if (context.TerrainChunkStreamer is GeneratedTerrainChunkStreamer streamer)
            {
                settingsSummary += $", ChunkedTerrain={context.Settings.TerrainChunkSize:0.#}m x {streamer.LoadedTerrains.Count} loaded";
            }

            if (context.PropChunkStreamer is GeneratedPropChunkStreamer propStreamer)
            {
                settingsSummary += $", ChunkedProps={propStreamer.LoadedChunkCount} loaded";
            }

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

            if (context.TerrainDetailSummaries.Count > 0)
            {
                builder.Append(", DetailLayers=");
                first = true;
                foreach (var pair in context.TerrainDetailSummaries)
                {
                    if (!first)
                    {
                        builder.Append(" | ");
                    }

                    builder.Append(pair.Key);
                    builder.Append(":cells=");
                    builder.Append(pair.Value.OccupiedCells);
                    builder.Append(",density=");
                    builder.Append(pair.Value.TotalDensity);
                    first = false;
                }
            }

            if (context.RejectedByReason.Count > 0)
            {
                builder.Append(", Rejections=");
                first = true;
                foreach (var pair in context.RejectedByReason)
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
                new CompositeGenerationStep(
                    "Environment",
                    new IGenerationStep[]
                    {
                        new TerrainGenerationStep(),
                        new WorldFeatureGenerationStep(),
                        new TerrainDetailGenerationStep(),
                        new GpuGrassGenerationStep(),
                        new WaterGenerationStep(),
                        new CompositeGenerationStep(
                            "Environment Props",
                            new IGenerationStep[]
                            {
                                new PropPlacementStep(),
                                new CreateBoundaryMarkersStep()
                            })
                    })
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
