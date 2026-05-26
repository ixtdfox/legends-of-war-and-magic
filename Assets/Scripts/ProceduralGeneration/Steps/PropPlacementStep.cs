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
        private const float MinTreeHeightMeters = 8f;
        private const float MaxTreeHeightMeters = 26f;

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
            var instancedRenderer = propsRoot.gameObject.AddComponent<GeneratedInstancedPropRenderer>();

            var placementQueue = BuildPlacementQueue(context.Settings.PropCategories);
            var footprintGrid = new FootprintGrid2D(8f);
            for (var i = 0; i < placementQueue.Count; i++)
            {
                var workItem = placementQueue[i];
                var random = new System.Random(unchecked(context.Seed * 486187739 + 97 + workItem.OriginalIndex * 104729));
                PlaceCategory(context, workItem.Category, terrain, propsRoot, instancedRenderer, footprintGrid, random);
            }
        }

        private static void PlaceCategory(
            GenerationContext context,
            PropCategoryPlacementSettings category,
            Terrain terrain,
            Transform propsRoot,
            GeneratedInstancedPropRenderer instancedRenderer,
            FootprintGrid2D footprintGrid,
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

            var categorySpacing = new PointSpacingHash2D(category.MinDistanceBetweenInstances);
            var minDistanceSqr = category.MinDistanceBetweenInstances * category.MinDistanceBetweenInstances;
            var maxAttempts = Mathf.Max(targetCount, Mathf.CeilToInt(targetCount * category.AttemptsMultiplier * ResolveAttemptBoost(category.Role)));
            var forestAnchors = ShouldUseForestAnchoredSampling(category.Role)
                ? CollectForestAnchors(propsRoot)
                : null;
            var accepted = 0;

            for (var attempt = 0; attempt < maxAttempts && accepted < targetCount; attempt++)
            {
                var candidate = ResolveCandidatePoint(random, minX, maxX, minZ, maxZ, category.Role, forestAnchors);
                var x = candidate.x;
                var z = candidate.y;

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
                if (minDistanceSqr > 0f && categorySpacing.IsOverlapping(point2D, minDistanceSqr))
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

                var scaleRange = category.RandomScaleRange;
                var uniformScale = Range(random, scaleRange.x, scaleRange.y);
                var rotationY = category.RandomYRotation ? Range(random, 0f, 360f) : 0f;
                var spawnPoint = point;
                spawnPoint.y += ResolveVerticalOffset(category.Role, random);

                var prefabLocalBounds = default(Bounds);
                var canRenderInstanced = instancedRenderer != null &&
                                         instancedRenderer.TryGetPrefabLocalBounds(prefab, category.Role, out prefabLocalBounds);
                if (canRenderInstanced)
                {
                    uniformScale = ResolveUniformScale(category.Role, prefabLocalBounds, uniformScale, random);
                }

                var instance = canRenderInstanced
                    ? CreateLightweightInstance(prefab, categoryRoot, spawnPoint, rotationY, uniformScale, accepted + 1)
                    : CreatePrefabInstance(prefab, categoryRoot, spawnPoint, rotationY, uniformScale, accepted + 1);

                var footprint = canRenderInstanced
                    ? ResolveFootprint(instance.transform, category, prefabLocalBounds)
                    : ResolveFootprint(instance, category);
                if (footprintGrid.IsOverlapping(footprint.Center, footprint.Radius))
                {
                    SafeDestroy(instance);
                    context.RecordRejected(categoryName, "Overlap");
                    continue;
                }

                ValidateLodSetup(category, prefab, lodWarnedPrefabs);
                Bounds? knownLocalBounds = canRenderInstanced ? prefabLocalBounds : (Bounds?)null;
                EnsureBlockingCollision(instance, category, knownLocalBounds);
                if (canRenderInstanced)
                {
                    instancedRenderer.RegisterPrefabInstance(prefab, category.Role, instance.transform, category.MaxDrawDistance);
                }
                else
                {
                    ApplyDrawDistance(instance, category.MaxDrawDistance);
                }

                categorySpacing.Add(point2D);
                footprintGrid.Add(footprint.Center, footprint.Radius);
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

        private static List<CategoryPlacementWork> BuildPlacementQueue(IReadOnlyList<PropCategoryPlacementSettings> categories)
        {
            var queue = new List<CategoryPlacementWork>();
            for (var i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                if (category == null)
                {
                    continue;
                }

                queue.Add(new CategoryPlacementWork(category, i));
            }

            queue.Sort(ComparePlacementWork);
            return queue;
        }

        private static int ComparePlacementWork(CategoryPlacementWork left, CategoryPlacementWork right)
        {
            var priorityComparison = ResolvePlacementPriority(left.Category.Role).CompareTo(ResolvePlacementPriority(right.Category.Role));
            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private static int ResolvePlacementPriority(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 0,
                ProceduralPropRole.RocksLarge => 1,
                ProceduralPropRole.Rock => 2,
                ProceduralPropRole.RocksSmallMedium => 2,
                ProceduralPropRole.ForestCoreTrees => 3,
                ProceduralPropRole.Tree => 3,
                ProceduralPropRole.ForestAccentTrees => 4,
                ProceduralPropRole.Bushes => 5,
                ProceduralPropRole.Log => 6,
                ProceduralPropRole.GroundPlants => 7,
                ProceduralPropRole.ShorePlants => 8,
                ProceduralPropRole.GroundGrass => 9,
                _ => 10
            };
        }

        private static GameObject CreateLightweightInstance(
            GameObject prefab,
            Transform parent,
            Vector3 position,
            float rotationY,
            float uniformScale,
            int instanceIndex)
        {
            var instance = new GameObject($"{prefab.name}_{instanceIndex:D4}");
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale = prefab.transform.localScale * uniformScale;
            return instance;
        }

        private static GameObject CreatePrefabInstance(
            GameObject prefab,
            Transform parent,
            Vector3 position,
            float rotationY,
            float uniformScale,
            int instanceIndex)
        {
            var instance = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            instance.name = $"{prefab.name}_{instanceIndex:D4}";
            PrepareSpawnedInstance(instance);
            instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
            instance.transform.localScale *= uniformScale;
            return instance;
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

        private static Vector2 ResolveCandidatePoint(
            System.Random random,
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            ProceduralPropRole role,
            IReadOnlyList<Vector3> forestAnchors)
        {
            if (forestAnchors != null && forestAnchors.Count > 0 && Next01(random) < 0.88f)
            {
                var anchor = forestAnchors[random.Next(0, forestAnchors.Count)];
                var angle = Next01(random) * Mathf.PI * 2f;
                var radius = Range(random, ResolveAnchorMinRadius(role), ResolveAnchorMaxRadius(role));
                var anchored = new Vector2(
                    anchor.x + Mathf.Cos(angle) * radius,
                    anchor.z + Mathf.Sin(angle) * radius);

                if (anchored.x >= minX && anchored.x <= maxX && anchored.y >= minZ && anchored.y <= maxZ)
                {
                    return anchored;
                }
            }

            return new Vector2(Range(random, minX, maxX), Range(random, minZ, maxZ));
        }

        private static bool ShouldUseForestAnchoredSampling(ProceduralPropRole role)
        {
            return role == ProceduralPropRole.Bushes ||
                   role == ProceduralPropRole.GroundPlants ||
                   role == ProceduralPropRole.Log;
        }

        private static float ResolveAnchorMinRadius(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.GroundPlants => 1.3f,
                ProceduralPropRole.Log => 2.2f,
                _ => 1.8f
            };
        }

        private static float ResolveAnchorMaxRadius(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.GroundPlants => 6.8f,
                ProceduralPropRole.Log => 8.5f,
                _ => 8.0f
            };
        }

        private static IReadOnlyList<Vector3> CollectForestAnchors(Transform propsRoot)
        {
            var anchors = new List<Vector3>();
            CollectCategoryAnchors(propsRoot, "ForestCoreTrees", anchors);
            CollectCategoryAnchors(propsRoot, "Tree", anchors);
            CollectCategoryAnchors(propsRoot, "ForestAccentTrees", anchors);
            return anchors;
        }

        private static void CollectCategoryAnchors(Transform propsRoot, string categoryName, List<Vector3> anchors)
        {
            var categoryRoot = propsRoot != null ? propsRoot.Find(categoryName) : null;
            if (categoryRoot == null)
            {
                return;
            }

            for (var i = 0; i < categoryRoot.childCount; i++)
            {
                anchors.Add(categoryRoot.GetChild(i).position);
            }
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
            var denseForest = Mathf.InverseLerp(2.4f, 4f, category.ClusterStrength);
            var forestFlat = Mathf.Lerp(flat01, 1f - SmoothRange(26f, 52f, slope), denseForest);
            var coreForestMask = cluster * Mathf.Lerp(0.94f, 0.78f, denseForest) +
                                 meadowNoise * Mathf.Lerp(0.18f, 0.30f, denseForest) +
                                 groveNoise * 0.18f * denseForest +
                                 0.26f * denseForest;

            return category.Role switch
            {
                ProceduralPropRole.Tree => Mathf.Clamp01(
                    coreForestMask *
                    forestFlat *
                    (1f - shore * 0.22f) *
                    (1f - high01 * Mathf.Lerp(0.36f, 0.22f, denseForest)) *
                    Mathf.Lerp(0.72f, 1.18f, understoryNoise)),
                ProceduralPropRole.ForestCoreTrees => Mathf.Clamp01(
                    coreForestMask *
                    forestFlat *
                    (1f - shore * 0.22f) *
                    (1f - high01 * Mathf.Lerp(0.36f, 0.22f, denseForest)) *
                    Mathf.Lerp(0.72f, 1.18f, understoryNoise)),
                ProceduralPropRole.ForestAccentTrees => Mathf.Clamp01(
                    (SmoothRange(0.48f, 0.84f, groveNoise) + denseForest * 0.18f) *
                    Mathf.Pow(cluster, 0.65f) *
                    forestFlat *
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

        private static float ResolveUniformScale(
            ProceduralPropRole role,
            Bounds prefabLocalBounds,
            float fallbackScale,
            System.Random random)
        {
            if (!IsTreeRole(role) || prefabLocalBounds.size.y <= 0.01f)
            {
                return fallbackScale;
            }

            var targetHeight = ResolveTreeTargetHeight(random);
            return Mathf.Clamp(targetHeight / prefabLocalBounds.size.y, 0.01f, 100f);
        }

        private static float ResolveTreeTargetHeight(System.Random random)
        {
            var biased01 = Mathf.Max(Next01(random), Mathf.Max(Next01(random), Next01(random)));
            return Mathf.Lerp(MinTreeHeightMeters, MaxTreeHeightMeters, biased01);
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
            HashSet<int> warnedPrefabs)
        {
            if (!category.WarnIfMissingLodGroup && !category.ExpectLodGroup)
            {
                return;
            }

            if (prefab.GetComponentInChildren<LODGroup>(true) != null)
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

        private static void EnsureBlockingCollision(
            GameObject instance,
            PropCategoryPlacementSettings category,
            Bounds? knownLocalBounds = null)
        {
            if (!ShouldBlockPlayer(category.Role))
            {
                return;
            }

            if (NormalizeExistingBlockingColliders(instance))
            {
                return;
            }

            var bounds = knownLocalBounds ?? CalculateLocalRendererBounds(instance);
            if (!bounds.HasValue)
            {
                return;
            }

            if (IsTreeRole(category.Role))
            {
                AddTreeTrunkCollider(instance, bounds.Value);
                return;
            }

            if (category.Role == ProceduralPropRole.Bushes)
            {
                AddBushCollider(instance, bounds.Value);
                return;
            }

            AddBoundsCollider(instance, bounds.Value, category.Role);
        }

        private static bool ShouldBlockPlayer(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Tree => true,
                ProceduralPropRole.ForestCoreTrees => true,
                ProceduralPropRole.ForestAccentTrees => true,
                ProceduralPropRole.Rock => true,
                ProceduralPropRole.RocksSmallMedium => true,
                ProceduralPropRole.RocksLarge => true,
                ProceduralPropRole.Cliff => true,
                ProceduralPropRole.Log => true,
                ProceduralPropRole.Bushes => true,
                _ => false
            };
        }

        private static bool IsTreeRole(ProceduralPropRole role)
        {
            return role == ProceduralPropRole.Tree ||
                   role == ProceduralPropRole.ForestCoreTrees ||
                   role == ProceduralPropRole.ForestAccentTrees;
        }

        private static bool NormalizeExistingBlockingColliders(GameObject instance)
        {
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            var hasBlockingCollider = false;
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !IsUsableCollider(collider))
                {
                    continue;
                }

                collider.enabled = true;
                collider.isTrigger = false;
                hasBlockingCollider = true;
            }

            return hasBlockingCollider;
        }

        private static bool IsUsableCollider(Collider collider)
        {
            return collider switch
            {
                MeshCollider meshCollider => meshCollider.sharedMesh != null,
                _ => true
            };
        }

        private static void AddTreeTrunkCollider(GameObject instance, Bounds localBounds)
        {
            var collider = instance.AddComponent<CapsuleCollider>();
            var xzDiameter = Mathf.Min(localBounds.size.x, localBounds.size.z);
            collider.direction = 1;
            collider.radius = Mathf.Clamp(xzDiameter * 0.13f, 0.18f, 0.85f);
            collider.height = Mathf.Clamp(localBounds.size.y * 0.72f, collider.radius * 2.25f, localBounds.size.y);
            collider.center = new Vector3(
                localBounds.center.x,
                localBounds.min.y + collider.height * 0.5f,
                localBounds.center.z);
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private static void AddBushCollider(GameObject instance, Bounds localBounds)
        {
            var collider = instance.AddComponent<CapsuleCollider>();
            var xzDiameter = Mathf.Min(localBounds.size.x, localBounds.size.z);
            collider.direction = 1;
            collider.radius = Mathf.Clamp(xzDiameter * 0.28f, 0.28f, 1.35f);
            collider.height = Mathf.Clamp(localBounds.size.y * 0.72f, collider.radius * 2f, localBounds.size.y);
            collider.center = new Vector3(
                localBounds.center.x,
                localBounds.min.y + collider.height * 0.5f,
                localBounds.center.z);
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private static void AddBoundsCollider(GameObject instance, Bounds localBounds, ProceduralPropRole role)
        {
            var collider = instance.AddComponent<BoxCollider>();
            var shrink = ResolveBoundsColliderShrink(role);
            var size = new Vector3(
                Mathf.Max(0.1f, localBounds.size.x * shrink.x),
                Mathf.Max(0.1f, localBounds.size.y * shrink.y),
                Mathf.Max(0.1f, localBounds.size.z * shrink.z));

            collider.size = size;
            collider.center = new Vector3(
                localBounds.center.x,
                localBounds.min.y + size.y * 0.5f,
                localBounds.center.z);
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private static Vector3 ResolveBoundsColliderShrink(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => new Vector3(0.9f, 0.9f, 0.9f),
                ProceduralPropRole.RocksLarge => new Vector3(0.84f, 0.78f, 0.84f),
                ProceduralPropRole.Rock => new Vector3(0.82f, 0.76f, 0.82f),
                ProceduralPropRole.RocksSmallMedium => new Vector3(0.78f, 0.72f, 0.78f),
                ProceduralPropRole.Log => new Vector3(0.86f, 0.55f, 0.86f),
                _ => new Vector3(0.82f, 0.76f, 0.82f)
            };
        }

        private static Footprint ResolveFootprint(GameObject instance, PropCategoryPlacementSettings category)
        {
            var bounds = CalculateRendererBounds(instance);
            var center = new Vector2(instance.transform.position.x, instance.transform.position.z);
            var radius = category.MinDistanceBetweenInstances * ResolveFootprintMinDistanceFactor(category.Role);

            if (bounds.HasValue)
            {
                center = new Vector2(bounds.Value.center.x, bounds.Value.center.z);
                var rendererRadius = Mathf.Max(bounds.Value.extents.x, bounds.Value.extents.z);
                radius = Mathf.Max(radius, rendererRadius * ResolveRendererFootprintFactor(category.Role));
            }

            radius = Mathf.Clamp(
                radius + ResolveFootprintPadding(category.Role),
                ResolveMinFootprintRadius(category.Role),
                ResolveMaxFootprintRadius(category.Role));

            return new Footprint(center, radius);
        }

        private static Footprint ResolveFootprint(
            Transform instanceTransform,
            PropCategoryPlacementSettings category,
            Bounds localBounds)
        {
            var bounds = GeneratedInstancedPropRenderer.TransformBounds(localBounds, instanceTransform.localToWorldMatrix);
            var center = new Vector2(bounds.center.x, bounds.center.z);
            var radius = category.MinDistanceBetweenInstances * ResolveFootprintMinDistanceFactor(category.Role);
            var rendererRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            radius = Mathf.Max(radius, rendererRadius * ResolveRendererFootprintFactor(category.Role));

            radius = Mathf.Clamp(
                radius + ResolveFootprintPadding(category.Role),
                ResolveMinFootprintRadius(category.Role),
                ResolveMaxFootprintRadius(category.Role));

            return new Footprint(center, radius);
        }

        private static Bounds? CalculateRendererBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var bounds = default(Bounds);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds ? bounds : null;
        }

        private static Bounds? CalculateLocalRendererBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var root = instance.transform;
            var hasBounds = false;
            var localBounds = default(Bounds);

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                EncapsulateLocalBoundsCorner(root, renderer.bounds.min, ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, renderer.bounds.max, ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.min.x, renderer.bounds.min.y, renderer.bounds.max.z), ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.min.x, renderer.bounds.max.y, renderer.bounds.min.z), ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.max.x, renderer.bounds.min.y, renderer.bounds.min.z), ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.min.x, renderer.bounds.max.y, renderer.bounds.max.z), ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.max.x, renderer.bounds.min.y, renderer.bounds.max.z), ref localBounds, ref hasBounds);
                EncapsulateLocalBoundsCorner(root, new Vector3(renderer.bounds.max.x, renderer.bounds.max.y, renderer.bounds.min.z), ref localBounds, ref hasBounds);
            }

            return hasBounds ? localBounds : null;
        }

        private static void EncapsulateLocalBoundsCorner(Transform root, Vector3 worldCorner, ref Bounds localBounds, ref bool hasBounds)
        {
            var localCorner = root.InverseTransformPoint(worldCorner);
            if (!hasBounds)
            {
                localBounds = new Bounds(localCorner, Vector3.zero);
                hasBounds = true;
                return;
            }

            localBounds.Encapsulate(localCorner);
        }

        private static float ResolveFootprintMinDistanceFactor(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 0.42f,
                ProceduralPropRole.RocksLarge => 0.42f,
                ProceduralPropRole.Rock => 0.35f,
                ProceduralPropRole.RocksSmallMedium => 0.34f,
                ProceduralPropRole.ForestCoreTrees => 0.22f,
                ProceduralPropRole.Tree => 0.22f,
                ProceduralPropRole.ForestAccentTrees => 0.20f,
                ProceduralPropRole.Bushes => 0.30f,
                ProceduralPropRole.Log => 0.30f,
                ProceduralPropRole.GroundGrass => 0.18f,
                ProceduralPropRole.GroundPlants => 0.22f,
                ProceduralPropRole.ShorePlants => 0.22f,
                _ => 0.25f
            };
        }

        private static float ResolveRendererFootprintFactor(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 0.82f,
                ProceduralPropRole.RocksLarge => 0.82f,
                ProceduralPropRole.Rock => 0.78f,
                ProceduralPropRole.RocksSmallMedium => 0.76f,
                ProceduralPropRole.ForestCoreTrees => 0.05f,
                ProceduralPropRole.Tree => 0.05f,
                ProceduralPropRole.ForestAccentTrees => 0.045f,
                ProceduralPropRole.Bushes => 0.36f,
                ProceduralPropRole.Log => 0.46f,
                ProceduralPropRole.GroundGrass => 0.32f,
                ProceduralPropRole.GroundPlants => 0.28f,
                ProceduralPropRole.ShorePlants => 0.28f,
                _ => 0.42f
            };
        }

        private static float ResolveFootprintPadding(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 1.3f,
                ProceduralPropRole.RocksLarge => 1.1f,
                ProceduralPropRole.Rock => 0.9f,
                ProceduralPropRole.RocksSmallMedium => 0.9f,
                ProceduralPropRole.ForestCoreTrees => 0.15f,
                ProceduralPropRole.Tree => 0.15f,
                ProceduralPropRole.ForestAccentTrees => 0.12f,
                ProceduralPropRole.Bushes => 0.25f,
                ProceduralPropRole.Log => 0.35f,
                ProceduralPropRole.GroundGrass => 0.1f,
                ProceduralPropRole.GroundPlants => 0.1f,
                ProceduralPropRole.ShorePlants => 0.1f,
                _ => 0.35f
            };
        }

        private static float ResolveMinFootprintRadius(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 3.5f,
                ProceduralPropRole.RocksLarge => 2.4f,
                ProceduralPropRole.Rock => 1.3f,
                ProceduralPropRole.RocksSmallMedium => 1.0f,
                ProceduralPropRole.ForestCoreTrees => 0.55f,
                ProceduralPropRole.Tree => 0.55f,
                ProceduralPropRole.ForestAccentTrees => 0.48f,
                ProceduralPropRole.Bushes => 0.45f,
                ProceduralPropRole.Log => 1.1f,
                ProceduralPropRole.GroundGrass => 0.25f,
                ProceduralPropRole.GroundPlants => 0.25f,
                ProceduralPropRole.ShorePlants => 0.25f,
                _ => 0.6f
            };
        }

        private static float ResolveMaxFootprintRadius(ProceduralPropRole role)
        {
            return role switch
            {
                ProceduralPropRole.Cliff => 18f,
                ProceduralPropRole.RocksLarge => 9f,
                ProceduralPropRole.Rock => 6.5f,
                ProceduralPropRole.RocksSmallMedium => 5.2f,
                ProceduralPropRole.ForestCoreTrees => 1.25f,
                ProceduralPropRole.Tree => 1.25f,
                ProceduralPropRole.ForestAccentTrees => 1.1f,
                ProceduralPropRole.Bushes => 1.6f,
                ProceduralPropRole.Log => 4.5f,
                ProceduralPropRole.GroundGrass => 1.1f,
                ProceduralPropRole.GroundPlants => 0.9f,
                ProceduralPropRole.ShorePlants => 0.9f,
                _ => 2.4f
            };
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

        private readonly struct CategoryPlacementWork
        {
            public CategoryPlacementWork(PropCategoryPlacementSettings category, int originalIndex)
            {
                Category = category;
                OriginalIndex = originalIndex;
            }

            public PropCategoryPlacementSettings Category { get; }
            public int OriginalIndex { get; }
        }

        private readonly struct Footprint
        {
            public Footprint(Vector2 center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            public Vector2 Center { get; }
            public float Radius { get; }
        }

        private readonly struct FootprintEntry
        {
            public FootprintEntry(Vector2 center, float radius)
            {
                Center = center;
                Radius = radius;
            }

            public Vector2 Center { get; }
            public float Radius { get; }
        }

        private sealed class FootprintGrid2D
        {
            private readonly Dictionary<Vector2Int, List<FootprintEntry>> cells = new();
            private readonly float cellSize;
            private float maxRadius;

            public FootprintGrid2D(float cellWorldSize)
            {
                cellSize = Mathf.Max(0.5f, cellWorldSize);
            }

            public void Add(Vector2 center, float radius)
            {
                var safeRadius = Mathf.Max(0.01f, radius);
                var cell = GetCell(center);
                if (!cells.TryGetValue(cell, out var entries))
                {
                    entries = new List<FootprintEntry>();
                    cells.Add(cell, entries);
                }

                entries.Add(new FootprintEntry(center, safeRadius));
                maxRadius = Mathf.Max(maxRadius, safeRadius);
            }

            public bool IsOverlapping(Vector2 center, float radius)
            {
                if (cells.Count == 0)
                {
                    return false;
                }

                var safeRadius = Mathf.Max(0.01f, radius);
                var centerCell = GetCell(center);
                var searchRadius = Mathf.CeilToInt((safeRadius + maxRadius) / cellSize) + 1;
                for (var y = -searchRadius; y <= searchRadius; y++)
                {
                    for (var x = -searchRadius; x <= searchRadius; x++)
                    {
                        var cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                        if (!cells.TryGetValue(cell, out var entries))
                        {
                            continue;
                        }

                        for (var i = 0; i < entries.Count; i++)
                        {
                            var entry = entries[i];
                            var minDistance = safeRadius + entry.Radius;
                            if ((entry.Center - center).sqrMagnitude < minDistance * minDistance)
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

        private sealed class PointSpacingHash2D
        {
            private readonly Dictionary<Vector2Int, List<Vector2>> cells = new();
            private readonly float cellSize;

            public PointSpacingHash2D(float minDistance)
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
