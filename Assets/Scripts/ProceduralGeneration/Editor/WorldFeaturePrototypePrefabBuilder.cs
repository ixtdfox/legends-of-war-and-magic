#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor
{
    public static class WorldFeaturePrototypePrefabBuilder
    {
        private const string SettlementPrefabDirectory = "Assets/Models/Prefab/Generated/Settlements";
        private const string PoiPrefabDirectory = "Assets/Models/Prefab/Generated/POI";
        private const string MaterialDirectory = "Assets/Models/Prefab/Generated/Materials/WorldFeatures";
        private const string SettlementCatalogPath = "Assets/Resources/ProceduralGeneration/DefaultSettlementBuildingCatalog.asset";
        private const string PoiCatalogPath = "Assets/Resources/ProceduralGeneration/DefaultPointOfInterestCatalog.asset";

        [MenuItem("Tools/Legends of War and Magic/Procedural Generation/Rebuild World Feature Prototype Prefabs")]
        public static void BuildAll()
        {
            EnsureDirectory(SettlementPrefabDirectory);
            EnsureDirectory(PoiPrefabDirectory);
            EnsureDirectory(MaterialDirectory);

            var materials = BuildMaterials();
            BuildSettlementPrefabs(materials);
            BuildPoiPrefabs(materials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("World feature prototype prefabs rebuilt.");
        }

        private static void BuildSettlementPrefabs(MaterialSet materials)
        {
            CreateBuilding("Tent_Level1", BuildingType.Tent, 1, new Vector2(5f, 4f), materials.Cloth, root =>
            {
                AddBox(root, "Tent Body", new Vector3(0f, 0.75f, 0f), new Vector3(4.4f, 1.5f, 3.2f), materials.Cloth);
                AddBox(root, "Tent Ridge", new Vector3(0f, 1.75f, 0f), new Vector3(4.8f, 0.45f, 3.8f), materials.Roof, new Vector3(0f, 0f, 45f));
            });
            CreateBuilding("Campfire_Level1", BuildingType.Campfire, 1, new Vector2(4f, 4f), materials.Fire, root =>
            {
                AddCylinder(root, "Fire", new Vector3(0f, 0.55f, 0f), 0.65f, 1.1f, materials.Fire);
                AddBox(root, "Logs A", new Vector3(0f, 0.18f, 0f), new Vector3(3f, 0.28f, 0.45f), materials.Wood, new Vector3(0f, 35f, 0f));
                AddBox(root, "Logs B", new Vector3(0f, 0.2f, 0f), new Vector3(3f, 0.28f, 0.45f), materials.Wood, new Vector3(0f, -35f, 0f));
            });
            CreateBuilding("CratePile_Level1", BuildingType.Storage, 1, new Vector2(5f, 4f), materials.Wood, root =>
            {
                AddBox(root, "Crate A", new Vector3(-0.9f, 0.55f, 0f), new Vector3(1.2f, 1.1f, 1.2f), materials.Wood);
                AddBox(root, "Crate B", new Vector3(0.45f, 0.45f, 0.25f), new Vector3(1f, 0.9f, 1f), materials.Wood);
                AddBox(root, "Crate C", new Vector3(0.1f, 1.25f, -0.35f), new Vector3(0.9f, 0.9f, 0.9f), materials.Wood);
            });
            CreateBuilding("SimpleFence_Level1", BuildingType.Wall, 1, new Vector2(6f, 2f), materials.Wood, root => AddFence(root, 6f, 1.5f, materials.Wood));

            CreateBuilding("SmallHouse_Level1", BuildingType.House, 1, new Vector2(8f, 8f), materials.Wood, root => AddHouse(root, 7f, 6f, 3.2f, materials.Wood, materials.Roof));
            CreateBuilding("Longhouse_Level1", BuildingType.Longhouse, 1, new Vector2(13f, 8f), materials.Wood, root => AddHouse(root, 12f, 6f, 3.4f, materials.Wood, materials.Roof));
            CreateBuilding("Well_Level1", BuildingType.Well, 1, new Vector2(5f, 5f), materials.Stone, root =>
            {
                AddCylinder(root, "Well Ring", new Vector3(0f, 0.55f, 0f), 1.25f, 1.1f, materials.Stone);
                AddBox(root, "Well Beam", new Vector3(0f, 2.2f, 0f), new Vector3(3.2f, 0.28f, 0.35f), materials.Wood);
                AddBox(root, "Well Post A", new Vector3(-1.35f, 1.3f, 0f), new Vector3(0.28f, 2.4f, 0.28f), materials.Wood);
                AddBox(root, "Well Post B", new Vector3(1.35f, 1.3f, 0f), new Vector3(0.28f, 2.4f, 0.28f), materials.Wood);
            });
            CreateBuilding("Barn_Level1", BuildingType.Farm, 1, new Vector2(14f, 10f), materials.Wood, root => AddHouse(root, 12f, 8f, 4f, materials.Wood, materials.Roof));
            CreateBuilding("Storage_Level1", BuildingType.Storage, 1, new Vector2(9f, 8f), materials.Wood, root => AddHouse(root, 8f, 6f, 3.5f, materials.Wood, materials.Roof));
            CreateBuilding("Blacksmith_Level1", BuildingType.Blacksmith, 1, new Vector2(10f, 9f), materials.Stone, root =>
            {
                AddHouse(root, 8f, 6f, 3.4f, materials.Stone, materials.Roof);
                AddBox(root, "Forge Chimney", new Vector3(2.2f, 4.2f, 0.8f), new Vector3(1f, 3f, 1f), materials.Stone);
                AddBox(root, "Anvil", new Vector3(-2.8f, 0.45f, -2.9f), new Vector3(1.1f, 0.55f, 0.7f), materials.Metal);
            });
            CreateBuilding("WoodenWatchtower_Level1", BuildingType.Watchtower, 1, new Vector2(6f, 6f), materials.Wood, root => AddWatchtower(root, materials.Wood, materials.Roof));
            CreateBuilding("WoodenFence_Level1", BuildingType.Wall, 1, new Vector2(8f, 2f), materials.Wood, root => AddFence(root, 8f, 2f, materials.Wood));
            CreateBuilding("Shrine_Level1", BuildingType.Shrine, 1, new Vector2(7f, 7f), materials.Stone, root =>
            {
                AddBox(root, "Shrine Base", new Vector3(0f, 0.25f, 0f), new Vector3(4.2f, 0.5f, 4.2f), materials.Stone);
                AddCylinder(root, "Shrine Stone", new Vector3(0f, 1.55f, 0f), 0.65f, 2.6f, materials.Stone);
                AddBox(root, "Offering Table", new Vector3(0f, 0.55f, -2.3f), new Vector3(2.2f, 0.55f, 0.8f), materials.Wood);
            });

            CreateBuilding("House_Level2", BuildingType.House, 2, new Vector2(10f, 9f), materials.Wood, root => AddHouse(root, 9f, 7f, 4.2f, materials.Wood, materials.Roof));
            CreateBuilding("Tavern_Level1", BuildingType.Tavern, 1, new Vector2(13f, 10f), materials.Wood, root =>
            {
                AddHouse(root, 12f, 8f, 4.5f, materials.Wood, materials.Roof);
                AddBox(root, "Sign", new Vector3(-6.2f, 2.5f, -2.2f), new Vector3(0.25f, 1.2f, 1.6f), materials.Cloth);
            });
            CreateBuilding("MarketStall_Level1", BuildingType.Market, 1, new Vector2(16f, 14f), materials.Cloth, root =>
            {
                AddBox(root, "Stall Table", new Vector3(0f, 0.55f, 0f), new Vector3(6f, 1.1f, 2.4f), materials.Wood);
                AddBox(root, "Awning", new Vector3(0f, 2.3f, 0f), new Vector3(7f, 0.35f, 3.6f), materials.Cloth);
                AddBox(root, "Goods", new Vector3(0f, 1.25f, 0f), new Vector3(4.8f, 0.45f, 1.4f), materials.Fire);
            });
            CreateBuilding("Blacksmith_Level2", BuildingType.Blacksmith, 2, new Vector2(12f, 10f), materials.Stone, root => AddForge(root, materials));
            CreateBuilding("Workshop_Level1", BuildingType.Workshop, 1, new Vector2(11f, 10f), materials.Wood, root => AddHouse(root, 10f, 8f, 4f, materials.Wood, materials.Roof));
            CreateBuilding("TownHall_Level1", BuildingType.TownHall, 1, new Vector2(16f, 12f), materials.Wood, root => AddTownHall(root, 14f, 9f, materials.Wood, materials.Roof));
            CreateBuilding("WoodenGate_Level1", BuildingType.Gate, 1, new Vector2(12f, 5f), materials.Wood, root => AddGate(root, 10f, 5.5f, materials.Wood));
            CreateBuilding("PalisadeWall_Level1", BuildingType.Wall, 2, new Vector2(10f, 3f), materials.Wood, root => AddFence(root, 10f, 3f, materials.Wood));

            CreateBuilding("StoneHouse_Level1", BuildingType.House, 3, new Vector2(11f, 10f), materials.Stone, root => AddHouse(root, 10f, 8f, 4.8f, materials.Stone, materials.Roof));
            CreateBuilding("Temple_Level1", BuildingType.Temple, 1, new Vector2(18f, 14f), materials.Stone, root => AddTemple(root, materials.Stone, materials.Roof));
            CreateBuilding("Barracks_Level1", BuildingType.Barracks, 1, new Vector2(16f, 12f), materials.Stone, root => AddHouse(root, 15f, 9f, 4.4f, materials.Stone, materials.Roof));
            CreateBuilding("CityGate_Level1", BuildingType.Gate, 2, new Vector2(16f, 6f), materials.Stone, root => AddGate(root, 14f, 7f, materials.Stone));
            CreateBuilding("StoneWall_Level1", BuildingType.Wall, 3, new Vector2(12f, 4f), materials.Stone, root => AddBox(root, "Stone Wall", new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 3f), materials.Stone));
            CreateBuilding("TownHall_Level2", BuildingType.TownHall, 2, new Vector2(22f, 16f), materials.Stone, root => AddTownHall(root, 19f, 12f, materials.Stone, materials.Roof));
            CreateBuilding("MarketSquare_Level1", BuildingType.Market, 2, new Vector2(22f, 18f), materials.Stone, root =>
            {
                AddBox(root, "Market Platform", new Vector3(0f, 0.16f, 0f), new Vector3(18f, 0.32f, 14f), materials.Stone);
                AddBox(root, "Central Stall", new Vector3(0f, 1.5f, 0f), new Vector3(8f, 2f, 4f), materials.Wood);
            });
        }

        private static void BuildPoiPrefabs(MaterialSet materials)
        {
            CreatePoi("CaveEntrance_Level1", PointOfInterestType.CaveEntrance, 3, 18f, materials.Stone, root =>
            {
                AddBox(root, "Cave Back", new Vector3(0f, 2f, 1.2f), new Vector3(8f, 4f, 2f), materials.Stone);
                AddBox(root, "Cave Mouth", new Vector3(0f, 1.6f, -0.5f), new Vector3(5f, 3.2f, 1.4f), materials.Dark);
                AddBox(root, "Rock A", new Vector3(-3.5f, 1.2f, -0.4f), new Vector3(2.2f, 2.4f, 2.2f), materials.Stone, new Vector3(0f, 18f, 0f));
                AddBox(root, "Rock B", new Vector3(3.5f, 1.4f, -0.3f), new Vector3(2f, 2.8f, 2f), materials.Stone, new Vector3(0f, -22f, 0f));
            });
            CreatePoi("BanditCamp_Level1", PointOfInterestType.BanditCamp, 4, 20f, materials.Wood, root =>
            {
                AddFence(root, 12f, 2f, materials.Wood);
                AddBox(root, "Bandit Tent", new Vector3(0f, 1f, 2.5f), new Vector3(4f, 2f, 3f), materials.Cloth);
                AddCylinder(root, "Campfire", new Vector3(0f, 0.45f, -2.8f), 0.65f, 0.9f, materials.Fire);
            });
            CreatePoi("Ruins_Level1", PointOfInterestType.Ruins, 3, 24f, materials.Stone, root => AddRuins(root, materials.Stone));
            CreatePoi("AncientTemple_Level1", PointOfInterestType.AncientTemple, 6, 32f, materials.Stone, root => AddTemple(root, materials.Stone, materials.Roof));
            CreatePoi("AbandonedHouse_Level1", PointOfInterestType.AbandonedHouse, 2, 16f, materials.Wood, root => AddHouse(root, 8f, 6f, 3f, materials.Wood, materials.Roof));
            CreatePoi("Shrine_Level1", PointOfInterestType.Shrine, 1, 12f, materials.Stone, root =>
            {
                AddBox(root, "Shrine Base", new Vector3(0f, 0.3f, 0f), new Vector3(4f, 0.6f, 4f), materials.Stone);
                AddCylinder(root, "Shrine Stone", new Vector3(0f, 1.6f, 0f), 0.7f, 2.6f, materials.Stone);
            });
            CreatePoi("StoneCircle_Level1", PointOfInterestType.StoneCircle, 2, 18f, materials.Stone, root =>
            {
                for (var i = 0; i < 8; i++)
                {
                    var angle = i / 8f * Mathf.PI * 2f;
                    AddBox(root, $"Standing Stone {i}", new Vector3(Mathf.Cos(angle) * 5f, 1.6f, Mathf.Sin(angle) * 5f), new Vector3(1f, 3.2f, 0.8f), materials.Stone, new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
                }
            });
            CreatePoi("Watchtower_Ruined_Level1", PointOfInterestType.Watchtower, 3, 16f, materials.Wood, root => AddWatchtower(root, materials.Wood, materials.Roof));
            CreatePoi("Graveyard_Level1", PointOfInterestType.Graveyard, 4, 20f, materials.Stone, root =>
            {
                for (var i = 0; i < 9; i++)
                {
                    AddBox(root, $"Marker {i}", new Vector3((i % 3 - 1) * 2.4f, 0.65f, (i / 3 - 1) * 2.5f), new Vector3(0.6f, 1.3f, 0.25f), materials.Stone);
                }
            });
            CreatePoi("ResourceNode_Mine_Level1", PointOfInterestType.ResourceNode, 2, 16f, materials.Stone, root =>
            {
                AddBox(root, "Mine Rock", new Vector3(0f, 1.6f, 0f), new Vector3(7f, 3.2f, 4f), materials.Stone);
                AddBox(root, "Mine Door", new Vector3(0f, 1f, -2.1f), new Vector3(3f, 2f, 0.4f), materials.Dark);
                AddBox(root, "Cart", new Vector3(3.7f, 0.55f, -2.2f), new Vector3(2f, 1.1f, 1.2f), materials.Wood);
            });
        }

        private static void CreateBuilding(string key, BuildingType type, int level, Vector2 footprint, Material material, Action<Transform> build)
        {
            var root = CreateRoot(key);
            build(root.transform);
            var metadata = root.AddComponent<SettlementBuildingPrefabMetadata>();
            metadata.Configure(type, level, footprint, key);
            AddFootprintCollider(root, footprint);
            var prefab = SavePrefab(root, $"{SettlementPrefabDirectory}/{key}.prefab");
            AssignSettlementCatalogPrefab(key, prefab);
        }

        private static void CreatePoi(string key, PointOfInterestType type, int danger, float radius, Material material, Action<Transform> build)
        {
            var root = CreateRoot(key);
            build(root.transform);
            var metadata = root.AddComponent<PointOfInterestPrefabMetadata>();
            metadata.Configure(type, danger, radius, key, type.ToString().ToLowerInvariant());
            AddFootprintCollider(root, new Vector2(radius * 1.25f, radius * 1.25f));
            var prefab = SavePrefab(root, $"{PoiPrefabDirectory}/{key}.prefab");
            AssignPoiCatalogPrefab(key, prefab);
        }

        private static GameObject CreateRoot(string name)
        {
            var root = new GameObject(name);
            root.transform.position = Vector3.zero;
            return root;
        }

        private static void AddHouse(Transform root, float width, float depth, float height, Material wall, Material roof)
        {
            AddBox(root, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), wall);
            AddBox(root, "Roof", new Vector3(0f, height + 0.9f, 0f), new Vector3(width + 0.8f, 1.4f, depth + 0.9f), roof, new Vector3(0f, 0f, 45f));
            AddBox(root, "Door", new Vector3(0f, 1.05f, -depth * 0.51f), new Vector3(1.2f, 2.1f, 0.18f), RuntimeWorldFeatureMaterials.PrototypeMaterial("Generated Door", new Color(0.16f, 0.10f, 0.06f)));
        }

        private static void AddTownHall(Transform root, float width, float depth, Material wall, Material roof)
        {
            AddHouse(root, width, depth, 5.2f, wall, roof);
            AddBox(root, "Tower", new Vector3(0f, 7.2f, 1.8f), new Vector3(4.2f, 5.2f, 4.2f), wall);
            AddBox(root, "Tower Roof", new Vector3(0f, 10.1f, 1.8f), new Vector3(5f, 1.2f, 5f), roof, new Vector3(0f, 0f, 45f));
        }

        private static void AddForge(Transform root, MaterialSet materials)
        {
            AddHouse(root, 10f, 7f, 4f, materials.Stone, materials.Roof);
            AddBox(root, "Large Chimney", new Vector3(2.8f, 5.6f, 1.1f), new Vector3(1.4f, 4.8f, 1.4f), materials.Stone);
            AddBox(root, "Forge Awning", new Vector3(-3f, 2.1f, -4f), new Vector3(5.2f, 0.35f, 2.5f), materials.Wood);
            AddBox(root, "Anvil", new Vector3(-3f, 0.55f, -4f), new Vector3(1.3f, 0.65f, 0.8f), materials.Metal);
        }

        private static void AddTemple(Transform root, Material stone, Material roof)
        {
            AddBox(root, "Temple Base", new Vector3(0f, 0.35f, 0f), new Vector3(15f, 0.7f, 11f), stone);
            AddBox(root, "Temple Hall", new Vector3(0f, 3.2f, 0.7f), new Vector3(11f, 5.8f, 8f), stone);
            AddBox(root, "Temple Roof", new Vector3(0f, 6.7f, 0.7f), new Vector3(12.5f, 1.6f, 9.4f), roof, new Vector3(0f, 0f, 45f));
            for (var i = -2; i <= 2; i++)
            {
                AddCylinder(root, $"Column {i}", new Vector3(i * 2.2f, 2.5f, -4.4f), 0.35f, 5f, stone);
            }
        }

        private static void AddWatchtower(Transform root, Material wood, Material roof)
        {
            for (var x = -1; x <= 1; x += 2)
            {
                for (var z = -1; z <= 1; z += 2)
                {
                    AddBox(root, $"Post {x}_{z}", new Vector3(x * 1.7f, 3f, z * 1.7f), new Vector3(0.35f, 6f, 0.35f), wood);
                }
            }

            AddBox(root, "Platform", new Vector3(0f, 6.2f, 0f), new Vector3(4.8f, 0.45f, 4.8f), wood);
            AddBox(root, "Cabin", new Vector3(0f, 7.5f, 0f), new Vector3(3.6f, 2.2f, 3.6f), wood);
            AddBox(root, "Roof", new Vector3(0f, 8.9f, 0f), new Vector3(4.5f, 0.9f, 4.5f), roof, new Vector3(0f, 0f, 45f));
        }

        private static void AddGate(Transform root, float width, float height, Material material)
        {
            AddBox(root, "Left Tower", new Vector3(-width * 0.36f, height * 0.5f, 0f), new Vector3(2.2f, height, 3f), material);
            AddBox(root, "Right Tower", new Vector3(width * 0.36f, height * 0.5f, 0f), new Vector3(2.2f, height, 3f), material);
            AddBox(root, "Lintel", new Vector3(0f, height - 0.6f, 0f), new Vector3(width, 1.2f, 2.6f), material);
        }

        private static void AddFence(Transform root, float width, float height, Material material)
        {
            AddBox(root, "Rail", new Vector3(0f, height * 0.55f, 0f), new Vector3(width, 0.35f, 0.35f), material);
            var postCount = Mathf.Max(2, Mathf.RoundToInt(width / 2f));
            for (var i = 0; i < postCount; i++)
            {
                var x = Mathf.Lerp(-width * 0.5f, width * 0.5f, i / (float)(postCount - 1));
                AddBox(root, $"Post {i}", new Vector3(x, height * 0.5f, 0f), new Vector3(0.28f, height, 0.28f), material);
            }
        }

        private static void AddRuins(Transform root, Material stone)
        {
            AddBox(root, "Broken Wall A", new Vector3(-2.5f, 1.4f, 0f), new Vector3(1.2f, 2.8f, 7f), stone, new Vector3(0f, -12f, 0f));
            AddBox(root, "Broken Wall B", new Vector3(2.8f, 1.1f, 1f), new Vector3(1.1f, 2.2f, 5f), stone, new Vector3(0f, 15f, 0f));
            AddBox(root, "Floor", new Vector3(0f, 0.15f, 0f), new Vector3(8f, 0.3f, 8f), stone);
            AddBox(root, "Rubble", new Vector3(0.7f, 0.55f, -2.6f), new Vector3(3f, 0.9f, 1.5f), stone, new Vector3(0f, 28f, 0f));
        }

        private static void AddBox(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, Vector3 euler = default)
        {
            var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            var go = mesh.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(euler);
            AssignMaterial(go, material);
            mesh.ToMesh();
            mesh.Refresh();
        }

        private static void AddCylinder(Transform parent, string name, Vector3 localPosition, float radius, float height, Material material)
        {
            var mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, radius, height, 0);
            var go = mesh.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            AssignMaterial(go, material);
            mesh.ToMesh();
            mesh.Refresh();
        }

        private static void AssignMaterial(GameObject go, Material material)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void AddFootprintCollider(GameObject root, Vector2 footprint)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.size = new Vector3(Mathf.Max(0.5f, footprint.x), 2f, Mathf.Max(0.5f, footprint.y));
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static MaterialSet BuildMaterials()
        {
            return new MaterialSet(
                Material("WorldFeature_Wood", new Color(0.45f, 0.28f, 0.14f)),
                Material("WorldFeature_Stone", new Color(0.46f, 0.45f, 0.39f)),
                Material("WorldFeature_Cloth", new Color(0.62f, 0.55f, 0.42f)),
                Material("WorldFeature_Roof", new Color(0.35f, 0.12f, 0.08f)),
                Material("WorldFeature_Fire", new Color(0.95f, 0.36f, 0.08f)),
                Material("WorldFeature_Metal", new Color(0.22f, 0.22f, 0.24f)),
                Material("WorldFeature_Dark", new Color(0.05f, 0.045f, 0.04f)));
        }

        private static Material Material(string name, Color color)
        {
            var path = $"{MaterialDirectory}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = name;
            material.color = color;
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.18f);
            }

            if (material.HasProperty("_SpecColor"))
            {
                material.SetColor("_SpecColor", new Color(0.025f, 0.022f, 0.018f, 1f));
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignSettlementCatalogPrefab(string prefabKey, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<SettlementBuildingCatalog>(SettlementCatalogPath);
            if (catalog == null)
            {
                return;
            }

            var entries = catalog.Buildings;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].PrefabKey == prefabKey)
                {
                    entries[i].ConfigurePrefab(prefab);
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static void AssignPoiCatalogPrefab(string prefabKey, GameObject prefab)
        {
            if (prefab == null)
            {
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<PointOfInterestCatalog>(PoiCatalogPath);
            if (catalog == null)
            {
                return;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].PrefabKey == prefabKey)
                {
                    entries[i].ConfigurePrefab(prefab);
                }
            }

            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureDirectory(string directory)
        {
            var parts = directory.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private readonly struct MaterialSet
        {
            public MaterialSet(Material wood, Material stone, Material cloth, Material roof, Material fire, Material metal, Material dark)
            {
                Wood = wood;
                Stone = stone;
                Cloth = cloth;
                Roof = roof;
                Fire = fire;
                Metal = metal;
                Dark = dark;
            }

            public Material Wood { get; }
            public Material Stone { get; }
            public Material Cloth { get; }
            public Material Roof { get; }
            public Material Fire { get; }
            public Material Metal { get; }
            public Material Dark { get; }
        }
    }
}
#endif
