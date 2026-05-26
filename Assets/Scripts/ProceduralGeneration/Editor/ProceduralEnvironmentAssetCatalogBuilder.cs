using System.Collections.Generic;
using System.IO;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEditor;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor
{
    public static class ProceduralEnvironmentAssetCatalogBuilder
    {
        private const string CatalogPath = "Assets/Resources/ProceduralGeneration/DefaultEnvironmentAssetCatalog.asset";
        private const string ResourceRoot = "Assets/Resources";
        private const string ResourcePrefabRoot = ResourceRoot + "/Prefabs";
        private const string ResourceTextureRoot = ResourceRoot + "/Textures";
        private const string ResourceGrassTextureRoot = ResourceTextureRoot + "/Grass";
        private const string ResourceTerrainLayerRoot = ResourceRoot + "/TerrainLayers";
        private const string GeneratedLayerFolder = "Assets/Resources/ProceduralGeneration/GeneratedTerrainLayers";

        [MenuItem("Tools/Legends of War and Magic/Procedural Generation/Rebuild Environment Asset Catalog")]
        public static void RebuildDefaultCatalog()
        {
            var catalog = RebuildDefaultCatalogFromImportedAssets();
            Debug.Log(
                $"Environment asset catalog rebuilt. TerrainSurfaces={catalog.TerrainSurfaces.Count}, " +
                $"PropCategories={catalog.PropCategories.Count}, TerrainDetails={catalog.TerrainDetails.Count}. Asset={CatalogPath}");
        }

        public static ProceduralEnvironmentAssetCatalog RebuildDefaultCatalogFromImportedAssets()
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/ProceduralGeneration");
            EnsureFolder(GeneratedLayerFolder);

            var catalog = AssetDatabase.LoadAssetAtPath<ProceduralEnvironmentAssetCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ProceduralEnvironmentAssetCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var terrainSurfaces = BuildTerrainSurfaces();
            var propCategories = BuildPropCategories();
            var terrainDetails = BuildTerrainDetails();
            catalog.Configure(terrainSurfaces, propCategories, terrainDetails);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Procedural environment catalog summary: terrainSurfaces={terrainSurfaces.Count}, " +
                $"propCategories={propCategories.Count}, terrainDetails={terrainDetails.Count}.");
            return catalog;
        }

        private static List<TerrainSurfaceDefinition> BuildTerrainSurfaces()
        {
            EnsureFristyTerrainLayers();

            var surfaces = new List<TerrainSurfaceDefinition>();
            TryAddLocalTerrainLayer(surfaces, TerrainSurfaceRole.Shore, "Fristy Mud Shore", "Fristy_Terrain_Mud", new Color(0.55f, 0.48f, 0.31f, 1f), 8f);
            TryAddLocalTerrainLayer(surfaces, TerrainSurfaceRole.Ground, "Fristy Grass Ground", "Fristy_Terrain_Grass", new Color(0.12f, 0.28f, 0.12f, 1f), 10f);
            TryAddLocalTerrainLayer(surfaces, TerrainSurfaceRole.Rock, "Fristy Rock", "Fristy_Terrain_Rock", new Color(0.36f, 0.36f, 0.34f, 1f), 9f);
            TryAddLocalTerrainLayer(surfaces, TerrainSurfaceRole.Highland, "Fristy Soil Highland", "Fristy_Terrain_Soil", new Color(0.54f, 0.56f, 0.50f, 1f), 11f);

            if (surfaces.Count >= 4)
            {
                return surfaces;
            }

            var selectedTextures = new Dictionary<TerrainSurfaceRole, Texture2D>();
            var textureSearchRoot = AssetDatabase.IsValidFolder(ResourceTextureRoot) ? ResourceTextureRoot : ResourceRoot;
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureSearchRoot });

            for (var i = 0; i < textureGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                if (ShouldSkipTerrainTexture(path) || !TryClassifyTerrainSurface(path, out var role))
                {
                    continue;
                }

                if (selectedTextures.ContainsKey(role))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    selectedTextures.Add(role, texture);
                }
            }

            AddSurfaceIfFound(surfaces, selectedTextures, TerrainSurfaceRole.Shore, "Asset Shore", new Color(0.55f, 0.48f, 0.31f, 1f), 8f);
            AddSurfaceIfFound(surfaces, selectedTextures, TerrainSurfaceRole.Ground, "Asset Ground", new Color(0.18f, 0.36f, 0.18f, 1f), 10f);
            AddSurfaceIfFound(surfaces, selectedTextures, TerrainSurfaceRole.Rock, "Asset Rock", new Color(0.36f, 0.36f, 0.34f, 1f), 9f);
            AddSurfaceIfFound(surfaces, selectedTextures, TerrainSurfaceRole.Highland, "Asset Highland", new Color(0.54f, 0.56f, 0.50f, 1f), 11f);

            return surfaces;
        }

        private static void EnsureFristyTerrainLayers()
        {
            var terrainLayerFolder = $"{ResourceTerrainLayerRoot}/Terrain";
            EnsureFolder(terrainLayerFolder);

            CreateOrUpdateTerrainLayerAsset(
                $"{terrainLayerFolder}/Fristy_Terrain_Mud.terrainlayer",
                $"{ResourceTextureRoot}/Terrain/Fristy_Terrain_Dark_Sand.png",
                $"{ResourceTextureRoot}/Terrain/Fristy_Terrain_Soil_04_Normal.png",
                9f,
                0.42f);
            CreateOrUpdateTerrainLayerAsset(
                $"{terrainLayerFolder}/Fristy_Terrain_Grass.terrainlayer",
                $"{ResourceTextureRoot}/Grass/Fristy_Grass_AlbedoFinal_02.png",
                $"{ResourceTextureRoot}/Grass/Fristy_Grass_Normal.png",
                7f,
                0.26f);
            CreateOrUpdateTerrainLayerAsset(
                $"{terrainLayerFolder}/Fristy_Terrain_Grass_01.terrainlayer",
                $"{ResourceTextureRoot}/Grass/Fristy_Grass_02.psd",
                $"{ResourceTextureRoot}/Grass/Fristy_Grass_Normal.png",
                5.5f,
                0.22f);
            CreateOrUpdateTerrainLayerAsset(
                $"{terrainLayerFolder}/Fristy_Terrain_Rock.terrainlayer",
                $"{ResourceTextureRoot}/Rocks/Fristy_Rock_T_D_02.png",
                $"{ResourceTextureRoot}/Rocks/Fristy_Rock_T_N_02.png",
                8f,
                0.35f);
            CreateOrUpdateTerrainLayerAsset(
                $"{terrainLayerFolder}/Fristy_Terrain_Soil.terrainlayer",
                $"{ResourceTextureRoot}/Terrain/Fristy_Terrain_Soil_And_Rocks_Albedo.png",
                $"{ResourceTextureRoot}/Terrain/Fristy_Terrain_Soil_And_Rocks_Normal.png",
                12f,
                0.31f);
        }

        private static TerrainLayer CreateOrUpdateTerrainLayerAsset(
            string layerPath,
            string diffusePath,
            string normalPath,
            float tileSize,
            float smoothness)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer
                {
                    name = Path.GetFileNameWithoutExtension(layerPath)
                };
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (diffuse != null)
            {
                layer.diffuseTexture = diffuse;
            }

            if (normal != null)
            {
                layer.normalMapTexture = normal;
            }

            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.metallic = 0f;
            layer.smoothness = smoothness;
            layer.normalScale = normal != null ? 0.75f : 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static bool TryAddLocalTerrainLayer(
            List<TerrainSurfaceDefinition> surfaces,
            TerrainSurfaceRole role,
            string surfaceName,
            string layerNameWithoutExtension,
            Color fallbackColor,
            float tileSize)
        {
            var layerPath = $"{ResourceTerrainLayerRoot}/Terrain/{layerNameWithoutExtension}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                return false;
            }

            var definition = new TerrainSurfaceDefinition();
            definition.Configure(role, surfaceName, layer, fallbackColor, tileSize);
            surfaces.Add(definition);
            return true;
        }

        private static void AddSurfaceIfFound(
            List<TerrainSurfaceDefinition> surfaces,
            Dictionary<TerrainSurfaceRole, Texture2D> selectedTextures,
            TerrainSurfaceRole role,
            string surfaceName,
            Color fallbackColor,
            float tileSize)
        {
            if (HasSurface(surfaces, role))
            {
                return;
            }

            if (!selectedTextures.TryGetValue(role, out var texture) || texture == null)
            {
                return;
            }

            var terrainLayer = CreateOrUpdateTerrainLayer(role, texture, tileSize);
            var definition = new TerrainSurfaceDefinition();
            definition.Configure(role, surfaceName, terrainLayer, fallbackColor, tileSize);
            surfaces.Add(definition);
        }

        private static bool HasSurface(IReadOnlyList<TerrainSurfaceDefinition> surfaces, TerrainSurfaceRole role)
        {
            for (var i = 0; i < surfaces.Count; i++)
            {
                if (surfaces[i] != null && surfaces[i].Role == role)
                {
                    return true;
                }
            }

            return false;
        }

        private static TerrainLayer CreateOrUpdateTerrainLayer(TerrainSurfaceRole role, Texture2D diffuseTexture, float tileSize)
        {
            var layerPath = $"{GeneratedLayerFolder}/{role} Terrain Layer.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = diffuseTexture;
            layer.tileSize = new Vector2(tileSize, tileSize);
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static List<ProceduralPropCategoryDefinition> BuildPropCategories()
        {
            var buckets = new Dictionary<ProceduralPropRole, List<GameObject>>
            {
                { ProceduralPropRole.ForestCoreTrees, new List<GameObject>() },
                { ProceduralPropRole.ForestAccentTrees, new List<GameObject>() },
                { ProceduralPropRole.Bushes, new List<GameObject>() },
                { ProceduralPropRole.GroundGrass, new List<GameObject>() },
                { ProceduralPropRole.GroundPlants, new List<GameObject>() },
                { ProceduralPropRole.RocksSmallMedium, new List<GameObject>() },
                { ProceduralPropRole.RocksLarge, new List<GameObject>() },
                { ProceduralPropRole.Cliff, new List<GameObject>() },
                { ProceduralPropRole.ShorePlants, new List<GameObject>() },
                { ProceduralPropRole.Log, new List<GameObject>() }
            };

            var prefabSearchRoot = AssetDatabase.IsValidFolder(ResourcePrefabRoot) ? ResourcePrefabRoot : ResourceRoot;
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabSearchRoot });
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (ShouldSkipProp(path) || !TryClassifyProp(path, out var role))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    buckets[role].Add(prefab);
                }
            }

            var categories = new List<ProceduralPropCategoryDefinition>();
            AddPropCategory(categories, buckets, ProceduralPropRole.ForestCoreTrees, "ForestCoreTrees", 9.4f, 5.1f, new Vector2(0f, 36f), new Vector2(0.9f, 1.28f), 620f, 11f);
            AddPropCategory(categories, buckets, ProceduralPropRole.ForestAccentTrees, "ForestAccentTrees", 0.22f, 8.5f, new Vector2(0f, 30f), new Vector2(0.85f, 1.12f), 620f, 8f, false);
            AddPropCategory(categories, buckets, ProceduralPropRole.Bushes, "Bushes", 4.6f, 3.6f, new Vector2(0f, 30f), new Vector2(0.75f, 1.3f), 260f, 8f);
            AddPropCategory(categories, buckets, ProceduralPropRole.GroundGrass, "GroundGrass", 34f, 1.15f, new Vector2(0f, 26f), new Vector2(0.78f, 1.35f), 115f, 8f);
            AddPropCategory(categories, buckets, ProceduralPropRole.GroundPlants, "GroundPlants", 7.5f, 2.1f, new Vector2(0f, 28f), new Vector2(0.65f, 1.25f), 170f, 8f);
            AddPropCategory(categories, buckets, ProceduralPropRole.RocksSmallMedium, "RocksSmallMedium", 4.8f, 5.2f, new Vector2(2f, 62f), new Vector2(0.72f, 1.45f), 420f, 10f);
            AddPropCategory(categories, buckets, ProceduralPropRole.RocksLarge, "RocksLarge", 1.25f, 13f, new Vector2(4f, 66f), new Vector2(0.9f, 1.85f), 560f, 10f);
            AddPropCategory(categories, buckets, ProceduralPropRole.Cliff, "Cliffs", 0.72f, 24f, new Vector2(22f, 82f), new Vector2(0.95f, 2.25f), 800f, 13f);
            AddPropCategory(categories, buckets, ProceduralPropRole.ShorePlants, "ShorePlants", 4.8f, 3.2f, new Vector2(0f, 24f), new Vector2(0.72f, 1.22f), 210f, 9f);
            AddPropCategory(categories, buckets, ProceduralPropRole.Log, "LogsAndBranches", 1.05f, 6.5f, new Vector2(0f, 24f), new Vector2(0.82f, 1.35f), 280f, 8f);
            LogPropCategorySummary(buckets);
            return categories;
        }

        private static List<TerrainDetailDefinition> BuildTerrainDetails()
        {
            var details = new List<TerrainDetailDefinition>();
            AddGrassTextureDetails(details);
            var prefabSearchRoot = AssetDatabase.IsValidFolder(ResourcePrefabRoot) ? ResourcePrefabRoot : ResourceRoot;
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabSearchRoot });
            var grassMeshCount = 0;
            var flowerCount = 0;
            var plantCount = 0;

            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (ShouldSkipDetail(path) || !TryClassifyTerrainDetail(path, out var role))
                {
                    continue;
                }

                if ((role == TerrainDetailRole.Grass && grassMeshCount >= 3) ||
                    (role == TerrainDetailRole.Flowers && flowerCount >= 2) ||
                    (role == TerrainDetailRole.LowPlants && plantCount >= 3))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    continue;
                }

                var definition = new TerrainDetailDefinition();
                var name = Path.GetFileNameWithoutExtension(path);
                switch (role)
                {
                    case TerrainDetailRole.Grass:
                        definition.ConfigureMesh(
                            role,
                            name,
                            prefab,
                            4.2f,
                            new Vector2(0.68f, 1.22f),
                            new Vector2(0.62f, 1.18f),
                            new Color(0.24f, 0.48f, 0.17f, 1f),
                            new Color(0.32f, 0.28f, 0.13f, 1f),
                            0.38f);
                        grassMeshCount++;
                        break;
                    case TerrainDetailRole.Flowers:
                        definition.ConfigureMesh(
                            role,
                            name,
                            prefab,
                            1.4f,
                            new Vector2(0.65f, 1.2f),
                            new Vector2(0.55f, 1.0f),
                            new Color(0.42f, 0.58f, 0.24f, 1f),
                            new Color(0.48f, 0.38f, 0.18f, 1f),
                            0.65f);
                        flowerCount++;
                        break;
                    case TerrainDetailRole.LowPlants:
                        definition.ConfigureMesh(
                            role,
                            name,
                            prefab,
                            1.35f,
                            new Vector2(0.7f, 1.35f),
                            new Vector2(0.55f, 1.15f),
                            new Color(0.18f, 0.38f, 0.15f, 1f),
                            new Color(0.34f, 0.29f, 0.14f, 1f),
                            0.52f);
                        plantCount++;
                        break;
                }

                details.Add(definition);
            }

            return details;
        }

        private static int AddGrassTextureDetails(List<TerrainDetailDefinition> details)
        {
            if (!AssetDatabase.IsValidFolder(ResourceGrassTextureRoot))
            {
                return 0;
            }

            var count = 0;
            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ResourceGrassTextureRoot });
            for (var i = 0; i < textureGuids.Length && count < 3; i++)
            {
                var texturePath = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                var textureKey = Path.GetFileNameWithoutExtension(texturePath).ToLowerInvariant();
                if (ShouldSkipTerrainTexture(texturePath) || !ContainsAny(textureKey, "grass", "albedo", "diffuse"))
                {
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (texture == null)
                {
                    continue;
                }

                var definition = new TerrainDetailDefinition();
                definition.ConfigureTexture(
                    TerrainDetailRole.Grass,
                    Path.GetFileNameWithoutExtension(texturePath),
                    texture,
                    12.5f,
                    new Vector2(0.55f, 1.05f),
                    new Vector2(0.55f, 1.15f),
                    new Color(0.28f, 0.50f, 0.18f, 1f),
                    new Color(0.36f, 0.31f, 0.15f, 1f),
                    0.32f);
                details.Add(definition);
                count++;
            }

            return count;
        }

        private static void AddPropCategory(
            List<ProceduralPropCategoryDefinition> categories,
            Dictionary<ProceduralPropRole, List<GameObject>> buckets,
            ProceduralPropRole role,
            string categoryName,
            float baseDensity,
            float minDistance,
            Vector2 slopeRange,
            Vector2 scaleRange,
            float drawDistance,
            float attemptsMultiplier,
            bool isEnabled = true)
        {
            var prefabs = buckets[role];
            if (prefabs.Count == 0)
            {
                return;
            }

            var definition = new ProceduralPropCategoryDefinition();
            definition.Configure(
                role,
                categoryName,
                isEnabled,
                prefabs.ToArray(),
                baseDensity,
                minDistance,
                slopeRange,
                scaleRange,
                true,
                drawDistance,
                attemptsMultiplier);
            categories.Add(definition);
        }

        private static void LogPropCategorySummary(Dictionary<ProceduralPropRole, List<GameObject>> buckets)
        {
            Debug.Log(
                "Environment catalog rebuilt from local Fristy Resources:\n" +
                $"- ForestCoreTrees: {buckets[ProceduralPropRole.ForestCoreTrees].Count}\n" +
                $"- ForestAccentTrees: {buckets[ProceduralPropRole.ForestAccentTrees].Count}\n" +
                $"- Bushes: {buckets[ProceduralPropRole.Bushes].Count}\n" +
                $"- GroundGrass: {buckets[ProceduralPropRole.GroundGrass].Count}\n" +
                $"- GroundPlants: {buckets[ProceduralPropRole.GroundPlants].Count}\n" +
                $"- RocksSmallMedium: {buckets[ProceduralPropRole.RocksSmallMedium].Count}\n" +
                $"- RocksLarge: {buckets[ProceduralPropRole.RocksLarge].Count}\n" +
                $"- Cliffs: {buckets[ProceduralPropRole.Cliff].Count}\n" +
                $"- ShorePlants: {buckets[ProceduralPropRole.ShorePlants].Count}");
        }

        private static bool TryClassifyTerrainSurface(string assetPath, out TerrainSurfaceRole role)
        {
            var key = assetPath.ToLowerInvariant();
            if (ContainsAny(key, "shore", "sand", "beach", "dirt", "mud"))
            {
                role = TerrainSurfaceRole.Shore;
                return true;
            }

            if (ContainsAny(key, "grass", "ground", "moss", "meadow", "field", "forest_floor"))
            {
                role = TerrainSurfaceRole.Ground;
                return true;
            }

            if (ContainsAny(key, "rock", "stone", "cliff", "boulder"))
            {
                role = TerrainSurfaceRole.Rock;
                return true;
            }

            if (ContainsAny(key, "highland", "mountain", "snow", "peak", "soil"))
            {
                role = TerrainSurfaceRole.Highland;
                return true;
            }

            role = TerrainSurfaceRole.Ground;
            return false;
        }

        private static bool TryClassifyProp(string assetPath, out ProceduralPropRole role)
        {
            var key = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (ContainsAny(key, "reeds_", "cattail_", "lilypads_", "waterlily_", "shoreplant", "river"))
            {
                role = ProceduralPropRole.ShorePlants;
                return true;
            }

            if (ContainsAny(key, "branch", "log", "fallen", "stump"))
            {
                role = ProceduralPropRole.Log;
                return true;
            }

            if (key.Contains("tree") ||
                key.StartsWith("fir_") ||
                (key.StartsWith("broadleaftree_") && key.EndsWith("_green")) ||
                (key.StartsWith("willowtree_") && key.EndsWith("_green")))
            {
                role = ProceduralPropRole.ForestCoreTrees;
                return true;
            }

            if (ContainsAny(key, "cliff", "ledge", "crag", "mountain"))
            {
                role = ProceduralPropRole.Cliff;
                return true;
            }

            if (key.StartsWith("blossomtree_") ||
                (key.StartsWith("broadleaftree_") && !key.EndsWith("_green")) ||
                (key.StartsWith("willowtree_") && !key.EndsWith("_green")))
            {
                role = ProceduralPropRole.ForestAccentTrees;
                return true;
            }

            if (ContainsAny(key, "boulder", "rock_04", "rock_4", "rock_large") ||
                key.StartsWith("rock_big_") ||
                key.StartsWith("stone_big_"))
            {
                role = ProceduralPropRole.RocksLarge;
                return true;
            }

            if (key.Contains("rock") ||
                key.StartsWith("rock_medium_") ||
                key.StartsWith("rock_small_") ||
                key.StartsWith("stone_medium_") ||
                key.StartsWith("stones_"))
            {
                role = ProceduralPropRole.RocksSmallMedium;
                return true;
            }

            if (key.StartsWith("bush_") || key.Contains("bush") || key.Contains("vegetation") || key.Contains("vine") || key.Contains("ivy"))
            {
                role = ProceduralPropRole.Bushes;
                return true;
            }

            if (key.StartsWith("grass_") || key.Contains("grass"))
            {
                role = ProceduralPropRole.GroundGrass;
                return true;
            }

            if (key.StartsWith("plant_") ||
                key.Contains("plant") ||
                key.Contains("weed") ||
                key == "plants" ||
                key.StartsWith("flower") ||
                key.StartsWith("flowermeadow_"))
            {
                role = ProceduralPropRole.GroundPlants;
                return true;
            }

            role = ProceduralPropRole.GroundPlants;
            return false;
        }

        private static bool TryClassifyTerrainDetail(string assetPath, out TerrainDetailRole role)
        {
            var key = assetPath.ToLowerInvariant();
            if (ContainsAny(key, "grass"))
            {
                role = TerrainDetailRole.Grass;
                return true;
            }

            if (ContainsAny(key, "flower", "purple", "white"))
            {
                role = TerrainDetailRole.Flowers;
                return true;
            }

            if (ContainsAny(key, "plant", "weed"))
            {
                role = TerrainDetailRole.LowPlants;
                return true;
            }

            role = TerrainDetailRole.Grass;
            return false;
        }

        private static bool ShouldSkipProp(string assetPath)
        {
            var key = assetPath.ToLowerInvariant();
            var fileName = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            return key.Contains("/demo/") ||
                   key.Contains("/code related/") ||
                   fileName == "water" ||
                   ContainsAny(fileName, "butterfly", "particle", "floatingleaf", "godray", "controller", "windcontrol", "vegetationbendcontrol");
        }

        private static bool ShouldSkipDetail(string assetPath)
        {
            var key = assetPath.ToLowerInvariant();
            return key.Contains("/demo/") ||
                   key.Contains("/code related/") ||
                   ContainsAny(key, "water", "lily", "reed", "cattail", "butterfly", "particle", "floatingleaf", "godray", "tree", "rock", "stone", "cliff", "branch", "bush");
        }

        private static bool ShouldSkipTerrainTexture(string assetPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            return ContainsAny(fileName, "normal", "_n", "-n", "mask", "rough", "metal", "height", "ao", "ambient");
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            for (var i = 0; i < needles.Length; i++)
            {
                if (value.Contains(needles[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var folderName = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
