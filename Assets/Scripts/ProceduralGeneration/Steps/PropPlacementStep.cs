using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Places environment props on generated terrain using deterministic, rule-based constraints.
    /// </summary>
    public sealed class PropPlacementStep : IGenerationStep
    {
        public void Execute(GenerationContext context)
        {
            if (!context.Settings.EnablePropPlacement || context.Settings.PropCategories == null || context.Settings.PropCategories.Count == 0)
            {
                return;
            }

            var terrain = context.GeneratedTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("Prop placement skipped because no generated terrain was found.");
                return;
            }

            var propsRoot = new GameObject("GeneratedProps").transform;
            propsRoot.SetParent(context.GeneratedRoot, false);

            for (var i = 0; i < context.Settings.PropCategories.Count; i++)
            {
                var random = new System.Random(unchecked(context.Seed * 486187739 + 97 + i * 104729));
                PlaceCategory(context, context.Settings.PropCategories[i], terrain, propsRoot, random);
            }
        }

        private static void PlaceCategory(
            GenerationContext context,
            PropCategoryPlacementSettings category,
            Terrain terrain,
            Transform propsRoot,
            System.Random random)
        {
            if (category == null || !category.Enabled || category.Prefabs == null || category.Prefabs.Length == 0 || category.DensityPer10kSqm <= 0f)
            {
                if (category != null && category.Enabled && category.DensityPer10kSqm > 0f && (category.Prefabs == null || category.Prefabs.Length == 0))
                {
                    Debug.LogWarning($"Prop category '{category.CategoryName}' was requested, but it has no prefabs. Terrain generation will continue without this category.");
                }

                return;
            }

            var area = context.Settings.WorldWidth * context.Settings.WorldLength;
            var targetCount = Mathf.RoundToInt((area / 10000f) * category.DensityPer10kSqm);
            if (targetCount <= 0)
            {
                return;
            }

            var categoryName = string.IsNullOrWhiteSpace(category.CategoryName) ? "Props" : category.CategoryName;
            var categoryRoot = new GameObject(categoryName).transform;
            categoryRoot.SetParent(propsRoot, false);

            var lodWarnedPrefabs = new HashSet<int>();

            var worldBounds = context.WorldBounds;
            var minX = worldBounds.min.x;
            var maxX = worldBounds.max.x;
            var minZ = worldBounds.min.z;
            var maxZ = worldBounds.max.z;

            var spatialHash = new SpatialHash2D(category.MinDistanceBetweenInstances);
            var minDistanceSqr = category.MinDistanceBetweenInstances * category.MinDistanceBetweenInstances;
            var maxAttempts = Mathf.Max(targetCount, Mathf.CeilToInt(targetCount * category.AttemptsMultiplier * ResolveAttemptBoost(category.Role)));
            var accepted = 0;

            for (var attempt = 0; attempt < maxAttempts && accepted < targetCount; attempt++)
            {
                var x = Range(random, minX, maxX);
                var z = Range(random, minZ, maxZ);

                if (!TrySamplePoint(terrain, x, z, out var point, out var normal))
                {
                    context.RecordRejected(categoryName, "TerrainSample");
                    continue;
                }

                var slope = Vector3.Angle(normal, Vector3.up);
                var slopeRange = category.AllowedSlopeRange;
                if (slope < slopeRange.x || slope > slopeRange.y)
                {
                    context.RecordRejected(categoryName, "Slope");
                    continue;
                }

                var heightRange = category.AllowedHeightRange;
                if (point.y < heightRange.x || point.y > heightRange.y)
                {
                    context.RecordRejected(categoryName, point.y <= context.Settings.WaterLevel + 0.35f ? "Water" : "Height");
                    continue;
                }

                var biomeScore = EvaluateBiomeScore(context, category, point, slope);
                if (biomeScore <= 0.01f || Next01(random) > biomeScore)
                {
                    context.RecordRejected(categoryName, "BiomeMask");
                    continue;
                }

                var point2D = new Vector2(point.x, point.z);
                if (minDistanceSqr > 0f && spatialHash.IsOverlapping(point2D, minDistanceSqr))
                {
                    context.RecordRejected(categoryName, "MinDistance");
                    continue;
                }

                var prefab = category.Prefabs[random.Next(0, category.Prefabs.Length)];
                if (prefab == null)
                {
                    context.RecordRejected(categoryName, "MissingPrefab");
                    continue;
                }

                var spawnPoint = point;
                spawnPoint.y += ResolveVerticalOffset(category.Role, random);
                var instance = Object.Instantiate(prefab, spawnPoint, Quaternion.identity, categoryRoot);
                instance.name = $"{prefab.name}_{accepted + 1:D4}";
                PrepareSpawnedInstance(instance);

                var rotationY = category.RandomYRotation ? Range(random, 0f, 360f) : 0f;
                instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

                var scaleRange = category.RandomScaleRange;
                var uniformScale = Range(random, scaleRange.x, scaleRange.y);
                instance.transform.localScale *= uniformScale;

                ValidateLodSetup(category, prefab, instance, lodWarnedPrefabs);
                ApplyDrawDistance(instance, category.MaxDrawDistance);

                spatialHash.Add(point2D);
                accepted++;
            }

            if (accepted == 0)
            {
                SafeDestroy(categoryRoot.gameObject);
            }
            else
            {
                context.RecordSpawn(categoryName, accepted);

                var summary = $"Prop category '{categoryName}' placed {accepted}/{targetCount} instances after {maxAttempts} attempts.";
                if (accepted < targetCount)
                {
                    Debug.Log(summary);
                }

                if (category.MaxDrawDistance > 0f)
                {
                    Debug.Log($"{summary} Generator-side draw distance culling enabled at {category.MaxDrawDistance:0.##} units.");
                }
            }
        }

        private static float ResolveAttemptBoost(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Tree => 1.55f,
                ProceduralPropRole.ForestCoreTrees => 1.75f,
                ProceduralPropRole.ForestAccentTrees => 2.4f,
                ProceduralPropRole.Bushes => 1.55f,
                ProceduralPropRole.GroundGrass => 1.75f,
                ProceduralPropRole.GroundPlants => 1.4f,
                ProceduralPropRole.Cliff => 2.2f,
                ProceduralPropRole.Rock => 1.8f,
                ProceduralPropRole.RocksSmallMedium => 2.0f,
                ProceduralPropRole.RocksLarge => 2.2f,
                ProceduralPropRole.ShorePlants => 2.5f,
                _ => 1.35f
            };
        }

        private static float EvaluateBiomeScore(
            GenerationContext context,
            PropCategoryPlacementSettings category,
            Vector3 point,
            float slope)
        {
            var settings = context.Settings;
            var nx = Mathf.InverseLerp(context.WorldBounds.min.x, context.WorldBounds.max.x, point.x);
            var nz = Mathf.InverseLerp(context.WorldBounds.min.z, context.WorldBounds.max.z, point.z);
            var normalizedHeight = settings.TerrainHeight > 0f ? Mathf.Clamp01(point.y / settings.TerrainHeight) : 0f;
            var shore = settings.WaterEnabled
                ? Mathf.Clamp01(1f - Mathf.Abs(point.y - settings.WaterLevel) / Mathf.Max(6f, settings.TerrainHeight * 0.07f))
                : 0f;
            var slope01 = Mathf.InverseLerp(6f, 60f, slope);
            var flat01 = 1f - SmoothRange(18f, 42f, slope);
            var high01 = SmoothRange(0.42f, 0.86f, normalizedHeight);
            var cluster = category.UseClusterPlacement
                ? SampleClusterMask(category, nx, nz, context.Seed)
                : 1f;
            var forestEdge = 1f - Mathf.Abs(cluster * 2f - 1f);
            var rockNoise = Mathf.PerlinNoise(nx * 21f + context.Seed * 0.00023f, nz * 21f + context.Seed * 0.00031f);
            var meadowNoise = Mathf.PerlinNoise(nx * 14f + context.Seed * 0.00041f, nz * 14f + context.Seed * 0.00037f);
            var groveNoise = Mathf.PerlinNoise(nx * 9.5f + context.Seed * 0.00053f, nz * 9.5f + context.Seed * 0.00059f);
            var understoryNoise = Mathf.PerlinNoise(nx * 34f + context.Seed * 0.00017f, nz * 34f + context.Seed * 0.00019f);
            var shoreBand = settings.WaterEnabled
                ? Mathf.Clamp01(1f - Mathf.Abs(point.y - (settings.WaterLevel + 0.85f)) / Mathf.Max(2.4f, settings.TerrainHeight * 0.025f))
                : 0f;

            return category.Role switch
            {
                ProceduralPropRole.Tree => Mathf.Clamp01(
                    (cluster * 0.94f + meadowNoise * 0.18f) *
                    flat01 *
                    (1f - shore * 0.22f) *
                    (1f - high01 * 0.36f) *
                    Mathf.Lerp(0.72f, 1.18f, understoryNoise)),
                ProceduralPropRole.ForestCoreTrees => Mathf.Clamp01(
                    (cluster * 0.94f + meadowNoise * 0.18f) *
                    flat01 *
                    (1f - shore * 0.22f) *
                    (1f - high01 * 0.36f) *
                    Mathf.Lerp(0.72f, 1.18f, understoryNoise)),
                ProceduralPropRole.ForestAccentTrees => Mathf.Clamp01(
                    SmoothRange(0.48f, 0.84f, groveNoise) *
                    Mathf.Pow(cluster, 0.65f) *
                    flat01 *
                    (1f - shore * 0.25f) *
                    (1f - high01 * 0.28f)),
                ProceduralPropRole.Bushes => Mathf.Clamp01(
                    flat01 *
                    (cluster * 0.42f + forestEdge * category.ForestEdgeAffinity * 0.22f + understoryNoise * 0.34f) *
                    (1f - shore * 0.08f) *
                    (1f - high01 * 0.34f)),
                ProceduralPropRole.GroundGrass => Mathf.Clamp01(
                    flat01 *
                    (meadowNoise * 0.46f + understoryNoise * 0.34f + cluster * 0.30f) *
                    (0.82f + forestEdge * category.ForestEdgeAffinity * 0.10f) *
                    (1f - shore * 0.12f) *
                    (1f - high01 * 0.50f)),
                ProceduralPropRole.GroundPlants => Mathf.Clamp01(
                    flat01 *
                    (meadowNoise * 0.52f + understoryNoise * 0.36f + cluster * 0.24f) *
                    (0.65f + forestEdge * category.ForestEdgeAffinity * 0.12f) *
                    (1f - shore * 0.10f) *
                    (1f - high01 * 0.45f)),
                ProceduralPropRole.Rock => Mathf.Clamp01(
                    cluster * 0.32f +
                    rockNoise * 0.22f +
                    slope01 * category.SlopeAffinity * 0.32f +
                    shore * category.ShoreAffinity * 0.25f +
                    high01 * 0.18f),
                ProceduralPropRole.RocksSmallMedium => Mathf.Clamp01(
                    cluster * 0.24f +
                    rockNoise * 0.24f +
                    slope01 * category.SlopeAffinity * 0.30f +
                    shore * category.ShoreAffinity * 0.30f +
                    high01 * 0.18f),
                ProceduralPropRole.RocksLarge => Mathf.Clamp01(
                    cluster * 0.22f +
                    SmoothRange(0.46f, 0.9f, rockNoise) * 0.26f +
                    slope01 * category.SlopeAffinity * 0.34f +
                    shore * category.ShoreAffinity * 0.22f +
                    high01 * 0.20f),
                ProceduralPropRole.Cliff => Mathf.Clamp01(
                    cluster * 0.25f +
                    Mathf.InverseLerp(26f, 66f, slope) * category.SlopeAffinity * 0.34f +
                    high01 * 0.28f +
                    rockNoise * 0.18f),
                ProceduralPropRole.ShorePlants => Mathf.Clamp01(
                    shoreBand *
                    flat01 *
                    (cluster * 0.34f + meadowNoise * 0.28f + understoryNoise * 0.24f) *
                    (settings.WaterEnabled ? 1f : 0f)),
                ProceduralPropRole.Log => Mathf.Clamp01((cluster * 0.75f + meadowNoise * 0.25f) * flat01 * (0.35f + forestEdge * category.ForestEdgeAffinity * 0.38f)),
                _ => Mathf.Clamp01(flat01 * (cluster * 0.45f + forestEdge * category.ForestEdgeAffinity * 0.18f + meadowNoise * 0.38f) * (1f - shore * 0.18f))
            };
        }

        private static float SampleClusterMask(PropCategoryPlacementSettings category, float nx, float nz, int seed)
        {
            var scale = Mathf.Max(1f, category.ClusterNoiseScale);
            var worldScale = 1000f / scale;
            var offsetA = (seed & 0xFFFF) * 0.00029f + (int)category.Role * 13.17f;
            var offsetB = ((seed >> 8) & 0xFFFF) * 0.00031f + (int)category.Role * 19.73f;
            var broad = Mathf.PerlinNoise(nx * worldScale + offsetA, nz * worldScale + offsetB);
            var detail = Mathf.PerlinNoise(nx * worldScale * 2.65f + offsetB, nz * worldScale * 2.65f + offsetA);
            var combined = Mathf.Clamp01(broad * 0.72f + detail * 0.28f);
            var threshold = category.ClusterThreshold;
            var mask = SmoothRange(threshold, 1f, combined);
            return Mathf.Pow(mask, Mathf.Lerp(1.55f, 0.55f, category.ClusterStrength / 4f));
        }

        private static float SmoothRange(float min, float max, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
        }

        private static float Range(System.Random random, float minInclusive, float maxInclusive)
        {
            return minInclusive + (float)random.NextDouble() * (maxInclusive - minInclusive);
        }

        private static float Next01(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float ResolveVerticalOffset(ProceduralPropRole role, System.Random random)
        {
            switch (role)
            {
                case ProceduralPropRole.Cliff:
                    return Range(random, -0.35f, -0.08f);
                case ProceduralPropRole.Rock:
                case ProceduralPropRole.RocksSmallMedium:
                case ProceduralPropRole.RocksLarge:
                    return Range(random, -0.22f, -0.04f);
                case ProceduralPropRole.Log:
                    return Range(random, -0.06f, 0.02f);
                default:
                    return 0f;
            }
        }

        private static void PrepareSpawnedInstance(GameObject instance)
        {
            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);

            var transforms = instance.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = HideFlags.None;
            }
        }


        private static void SafeDestroy(GameObject target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }

        private static void ValidateLodSetup(
            PropCategoryPlacementSettings category,
            GameObject prefab,
            GameObject instance,
            HashSet<int> warnedPrefabs)
        {
            if (!category.WarnIfMissingLodGroup && !category.ExpectLodGroup)
            {
                return;
            }

            if (instance.GetComponentInChildren<LODGroup>() != null)
            {
                return;
            }

            var prefabId = prefab.GetInstanceID();
            if (!warnedPrefabs.Add(prefabId))
            {
                return;
            }

            var expectation = category.ExpectLodGroup ? "Expected an LODGroup but none was found." : "No LODGroup found.";
            Debug.LogWarning(
                $"Prop category '{category.CategoryName}' prefab '{prefab.name}' is not LOD-configured. {expectation} " +
                "To enable efficient camera-distance rendering, add an LODGroup to the prefab root or one of its children.");
        }

        private static void ApplyDrawDistance(GameObject instance, float maxDrawDistance)
        {
            if (maxDrawDistance <= 0f)
            {
                return;
            }

            var cullingGroup = instance.GetComponent<GeneratedPropDistanceCulling>();
            if (cullingGroup == null)
            {
                cullingGroup = instance.AddComponent<GeneratedPropDistanceCulling>();
            }

            cullingGroup.Initialize(maxDrawDistance);
        }

        private static bool TrySamplePoint(Terrain terrain, float worldX, float worldZ, out Vector3 point, out Vector3 normal)
        {
            var terrainPos = terrain.transform.position;
            var localX = worldX - terrainPos.x;
            var localZ = worldZ - terrainPos.z;
            var size = terrain.terrainData.size;

            if (localX < 0f || localX > size.x || localZ < 0f || localZ > size.z)
            {
                point = default;
                normal = default;
                return false;
            }

            var y = terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainPos.y;
            point = new Vector3(worldX, y, worldZ);

            var normalizedX = Mathf.Clamp01(localX / size.x);
            var normalizedZ = Mathf.Clamp01(localZ / size.z);
            normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ).normalized;
            return true;
        }

        private sealed class SpatialHash2D
        {
            private readonly Dictionary<Vector2Int, List<Vector2>> cells = new();
            private readonly float cellSize;

            public SpatialHash2D(float minDistance)
            {
                cellSize = Mathf.Max(0.01f, minDistance);
            }

            public void Add(Vector2 point)
            {
                var cell = GetCell(point);
                if (!cells.TryGetValue(cell, out var points))
                {
                    points = new List<Vector2>();
                    cells.Add(cell, points);
                }

                points.Add(point);
            }

            public bool IsOverlapping(Vector2 point, float minDistanceSqr)
            {
                var center = GetCell(point);
                for (var y = -1; y <= 1; y++)
                {
                    for (var x = -1; x <= 1; x++)
                    {
                        var cell = new Vector2Int(center.x + x, center.y + y);
                        if (!cells.TryGetValue(cell, out var points))
                        {
                            continue;
                        }

                        for (var i = 0; i < points.Count; i++)
                        {
                            if ((points[i] - point).sqrMagnitude < minDistanceSqr)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            private Vector2Int GetCell(Vector2 point)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(point.x / cellSize),
                    Mathf.FloorToInt(point.y / cellSize));
            }
        }
    }
}
