using LegendsOfWarAndMagic.Game.Player;
using LegendsOfWarAndMagic.ProceduralGeneration;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
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

        private void Start()
        {
            EnsureLighting();

            var request = MapGenerationSession.GetRequestOrDefault();
            var mappedSettings = MapGenerationPresetMapper.Build(request);
            var generator = EnsureGenerator();
            generator.GenerateOnStart = false;
            generator.GenerateFromSettings(mappedSettings.Settings, mappedSettings.Seed);

            var spawnPoint = FindSafeSpawnPoint(generator.GeneratedTerrain, mappedSettings.Settings);
            var player = CreatePlayer(spawnPoint);
            CreateFirstPersonCamera(player);

            Debug.Log($"GameScene ready. Request={mappedSettings.Summary}. Spawn={spawnPoint}. {generator.LastGenerationSummary}");
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

        private static Vector3 FindSafeSpawnPoint(Terrain terrain, ProceduralLocationSettings settings)
        {
            if (terrain == null || terrain.terrainData == null || settings == null)
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

                    if (!TrySampleTerrain(terrain, x, z, out var point, out var slope))
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

        private static bool TrySampleTerrain(Terrain terrain, float worldX, float worldZ, out Vector3 point, out float slope)
        {
            var terrainPosition = terrain.transform.position;
            var size = terrain.terrainData.size;
            var localX = worldX - terrainPosition.x;
            var localZ = worldZ - terrainPosition.z;

            if (localX < 0f || localX > size.x || localZ < 0f || localZ > size.z)
            {
                point = default;
                slope = 90f;
                return false;
            }

            var normalizedX = Mathf.Clamp01(localX / size.x);
            var normalizedZ = Mathf.Clamp01(localZ / size.z);
            var y = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainPosition.y;
            var normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);

            point = new Vector3(worldX, y, worldZ);
            slope = Vector3.Angle(normal, Vector3.up);
            return true;
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

        private static void CreateFirstPersonCamera(GameObject player)
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
