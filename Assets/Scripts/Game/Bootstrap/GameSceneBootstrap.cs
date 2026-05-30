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
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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
            ApplyForestVisualMood();

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

            var isInitialLocationEntry = string.IsNullOrWhiteSpace(GeneratedWorldSession.PreviousLocationId.Value);
            var spawnPoint = isInitialLocationEntry
                ? FindInitialSettlementSpawnPoint(generator, settings)
                : FindEntrySpawnPoint(generator.GeneratedTerrainSampler, settings, GeneratedWorldSession.EntryDirection);
            yield return PreloadSpawnChunks(generator, settings, spawnPoint, 0.76f, 0.91f);
            spawnPoint = SnapSpawnPointToGeneratedTerrain(generator, spawnPoint);
            RuntimeLoadingOverlay.SetProgress("Расставляем игрока и переходы...", 0.93f);
            yield return null;

            var player = CreatePlayer(spawnPoint);
            OrientPlayerTowardLocationCenter(player, spawnPoint);
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

            var spawnPoint = FindInitialSettlementSpawnPoint(generator, mappedSettings.Settings);
            yield return PreloadSpawnChunks(generator, mappedSettings.Settings, spawnPoint, 0.74f, 0.91f);
            spawnPoint = SnapSpawnPointToGeneratedTerrain(generator, spawnPoint);
            RuntimeLoadingOverlay.SetProgress("Расставляем игрока...", 0.93f);
            yield return null;

            var player = CreatePlayer(spawnPoint);
            OrientPlayerTowardLocationCenter(player, spawnPoint);
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
            if (settings == null)
            {
                yield break;
            }

            var preloadRadius = Mathf.Min(settings.TerrainChunkLoadRadius, settings.TerrainChunkBootstrapPreloadRadius);
            var terrainProgressEnd = generator.PropChunkStreamer != null
                ? Mathf.Lerp(progressStart, progressEnd, 0.62f)
                : progressEnd;

            if (streamer != null)
            {
                yield return streamer.PreloadAroundRoutine(
                    spawnPoint,
                    preloadRadius,
                    (loaded, total) =>
                    {
                        var progress = total <= 0 ? 1f : loaded / (float)total;
                        RuntimeLoadingOverlay.SetProgress(
                            $"Подгружаем стартовые чанки: {loaded}/{total}",
                            Mathf.Lerp(progressStart, terrainProgressEnd, progress));
                    });
            }

            var propStreamer = generator.PropChunkStreamer;
            if (propStreamer == null)
            {
                yield break;
            }

            yield return propStreamer.PreloadAroundRoutine(
                spawnPoint,
                preloadRadius,
                (loaded, total) =>
                {
                    var progress = total <= 0 ? 1f : loaded / (float)total;
                    RuntimeLoadingOverlay.SetProgress(
                        $"Подгружаем окружение: {loaded}/{total}",
                        Mathf.Lerp(terrainProgressEnd, progressEnd, progress));
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
            var bestValidPoint = new Vector3(0f, float.MinValue, 0f);
            var bestValidScore = float.MinValue;

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
                        var radius01 = maxRadius > 0f ? radius / maxRadius : 0f;
                        var scenicBand = 1f - Mathf.Abs(radius01 - 0.34f);
                        var score = point.y * 0.85f - slope * 1.2f + scenicBand * 18f;
                        if (score > bestValidScore)
                        {
                            bestValidScore = score;
                            bestValidPoint = point;
                        }
                    }
                }
            }

            if (bestValidScore > float.MinValue * 0.5f)
            {
                return bestValidPoint + Vector3.up * 0.2f;
            }

            if (bestPoint.y > float.MinValue * 0.5f)
            {
                return bestPoint + Vector3.up * 0.2f;
            }

            return new Vector3(0f, settings.TerrainHeight + 3f, 0f);
        }

        private static Vector3 FindInitialSettlementSpawnPoint(ProceduralLocationGenerator generator, ProceduralLocationSettings settings)
        {
            if (generator == null || settings == null || generator.GeneratedWorldLayers?.Settlements == null)
            {
                return FindSafeSpawnPoint(generator != null ? generator.GeneratedTerrainSampler : null, settings);
            }

            var settlement = SelectInitialSpawnSettlement(generator.GeneratedWorldLayers);
            if (settlement == null)
            {
                return FindSafeSpawnPoint(generator.GeneratedTerrainSampler, settings);
            }

            var sampler = new SettlementAdjustedTerrainSampler(
                generator.GeneratedTerrainSampler,
                settings,
                generator.GeneratedWorldLayers.Settlements);
            if (TryFindSettlementSpawnCandidate(settlement, sampler, settings, out var point))
            {
                return point + Vector3.up * 0.2f;
            }

            if (sampler.TrySample(settlement.WorldPosition.x, settlement.WorldPosition.y, out point, out _))
            {
                return point + Vector3.up * 0.2f;
            }

            return FindSafeSpawnPoint(generator.GeneratedTerrainSampler, settings);
        }

        private static GeneratedSettlement SelectInitialSpawnSettlement(WorldGenerationLayers layers)
        {
            GeneratedSettlement best = null;
            var bestScore = float.MinValue;
            for (var i = 0; i < layers.Settlements.Count; i++)
            {
                var settlement = layers.Settlements[i];
                var score = settlement.Tier switch
                {
                    SettlementTier.Village => 100f,
                    SettlementTier.Town => 92f,
                    SettlementTier.Hamlet => 84f,
                    SettlementTier.City => 78f,
                    SettlementTier.Capital => 76f,
                    SettlementTier.Camp => 42f,
                    _ => 50f
                };
                score += Mathf.Clamp(settlement.Radius, 0f, 80f) * 0.05f;
                if (score <= bestScore)
                {
                    continue;
                }

                best = settlement;
                bestScore = score;
            }

            return best;
        }

        private static bool TryFindSettlementSpawnCandidate(
            GeneratedSettlement settlement,
            IProceduralTerrainSampler sampler,
            ProceduralLocationSettings settings,
            out Vector3 point)
        {
            point = default;
            if (settlement == null || sampler == null || settings == null)
            {
                return false;
            }

            var candidates = new System.Collections.Generic.List<Vector2>(32)
            {
                settlement.WorldPosition
            };

            for (var i = 0; i < settlement.InternalRoads.Count; i++)
            {
                var road = settlement.InternalRoads[i];
                if (road.Points.Count == 0)
                {
                    continue;
                }

                candidates.Add(road.Points[road.Points.Count / 2]);
                candidates.Add(road.Points[0]);
                candidates.Add(road.Points[road.Points.Count - 1]);
            }

            var ringRadius = Mathf.Clamp(settlement.Radius * 0.18f, 8f, 22f);
            for (var i = 0; i < 16; i++)
            {
                var angle = i * Mathf.PI * 2f / 16f;
                candidates.Add(new Vector2(
                    settlement.WorldPosition.x + Mathf.Cos(angle) * ringRadius,
                    settlement.WorldPosition.y + Mathf.Sin(angle) * ringRadius));
            }

            var bestScore = float.MinValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (IsInsideSettlementBuilding(settlement, candidate))
                {
                    continue;
                }

                if (!sampler.TrySample(candidate.x, candidate.y, out var sampledPoint, out var normal))
                {
                    continue;
                }

                var slope = Vector3.Angle(normal, Vector3.up);
                if (slope > 18f || (settings.WaterEnabled && sampledPoint.y <= settings.WaterLevel + SafeWaterClearance))
                {
                    continue;
                }

                var centerDistance = Vector2.Distance(candidate, settlement.WorldPosition);
                var roadBonus = i > 0 && i <= settlement.InternalRoads.Count * 3 ? 18f : 0f;
                var score = 100f - centerDistance * 0.7f - slope * 2.8f + roadBonus;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                point = sampledPoint;
            }

            return bestScore > float.MinValue * 0.5f;
        }

        private static bool IsInsideSettlementBuilding(GeneratedSettlement settlement, Vector2 point)
        {
            for (var i = 0; i < settlement.Buildings.Count; i++)
            {
                var building = settlement.Buildings[i];
                var clearance = Mathf.Max(building.FootprintSize.x, building.FootprintSize.y) * 0.5f + PlayerRadius + 2.5f;
                if (Vector2.Distance(point, building.WorldPosition) <= clearance)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 SnapSpawnPointToGeneratedTerrain(ProceduralLocationGenerator generator, Vector3 spawnPoint)
        {
            if (generator == null)
            {
                return spawnPoint;
            }

            var sampler = generator.TerrainChunkStreamer as IProceduralTerrainSampler ?? generator.GeneratedTerrainSampler;
            if (sampler != null && sampler.TrySample(spawnPoint.x, spawnPoint.z, out var point, out _))
            {
                return point + Vector3.up * 0.2f;
            }

            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                var position = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (spawnPoint.x < position.x ||
                    spawnPoint.x > position.x + size.x ||
                    spawnPoint.z < position.z ||
                    spawnPoint.z > position.z + size.z)
                {
                    continue;
                }

                var height = terrain.SampleHeight(new Vector3(spawnPoint.x, 0f, spawnPoint.z)) + position.y;
                return new Vector3(spawnPoint.x, height + 0.2f, spawnPoint.z);
            }

            return spawnPoint;
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
            try
            {
                player.tag = "Player";
            }
            catch (UnityException)
            {
                // Projects without the Player tag still work through the camera fallback.
            }

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

        private static void OrientPlayerTowardLocationCenter(GameObject player, Vector3 spawnPoint)
        {
            if (player == null)
            {
                return;
            }

            var centerDirection = new Vector3(-spawnPoint.x, 0f, -spawnPoint.z);
            if (centerDirection.sqrMagnitude <= 0.01f)
            {
                return;
            }

            player.transform.rotation = Quaternion.LookRotation(centerDirection.normalized, Vector3.up);
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
            camera.fieldOfView = 64f;
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
                playerController.SetViewPitch(0f);
            }

            return camera;
        }

        private static void EnsureLighting()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Light primaryDirectional = null;
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    primaryDirectional ??= lights[i];
                }
            }

            if (primaryDirectional == null)
            {
                var lightObject = new GameObject("Directional Light");
                primaryDirectional = lightObject.AddComponent<Light>();
                primaryDirectional.type = LightType.Directional;
            }

            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    ConfigureForestDirectionalLight(lights[i], lights[i] == primaryDirectional);
                }
            }

            ConfigureForestDirectionalLight(primaryDirectional, true);
        }

        private static void ConfigureForestDirectionalLight(Light light, bool primary)
        {
            if (light == null)
            {
                return;
            }

            light.enabled = true;
            light.transform.rotation = Quaternion.Euler(44f, -42f, 0f);
            light.color = primary ? new Color(0.90f, 0.91f, 0.80f, 1f) : new Color(0.58f, 0.64f, 0.58f, 1f);
            light.intensity = primary ? 25500f : 1600f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = primary ? 0.70f : 0.14f;
            light.shadowBias = 0.045f;
            light.shadowNormalBias = 0.28f;

            var hdLight = light.GetComponent<HDAdditionalLightData>();
            if (hdLight != null)
            {
                hdLight.SetLightDimmer(primary ? 0.95f : 0.62f, primary ? 0.68f : 0.24f);
            }
        }

        private static void ApplyForestVisualMood()
        {
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 2.25f);
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 180f);
            QualitySettings.shadowCascades = Mathf.Max(QualitySettings.shadowCascades, 4);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.40f, 0.43f, 0.39f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.30f, 0.22f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.075f, 0.095f, 0.065f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.66f, 0.69f, 0.66f, 1f);
            RenderSettings.fogDensity = 0.0038f;

            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None);
            for (var i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == null)
                {
                    continue;
                }

                cameras[i].clearFlags = CameraClearFlags.Skybox;
                cameras[i].backgroundColor = new Color(0.66f, 0.69f, 0.66f, 1f);
                cameras[i].farClipPlane = Mathf.Min(cameras[i].farClipPlane, 1800f);
            }

            ApplyHdrpForestVolumeMood();
        }

        private static void ApplyHdrpForestVolumeMood()
        {
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            for (var i = 0; i < volumes.Length; i++)
            {
                var profile = volumes[i] != null ? volumes[i].profile : null;
                if (profile == null)
                {
                    continue;
                }

                if (profile.TryGet<VisualEnvironment>(out var visualEnvironment))
                {
                    SetEnumVolumeParameter(visualEnvironment, "fogType", "Exponential");
                    visualEnvironment.skyAmbientMode.overrideState = true;
                    visualEnvironment.skyAmbientMode.value = SkyAmbientMode.Dynamic;
                }

                if (profile.TryGet<PhysicallyBasedSky>(out var sky))
                {
                    sky.multiplier.overrideState = true;
                    sky.multiplier.value = 0.64f;
                    sky.desiredLuxValue.overrideState = true;
                    sky.desiredLuxValue.value = 18500f;
                    sky.groundTint.overrideState = true;
                    sky.groundTint.value = new Color(0.12f, 0.14f, 0.11f, 1f);
                    sky.horizonTint.overrideState = true;
                    sky.horizonTint.value = new Color(0.70f, 0.71f, 0.67f, 1f);
                    sky.zenithTint.overrideState = true;
                    sky.zenithTint.value = new Color(0.58f, 0.63f, 0.62f, 1f);
                    sky.airTint.overrideState = true;
                    sky.airTint.value = new Color(0.76f, 0.77f, 0.71f, 1f);
                    sky.aerosolTint.overrideState = true;
                    sky.aerosolTint.value = new Color(0.68f, 0.68f, 0.60f, 1f);
                    sky.aerosolDensity.overrideState = true;
                    sky.aerosolDensity.value = 0.025f;
                    sky.colorSaturation.overrideState = true;
                    sky.colorSaturation.value = 0.32f;
                }

                if (profile.TryGet<Fog>(out var fog))
                {
                    fog.enabled.overrideState = true;
                    fog.enabled.value = true;
                    fog.tint.overrideState = true;
                    fog.tint.value = new Color(0.68f, 0.70f, 0.66f, 1f);
                    fog.maxFogDistance.overrideState = true;
                    fog.maxFogDistance.value = 1800f;
                    fog.mipFogNear.overrideState = true;
                    fog.mipFogNear.value = 30f;
                    fog.mipFogFar.overrideState = true;
                    fog.mipFogFar.value = 560f;
                    fog.meanFreePath.overrideState = true;
                    fog.meanFreePath.value = 300f;
                    fog.baseHeight.overrideState = true;
                    fog.baseHeight.value = -28f;
                    fog.maximumHeight.overrideState = true;
                    fog.maximumHeight.value = 190f;
                    fog.enableVolumetricFog.overrideState = true;
                    fog.enableVolumetricFog.value = false;
                    fog.globalLightProbeDimmer.overrideState = true;
                    fog.globalLightProbeDimmer.value = 1f;
                    fog.anisotropy.overrideState = true;
                    fog.anisotropy.value = 0.35f;
                }

                if (profile.TryGet<Exposure>(out var exposure))
                {
                    exposure.mode.overrideState = true;
                    exposure.mode.value = ExposureMode.Fixed;
                    exposure.fixedExposure.overrideState = true;
                    exposure.fixedExposure.value = 9.45f;
                    exposure.compensation.overrideState = true;
                    exposure.compensation.value = -0.30f;
                    exposure.limitMin.overrideState = true;
                    exposure.limitMin.value = 8.3f;
                    exposure.limitMax.overrideState = true;
                    exposure.limitMax.value = 10.6f;
                    exposure.adaptationSpeedDarkToLight.overrideState = true;
                    exposure.adaptationSpeedDarkToLight.value = 2.4f;
                    exposure.adaptationSpeedLightToDark.overrideState = true;
                    exposure.adaptationSpeedLightToDark.value = 2.4f;
                }

                var tonemapping = EnsureVolumeOverride<Tonemapping>(profile);
                tonemapping.mode.overrideState = true;
                tonemapping.mode.value = TonemappingMode.ACES;

                var color = EnsureVolumeOverride<ColorAdjustments>(profile);
                color.postExposure.overrideState = true;
                color.postExposure.value = -0.12f;
                color.contrast.overrideState = true;
                color.contrast.value = 8f;
                color.saturation.overrideState = true;
                color.saturation.value = -26f;
                color.colorFilter.overrideState = true;
                color.colorFilter.value = new Color(0.96f, 0.98f, 0.91f, 1f);

                var ambientOcclusion = EnsureVolumeOverride<ScreenSpaceAmbientOcclusion>(profile);
                ambientOcclusion.active = true;
                ambientOcclusion.intensity.overrideState = true;
                ambientOcclusion.intensity.value = 0.85f;
                ambientOcclusion.directLightingStrength.overrideState = true;
                ambientOcclusion.directLightingStrength.value = 0.42f;
                ambientOcclusion.radius.overrideState = true;
                ambientOcclusion.radius.value = 1.45f;

                var bloom = EnsureVolumeOverride<Bloom>(profile);
                bloom.active = true;
                bloom.threshold.overrideState = true;
                bloom.threshold.value = 1.1f;
                bloom.intensity.overrideState = true;
                bloom.intensity.value = 0.035f;
                bloom.scatter.overrideState = true;
                bloom.scatter.value = 0.45f;
            }
        }

        private static T EnsureVolumeOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component))
            {
                component.active = true;
                return component;
            }

            return profile.Add<T>(true);
        }

        private static void SetEnumVolumeParameter(VolumeComponent component, string fieldName, string enumValueName)
        {
            if (component == null)
            {
                return;
            }

            var field = component.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (field == null || field.GetValue(component) is not VolumeParameter parameter)
            {
                return;
            }

            var valueProperty = parameter.GetType().GetProperty("value");
            if (valueProperty == null || !valueProperty.PropertyType.IsEnum)
            {
                return;
            }

            parameter.overrideState = true;
            valueProperty.SetValue(parameter, System.Enum.Parse(valueProperty.PropertyType, enumValueName));
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
