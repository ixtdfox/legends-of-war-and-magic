using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Presentation
{
    public static class WorldFeatureRuntimeSpawner
    {
        private const string SettlementCatalogResourcePath = "ProceduralGeneration/DefaultSettlementBuildingCatalog";
        private const string PoiCatalogResourcePath = "ProceduralGeneration/DefaultPointOfInterestCatalog";

        private static SettlementBuildingCatalog fallbackSettlementCatalog;
        private static PointOfInterestCatalog fallbackPoiCatalog;

        public static void Spawn(GenerationContext context)
        {
            if (context?.GeneratedRoot == null || context.WorldLayers == null)
            {
                return;
            }

            Spawn(context, WorldFeatureSpawnPlanBuilder.Build(context.WorldLayers));
        }

        public static void Spawn(GenerationContext context, WorldFeatureSpawnPlan plan)
        {
            if (context?.GeneratedRoot == null || plan == null)
            {
                return;
            }

            var root = new GameObject("GeneratedWorldFeatures").transform;
            root.SetParent(context.GeneratedRoot, false);

            SpawnSettlements(context, root, plan);
            SpawnPointsOfInterest(context, root, plan);
        }

        private static void SpawnSettlements(GenerationContext context, Transform root, WorldFeatureSpawnPlan plan)
        {
            var parent = new GameObject("Settlements").transform;
            parent.SetParent(root, false);
            var settlements = plan.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                var settlementRoot = new GameObject($"{settlement.Tier}_{settlement.Name}");
                settlementRoot.transform.SetParent(parent, false);
                settlementRoot.transform.position = ToWorld(context, settlement.WorldPosition, 0.05f);

                for (var buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    SpawnBuilding(context, settlementRoot.transform, settlement.Buildings[buildingIndex]);
                }
            }
        }

        private static void SpawnBuilding(GenerationContext context, Transform parent, SettlementBuildingSpawnInstruction building)
        {
            var prefab = ResolveSettlementPrefab(context.Settings.Settlements.BuildingCatalog, building.PrefabKey);
            var groundY = ResolveSettlementGroundY(context, building.WorldPosition);
            var position = new Vector3(building.WorldPosition.x, groundY + 0.03f, building.WorldPosition.y);
            var rotation = Quaternion.Euler(0f, building.RotationDegrees, 0f);
            if (prefab == null)
            {
                UnityEngine.Debug.LogWarning($"Missing settlement prefab '{building.PrefabKey}'. Rebuild world feature prototype prefabs.");
                return;
            }

            var instance = Object.Instantiate(prefab, parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.name = building.Id;
            AlignBottomToTerrain(instance, position.y + 0.02f);

            var metadata = instance.GetComponent<SettlementBuildingPrefabMetadata>();
            if (metadata == null)
            {
                metadata = instance.AddComponent<SettlementBuildingPrefabMetadata>();
            }

            metadata.Configure(
                building.Type,
                building.Level,
                building.FootprintSize,
                building.PrefabKey);
        }

        private static void SpawnPointsOfInterest(GenerationContext context, Transform root, WorldFeatureSpawnPlan plan)
        {
            var parent = new GameObject("PointsOfInterest").transform;
            parent.SetParent(root, false);
            var points = plan.PointsOfInterest;
            for (var i = 0; i < points.Count; i++)
            {
                var poi = points[i];
                var prefab = ResolvePoiPrefab(context.Settings.PointsOfInterest.Catalog, poi.PrefabKey);
                var position = new Vector3(
                    poi.WorldPosition.x,
                    ResolveGroundY(context, poi.WorldPosition) + 0.04f,
                    poi.WorldPosition.y);
                if (prefab == null)
                {
                    UnityEngine.Debug.LogWarning($"Missing point of interest prefab '{poi.PrefabKey}'. Rebuild world feature prototype prefabs.");
                    continue;
                }

                var instance = Object.Instantiate(prefab, parent);
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                instance.name = poi.Id;
                AlignBottomToTerrain(instance, position.y + 0.02f);

                var metadata = instance.GetComponent<PointOfInterestPrefabMetadata>();
                if (metadata == null)
                {
                    metadata = instance.AddComponent<PointOfInterestPrefabMetadata>();
                }

                metadata.Configure(poi.Type, poi.DangerLevel, poi.Radius, poi.PrefabKey, string.Join(",", poi.Tags));
            }
        }

        private static GameObject ResolveSettlementPrefab(SettlementBuildingCatalog catalog, string prefabKey)
        {
            if (string.IsNullOrWhiteSpace(prefabKey))
            {
                return null;
            }

            catalog ??= ResolveFallbackSettlementCatalog();
            if (catalog == null)
            {
                return null;
            }

            var buildings = catalog.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var entry = buildings[i];
                if (entry != null && entry.PrefabKey == prefabKey)
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

        private static GameObject ResolvePoiPrefab(PointOfInterestCatalog catalog, string prefabKey)
        {
            if (string.IsNullOrWhiteSpace(prefabKey))
            {
                return null;
            }

            catalog ??= ResolveFallbackPoiCatalog();
            if (catalog == null)
            {
                return null;
            }

            var entries = catalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry != null && entry.PrefabKey == prefabKey)
                {
                    return entry.Prefab;
                }
            }

            return null;
        }

        private static SettlementBuildingCatalog ResolveFallbackSettlementCatalog()
        {
            if (fallbackSettlementCatalog == null)
            {
                fallbackSettlementCatalog = Resources.Load<SettlementBuildingCatalog>(SettlementCatalogResourcePath);
            }

            return fallbackSettlementCatalog;
        }

        private static PointOfInterestCatalog ResolveFallbackPoiCatalog()
        {
            if (fallbackPoiCatalog == null)
            {
                fallbackPoiCatalog = Resources.Load<PointOfInterestCatalog>(PoiCatalogResourcePath);
            }

            return fallbackPoiCatalog;
        }

        private static Vector3 ToWorld(GenerationContext context, Vector2 position, float verticalOffset)
        {
            if (TrySampleGeneratedTerrainHeight(context, position, out var terrainHeight))
            {
                return new Vector3(position.x, terrainHeight + verticalOffset, position.y);
            }

            if (context.TerrainSampler != null && context.TerrainSampler.TrySample(position.x, position.y, out var point, out _))
            {
                return point + Vector3.up * verticalOffset;
            }

            return new Vector3(position.x, verticalOffset, position.y);
        }

        private static float ResolveSettlementGroundY(GenerationContext context, Vector2 position)
        {
            if (TrySampleGeneratedTerrainHeight(context, position, out var terrainHeight))
            {
                return terrainHeight;
            }

            if (context?.TerrainSampler == null)
            {
                return ResolveGroundY(context, position);
            }

            var sampler = new SettlementAdjustedTerrainSampler(
                context.TerrainSampler,
                context.Settings,
                context.WorldLayers?.Settlements);
            return sampler.SampleHeightMeters(position.x, position.y);
        }

        private static float ResolveGroundY(GenerationContext context, Vector2 position)
        {
            if (TrySampleGeneratedTerrainHeight(context, position, out var terrainHeight))
            {
                return terrainHeight;
            }

            return context?.TerrainSampler != null &&
                   context.TerrainSampler.TrySample(position.x, position.y, out var point, out _)
                ? point.y
                : 0f;
        }

        private static bool TrySampleGeneratedTerrainHeight(GenerationContext context, Vector2 position, out float height)
        {
            height = 0f;
            if (context == null)
            {
                return false;
            }

            var terrains = context.GeneratedTerrains;
            for (var i = 0; i < terrains.Count; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                {
                    continue;
                }

                var terrainPosition = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (position.x < terrainPosition.x ||
                    position.x > terrainPosition.x + size.x ||
                    position.y < terrainPosition.z ||
                    position.y > terrainPosition.z + size.z)
                {
                    continue;
                }

                height = terrain.SampleHeight(new Vector3(position.x, 0f, position.y)) + terrainPosition.y;
                return true;
            }

            return false;
        }

        private static void AlignBottomToTerrain(GameObject instance, float targetGroundY)
        {
            if (instance == null)
            {
                return;
            }

            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            var minY = float.MaxValue;
            for (var i = 0; i < renderers.Length; i++)
            {
                minY = Mathf.Min(minY, renderers[i].bounds.min.y);
            }

            if (minY < float.MaxValue)
            {
                instance.transform.position += Vector3.up * (targetGroundY - minY);
            }
        }
    }
}
