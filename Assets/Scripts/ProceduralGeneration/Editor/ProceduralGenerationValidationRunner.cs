using System.Collections.Generic;
using System.IO;
using System.Text;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor
{
    public static class ProceduralGenerationValidationRunner
    {
        private const string ScreenshotFolder = "Assets/Screenshots";

        public static void RunDenseForestValidation()
        {
            ProceduralEnvironmentAssetCatalogBuilder.RebuildDefaultCatalogFromImportedAssets();
            EnsureFolder(ScreenshotFolder);

            var requests = new[]
            {
                new NamedRequest(
                    "mainland-hills-high-forest",
                    new MapGenerationRequest
                    {
                        MapSize = MapSizeOption.Small,
                        LandType = LandTypeOption.Mainland,
                        WaterAmount = WaterAmountOption.Normal,
                        Relief = ReliefOption.Hills,
                        PropDensity = PropDensityOption.High,
                        TreeDensity = 1f,
                        SeedText = "dense-mainland-hills"
                    }),
                new NamedRequest(
                    "mainland-mountains-high-forest",
                    new MapGenerationRequest
                    {
                        MapSize = MapSizeOption.Small,
                        LandType = LandTypeOption.Mainland,
                        WaterAmount = WaterAmountOption.Low,
                        Relief = ReliefOption.Mountains,
                        PropDensity = PropDensityOption.High,
                        TreeDensity = 1f,
                        SeedText = "dense-mainland-mountains"
                    }),
                new NamedRequest(
                    "archipelago-hills-high-forest",
                    new MapGenerationRequest
                    {
                        MapSize = MapSizeOption.Small,
                        LandType = LandTypeOption.Archipelago,
                        WaterAmount = WaterAmountOption.High,
                        Relief = ReliefOption.Hills,
                        PropDensity = PropDensityOption.High,
                        TreeDensity = 1f,
                        SeedText = "dense-archipelago-hills"
                    })
            };

            for (var i = 0; i < requests.Length; i++)
            {
                ValidateRequest(requests[i]);
            }

            AssetDatabase.Refresh();
        }

        private static void ValidateRequest(NamedRequest namedRequest)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureLighting();

            var mapped = MapGenerationPresetMapper.Build(namedRequest.Request);
            var generatorObject = new GameObject("ProceduralLocationGenerator");
            var generator = generatorObject.AddComponent<ProceduralLocationGenerator>();
            generator.GenerateOnStart = false;
            generator.GenerateFromSettings(mapped.Settings, mapped.Seed);

            var report = BuildReport(namedRequest.Name, generator);
            Debug.Log(report);
            CaptureScreenshot(namedRequest.Name, generator.GeneratedTerrain);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static string BuildReport(string validationName, ProceduralLocationGenerator generator)
        {
            var root = GameObject.Find("GeneratedProps");
            var categoryCounts = new Dictionary<string, int>();
            var prototypeNamedObjects = 0;
            if (root != null)
            {
                CountGeneratedProps(root.transform, categoryCounts, ref prototypeNamedObjects);
            }

            var detailSummary = "no terrain";
            var terrain = generator.GeneratedTerrain;
            if (terrain != null && terrain.terrainData != null)
            {
                var prototypes = terrain.terrainData.detailPrototypes;
                var detailNames = new List<string>();
                for (var i = 0; i < prototypes.Length; i++)
                {
                    var prototype = prototypes[i];
                    if (prototype == null)
                    {
                        continue;
                    }

                    if (prototype.prototype != null)
                    {
                        detailNames.Add(prototype.prototype.name);
                    }
                    else if (prototype.prototypeTexture != null)
                    {
                        detailNames.Add(prototype.prototypeTexture.name);
                    }
                }

                detailSummary = string.Join(", ", detailNames);
            }

            var builder = new StringBuilder();
            builder.Append("Dense forest validation '");
            builder.Append(validationName);
            builder.Append("'. PrototypeNamedObjects=");
            builder.Append(prototypeNamedObjects);
            builder.Append(". Categories=");
            var first = true;
            foreach (var pair in categoryCounts)
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

            builder.Append(". TerrainDetails=");
            builder.Append(detailSummary);
            builder.Append(". ");
            builder.Append(generator.LastGenerationSummary);
            return builder.ToString();
        }

        private static void CountGeneratedProps(
            Transform propsRoot,
            Dictionary<string, int> categoryCounts,
            ref int prototypeNamedObjects)
        {
            for (var categoryIndex = 0; categoryIndex < propsRoot.childCount; categoryIndex++)
            {
                var category = propsRoot.GetChild(categoryIndex);
                categoryCounts[category.name] = category.childCount;
                for (var instanceIndex = 0; instanceIndex < category.childCount; instanceIndex++)
                {
                    var instance = category.GetChild(instanceIndex);
                    if (instance.name.Contains("Prototype"))
                    {
                        prototypeNamedObjects++;
                    }
                }
            }
        }

        private static void CaptureScreenshot(string validationName, Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return;
            }

            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log(
                    $"Skipped validation screenshot for '{validationName}' because batchmode HDRP offscreen rendering is not reliable in this environment.");
                return;
            }

            var cameraObject = new GameObject("Validation Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2500f;

            var terrainSize = terrain.terrainData.size;
            var lookAt = new Vector3(terrain.transform.position.x + terrainSize.x * 0.52f, 0f, terrain.transform.position.z + terrainSize.z * 0.55f);
            lookAt.y = terrain.SampleHeight(lookAt) + terrain.transform.position.y + 8f;
            var cameraPosition = lookAt + new Vector3(-72f, 36f, -92f);
            cameraObject.transform.position = cameraPosition;
            cameraObject.transform.LookAt(lookAt);

            var renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            camera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            camera.Render();

            var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            var path = $"{ScreenshotFolder}/{validationName}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());

            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Debug.Log($"Saved procedural generation validation screenshot: {path}");
        }

        private static void EnsureLighting()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.94f, 0.84f, 1f);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folder = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private readonly struct NamedRequest
        {
            public NamedRequest(string name, MapGenerationRequest request)
            {
                Name = name;
                Request = request;
            }

            public string Name { get; }
            public MapGenerationRequest Request { get; }
        }
    }
}
