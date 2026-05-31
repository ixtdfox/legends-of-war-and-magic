using System;
using System.Collections.Generic;
using System.IO;
using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.DebugTools.UnityIntegration;
using LegendsOfWarAndMagic.ProceduralGeneration;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace LegendsOfWarAndMagic.DebugTools.Snapshots
{
    public static class TerrainSnapshotService
    {
        public static string Capture(DebugSession session)
        {
            if (session == null || !session.IsEnabled)
            {
                return null;
            }

            var directory = session.NextSnapshotDirectory();
            try
            {
                using (session.Profiler.Scope("DebugSnapshot.CaptureTerrain", new { directory }))
                {
                    CaptureInternal(session, directory);
                }

                session.SetLastSavedPath(directory);
                return directory;
            }
            catch (Exception exception)
            {
                session.Writer.WriteJson(Path.Combine(directory, "snapshot-error.json"), new
                {
                    exception = exception.GetType().FullName,
                    exception.Message,
                    exception.StackTrace
                });
                Debug.LogWarning($"DebugTools: terrain snapshot failed: {exception.Message}");
                return directory;
            }
        }

        private static void CaptureInternal(DebugSession session, string directory)
        {
            var generator = UnityEngine.Object.FindFirstObjectByType<ProceduralLocationGenerator>();
            var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            var player = ResolvePlayer(camera);
            var terrainSampler = ResolveTerrainSampler(generator);
            var masks = generator != null ? generator.GeneratedWorldLayers?.Masks : null;
            var relevantTerrains = TerrainMeshObjExporter.SelectRelevantTerrains(camera, player != null ? player.transform : null);
            var snapshotArea = TerrainDataSerializer.ResolveSnapshotArea(camera, player != null ? player.transform : null, relevantTerrains);
            var settings = generator != null ? generator.CurrentSettings : null;
            var seed = generator != null ? generator.LastUsedSeed : 0;

            session.Writer.CreateDirectory(Path.Combine(directory, "noise-maps"));
            session.Writer.WriteJson(Path.Combine(directory, "snapshot.json"), new
            {
                snapshotId = Path.GetFileName(directory),
                timestampUtc = DateTime.UtcNow,
                session = session.BuildSessionMetadata(),
                scene = SceneManager.GetActiveScene().name,
                unity = BuildUnityIncidentMetadata(),
                generation = DebugGenerationInstrumentation.BuildGeneratorSummary(generator),
                settings = DebugGenerationInstrumentation.BuildLocationSettingsSnapshot(settings, seed),
                activeDebugToggles = new
                {
                    debugEnabled = true,
                    runtimeRecording = session.IsRuntimeRecording
                }
            });

            session.Writer.WriteJson(Path.Combine(directory, "generation-inputs.json"), DebugGenerationInstrumentation.BuildLocationSettingsSnapshot(settings, seed));
            session.Writer.WriteJson(Path.Combine(directory, "camera.json"), CameraSnapshotSerializer.CaptureCamera(camera));
            session.Writer.WriteJson(Path.Combine(directory, "player.json"), CameraSnapshotSerializer.CapturePlayer(player));

            var terrainData = TerrainDataSerializer.CaptureTerrainData(terrainSampler, masks, snapshotArea, 65);
            session.Writer.WriteJson(Path.Combine(directory, "terrain-data.json"), terrainData);
            session.Writer.WriteJson(Path.Combine(directory, "noise-maps", "heightmap.json"), terrainData);
            session.Writer.WriteJson(Path.Combine(directory, "noise-maps", "road-mask.json"), new
            {
                source = "WorldGenerationMaskSet road evaluation sampled in terrain-data.json",
                terrainDataFile = "../terrain-data.json"
            });
            session.Writer.WriteJson(Path.Combine(directory, "noise-maps", "biome-map.json"), new
            {
                status = "not available",
                reason = "Runtime location terrain currently uses terrain shaping and world-feature masks, not a per-cell biome map."
            });
            session.Writer.WriteJson(Path.Combine(directory, "noise-maps", "moisture-map.json"), new
            {
                status = "not available",
                reason = "Moisture is used by the top-down world map generator, not by the loaded runtime terrain chunks."
            });
            session.Writer.WriteJson(Path.Combine(directory, "noise-maps", "temperature-map.json"), new
            {
                status = "not available",
                reason = "Temperature is used by the top-down world map generator, not by the loaded runtime terrain chunks."
            });

            var objPath = Path.Combine(directory, "terrain-mesh.obj");
            var meshMetadata = TerrainMeshObjExporter.Export(
                objPath,
                relevantTerrains,
                player != null ? player.transform.position : camera != null ? camera.transform.position : Vector3.zero);
            session.Writer.WriteJson(Path.Combine(directory, "terrain-mesh-metadata.json"), meshMetadata);

            var visibleArea = VisibleAreaSampler.Capture(camera, player != null ? player.transform : null, terrainSampler, masks, relevantTerrains);
            session.Writer.WriteJson(Path.Combine(directory, "visible-area.json"), visibleArea);
            session.Writer.WriteJson(
                Path.Combine(directory, "nearby-objects.json"),
                SceneObjectSnapshotSerializer.CaptureNearbyObjects(
                    player != null ? player.transform.position : camera != null ? camera.transform.position : Vector3.zero,
                    camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null));

            TryCaptureScreenshot(directory);
        }

        private static GameObject ResolvePlayer(Camera camera)
        {
            try
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                {
                    return tagged;
                }
            }
            catch (UnityException)
            {
            }

            return camera != null && camera.transform.parent != null ? camera.transform.parent.gameObject : null;
        }

        private static IProceduralTerrainSampler ResolveTerrainSampler(ProceduralLocationGenerator generator)
        {
            if (generator == null)
            {
                return null;
            }

            return generator.TerrainChunkStreamer as IProceduralTerrainSampler ?? generator.GeneratedTerrainSampler;
        }

        private static object BuildUnityIncidentMetadata()
        {
            return new
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                screen = new { Screen.width, Screen.height },
                qualityLevel = QualitySettings.GetQualityLevel(),
                qualityName = QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : string.Empty,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                isEditor = Application.isEditor,
                isDebugBuild = Debug.isDebugBuild,
                memory = new
                {
                    allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
                    reservedBytes = Profiler.GetTotalReservedMemoryLong(),
                    monoUsedBytes = Profiler.GetMonoUsedSizeLong()
                },
                activeGameObjectCounts = CountSceneObjects()
            };
        }

        private static object CountSceneObjects()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < roots.Length; i++)
            {
                var transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (var j = 0; j < transforms.Length; j++)
                {
                    var category = SceneObjectSnapshotSerializer.Categorize(transforms[j].gameObject);
                    counts.TryGetValue(category, out var count);
                    counts[category] = count + 1;
                }
            }

            return counts;
        }

        private static void TryCaptureScreenshot(string directory)
        {
            try
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, "screenshot.png"));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"DebugTools: screenshot capture failed: {exception.Message}");
            }
        }
    }
}
