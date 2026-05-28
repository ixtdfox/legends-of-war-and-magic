using System.Collections;
using LegendsOfWarAndMagic.Diagnostics;
using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Unity;
using LegendsOfWarAndMagic.Game.World.Presentation;
using LegendsOfWarAndMagic.Generator.Location;
using LegendsOfWarAndMagic.Game.Player;
using LegendsOfWarAndMagic.ProceduralGeneration;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;

namespace LegendsOfWarAndMagic.Game.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class GameSceneBootstrap : MonoBehaviour
    {
        private const float SafeWaterClearance = 1.8f;
        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.4f;
        private const float PlayerEyeHeight = 1.62f;

        private IEnumerator Start()
        {
            EnsureLighting();

            if (!GeneratedWorldSession.HasWorld)
            {
                GeneratedWorldRuntimeState.TryRestoreSession();
            }

            if (GeneratedWorldSession.HasWorld && GeneratedWorldSession.CurrentLocation != null)
            {
                yield return BootstrapGeneratedWorldLocation();
                yield break;
            }

            yield return BootstrapLegacySingleLocation();
        }

        private IEnumerator BootstrapGeneratedWorldLocation()
        {
            var world = GeneratedWorldSession.CurrentWorld;
            var location = GeneratedWorldSession.CurrentLocation;
            RuntimeLoadingOverlay.Show($"Загружаем «{location.Name.Value}»...", 0.08f);
            yield return null;

            RuntimeLoadingOverlay.SetProgress("Готовим настройки локации...", 0.18f);
            var settings = LocationTerrainSettingsFactory.Create(location);
            yield return null;

            RuntimeLoadingOverlay.SetProgress("Начинаем генерацию локации...", 0.28f);
            yield return null;

            var generator = EnsureGenerator();
            generator.GenerateOnStart = false;
            yield return generator.GenerateFromSettingsRoutine(
                settings,
                location.TerrainSeed,
                (message, progress) => RuntimeLoadingOverlay.SetProgress(message, Mathf.Lerp(0.30f, 0.72f, progress)));

            RuntimeLoadingOverlay.SetProgress("Ищем точку входа...", 0.74f);
            yield return null;

            var spawnPoint = FindEntrySpawnPoint(generator.GeneratedTerrainSampler, settings, GeneratedWorldSession.EntryDirection);
            yield return PreloadSpawnChunks(generator, settings, spawnPoint, 0.76f, 0.91f);
            RuntimeLoadingOverlay.SetProgress("Расставляем игрока и переходы...", 0.93f);
            yield return null;

            var player = CreatePlayer(spawnPoint);
            var camera = CreateFirstPersonCamera(player);
            RuntimeFantasyGameUi.Ensure();
            RuntimeMapHud.Ensure();
            LocationTransitionPromptUI.Ensure();
            CreateGatewayTriggers(location, settings);
            RuntimeGeometryDebugPanel.Ensure(camera);
            RuntimeGraphicsSettingsPanel.Ensure(camera);
            RuntimeLoadingOverlay.SetProgress("Готово", 1f);
            yield return null;
            RuntimeLoadingOverlay.Hide();

            Debug.Log($"Generated world location ready. World={world.Name.Value} ({world.Id.Value}), Location={location.Name.Value}, Seed={location.TerrainSeed}, Spawn={spawnPoint}. {generator.LastGenerationSummary}");
        }

        private IEnumerator BootstrapLegacySingleLocation()
        {
            var request = MapGenerationSession.GetRequestOrDefault();
            var mappedSettings = MapGenerationPresetMapper.Build(request);
            var generator = EnsureGenerator();
            generator.GenerateOnStart = false;
            RuntimeLoadingOverlay.Show("Начинаем генерацию локации...", 0.08f);
            yield return null;

            yield return generator.GenerateFromSettingsRoutine(
                mappedSettings.Settings,
                mappedSettings.Seed,
                (message, progress) => RuntimeLoadingOverlay.SetProgress(message, Mathf.Lerp(0.12f, 0.72f, progress)));

            var spawnPoint = FindSafeSpawnPoint(generator.GeneratedTerrainSampler, mappedSettings.Settings);
            yield return PreloadSpawnChunks(generator, mappedSettings.Settings, spawnPoint, 0.74f, 0.91f);
            RuntimeLoadingOverlay.SetProgress("Расставляем игрока...", 0.93f);
            yield return null;

            var player = CreatePlayer(spawnPoint);
            var camera = CreateFirstPersonCamera(player);
            RuntimeFantasyGameUi.Ensure();
            RuntimeGeometryDebugPanel.Ensure(camera);
            RuntimeGraphicsSettingsPanel.Ensure(camera);

            Debug.Log($"GameScene ready. Request={mappedSettings.Summary}. Spawn={spawnPoint}. {generator.LastGenerationSummary}");
            RuntimeLoadingOverlay.SetProgress("Готово", 1f);
            yield return null;
            RuntimeLoadingOverlay.Hide();
        }

        private static IEnumerator PreloadSpawnChunks(
            ProceduralLocationGenerator generator,
            ProceduralLocationSettings settings,
            Vector3 spawnPoint,
            float progressStart,
            float progressEnd)
        {
            var streamer = generator.TerrainChunkStreamer;
            if (streamer == null || settings == null)
            {
                yield break;
            }

            var preloadRadius = Mathf.Min(settings.TerrainChunkLoadRadius, settings.TerrainChunkBootstrapPreloadRadius);
            yield return streamer.PreloadAroundRoutine(
                spawnPoint,
                preloadRadius,
                (loaded, total) =>
                {
                    var progress = total <= 0 ? 1f : loaded / (float)total;
                    RuntimeLoadingOverlay.SetProgress(
                        $"Подгружаем стартовые чанки: {loaded}/{total}",
                        Mathf.Lerp(progressStart, progressEnd, progress));
                });
        }

        private static ProceduralLocationGenerator EnsureGenerator()
        {
            var generator = Object.FindFirstObjectByType<ProceduralLocationGenerator>();
            if (generator != null)
            {
                return generator;
            }

            var generatorObject = new GameObject("ProceduralLocationGenerator");
            return generatorObject.AddComponent<ProceduralLocationGenerator>();
        }

        private static Vector3 FindSafeSpawnPoint(IProceduralTerrainSampler terrainSampler, ProceduralLocationSettings settings)
        {
            if (terrainSampler == null || settings == null)
            {
                return new Vector3(0f, 10f, 0f);
            }

            var waterMinimum = settings.WaterEnabled ? settings.WaterLevel + SafeWaterClearance : 0f;
            var bounds = settings.GetWorldBounds();
            var maxRadius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.65f;
            var bestPoint = new Vector3(0f, float.MinValue, 0f);

            for (var radiusStep = 0; radiusStep <= 10; radiusStep++)
            {
                var radius = maxRadius * radiusStep / 10f;
                var angleCount = radiusStep == 0 ? 1 : 12 + radiusStep * 4;

                for (var angleIndex = 0; angleIndex < angleCount; angleIndex++)
                {
                    var angle = angleIndex / (float)angleCount * Mathf.PI * 2f;
                    var x = Mathf.Cos(angle) * radius;
                    var z = Mathf.Sin(angle) * radius;

                    if (!TrySampleTerrain(terrainSampler, x, z, out var point, out var slope))
                    {
                        continue;
                    }

                    if (point.y > bestPoint.y)
                    {
                        bestPoint = point;
                    }

                    if (point.y >= waterMinimum && slope <= 34f)
                    {
                        return point + Vector3.up * 0.2f;
                    }
                }
            }

            if (bestPoint.y > float.MinValue * 0.5f)
            {
                return bestPoint + Vector3.up * 0.2f;
            }

            return new Vector3(0f, settings.TerrainHeight + 3f, 0f);
        }

        private static Vector3 FindEntrySpawnPoint(IProceduralTerrainSampler terrainSampler, ProceduralLocationSettings settings, WorldDirection entryDirection)
        {
            if (terrainSampler == null || settings == null)
            {
                return new Vector3(0f, 10f, 0f);
            }

            var bounds = settings.GetWorldBounds();
            var candidate = entryDirection switch
            {
                WorldDirection.North => new Vector3(0f, 0f, bounds.extents.z * 0.82f),
                WorldDirection.South => new Vector3(0f, 0f, -bounds.extents.z * 0.82f),
                WorldDirection.East => new Vector3(bounds.extents.x * 0.82f, 0f, 0f),
                WorldDirection.West => new Vector3(-bounds.extents.x * 0.82f, 0f, 0f),
                _ => Vector3.zero
            };

            for (var radius = 0; radius <= 80; radius += 10)
            {
                for (var i = 0; i < 12; i++)
                {
                    var angle = i / 12f * Mathf.PI * 2f;
                    var x = candidate.x + Mathf.Cos(angle) * radius;
                    var z = candidate.z + Mathf.Sin(angle) * radius;
                    if (TrySampleTerrain(terrainSampler, x, z, out var point, out var slope) && slope <= 42f)
                    {
                        return point + Vector3.up * 0.2f;
                    }
                }
            }

            return FindSafeSpawnPoint(terrainSampler, settings);
        }

        private static bool TrySampleTerrain(IProceduralTerrainSampler terrainSampler, float worldX, float worldZ, out Vector3 point, out float slope)
        {
            point = default;
            if (terrainSampler == null || !terrainSampler.TrySample(worldX, worldZ, out point, out var normal))
            {
                slope = 90f;
                return false;
            }

            slope = Vector3.Angle(normal, Vector3.up);
            return true;
        }

        private static void CreateGatewayTriggers(WorldLocation location, ProceduralLocationSettings settings)
        {
            if (location == null || settings == null)
            {
                return;
            }

            var root = new GameObject("Location Gateways").transform;
            var bounds = settings.GetWorldBounds();
            var transitionService = new LocationSceneLoader();

            foreach (var gateway in location.Gateways)
            {
                var rect = gateway.GatewayAreaNormalized;
                var centerX = bounds.min.x + (rect.X + rect.Width * 0.5f) * bounds.size.x;
                var centerZ = bounds.min.z + (rect.Y + rect.Height * 0.5f) * bounds.size.z;
                var triggerObject = new GameObject($"Gateway {gateway.ExitDirection} to {gateway.ToLocationId.Value}");
                triggerObject.transform.SetParent(root, false);
                triggerObject.transform.position = new Vector3(centerX, settings.TerrainHeight * 0.5f, centerZ);

                var collider = triggerObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(
                    Mathf.Max(32f, rect.Width * bounds.size.x),
                    settings.TerrainHeight + 80f,
                    Mathf.Max(32f, rect.Height * bounds.size.z));

                var trigger = triggerObject.AddComponent<LocationBoundaryTrigger>();
                trigger.Configure(location, gateway, transitionService);
            }
        }

        private static GameObject CreatePlayer(Vector3 spawnPoint)
        {
            var player = new GameObject("Player");
            player.transform.position = spawnPoint;

            var characterController = player.AddComponent<CharacterController>();
            characterController.height = PlayerHeight;
            characterController.radius = PlayerRadius;
            characterController.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            characterController.stepOffset = 0.4f;
            characterController.slopeLimit = 50f;

            player.AddComponent<SimplePlayerController>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Player Capsule";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(PlayerRadius * 2f, PlayerHeight * 0.5f, PlayerRadius * 2f);

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            visual.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Player Material", new Color(0.40f, 0.52f, 0.72f, 1f));
            return player;
        }

        private static UnityEngine.Camera CreateFirstPersonCamera(GameObject player)
        {
            var existingCamera = UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main
                : Object.FindFirstObjectByType<UnityEngine.Camera>();
            var cameraObject = existingCamera != null
                ? existingCamera.gameObject
                : new GameObject("First Person Camera");

            cameraObject.name = "First Person Camera";
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, PlayerEyeHeight, 0.08f);
            cameraObject.transform.localRotation = Quaternion.identity;

            var camera = existingCamera != null ? existingCamera : cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 3000f;

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            var followCamera = cameraObject.GetComponent<LegendsOfWarAndMagic.Game.Camera.SimpleFollowCamera>();
            if (followCamera != null)
            {
                Object.Destroy(followCamera);
            }

            var playerController = player.GetComponent<SimplePlayerController>();
            if (playerController != null)
            {
                playerController.SetViewCamera(cameraObject.transform);
            }

            return camera;
        }

        private static void EnsureLighting()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return;
                }
            }

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.93f, 0.82f, 1f);
        }

        private static Material CreateMaterial(string name, Color color)
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
                name = name,
                color = color,
                hideFlags = HideFlags.HideAndDontSave
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            return material;
        }
    }
}
