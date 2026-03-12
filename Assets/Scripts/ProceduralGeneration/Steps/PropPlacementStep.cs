using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;
using Random = UnityEngine.Random;

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

            var state = Random.state;
            Random.InitState(unchecked(context.Seed * 486187739 + 97));

            try
            {
                for (var i = 0; i < context.Settings.PropCategories.Count; i++)
                {
                    PlaceCategory(context, context.Settings.PropCategories[i], terrain, propsRoot);
                }
            }
            finally
            {
                Random.state = state;
            }
        }

        private static void PlaceCategory(
            GenerationContext context,
            PropCategoryPlacementSettings category,
            Terrain terrain,
            Transform propsRoot)
        {
            if (category == null || !category.Enabled || category.Prefabs == null || category.Prefabs.Length == 0 || category.DensityPer10kSqm <= 0f)
            {
                return;
            }

            var area = context.Settings.WorldWidth * context.Settings.WorldLength;
            var targetCount = Mathf.RoundToInt((area / 10000f) * category.DensityPer10kSqm);
            if (targetCount <= 0)
            {
                return;
            }

            var categoryRoot = new GameObject(category.CategoryName).transform;
            categoryRoot.SetParent(propsRoot, false);

            var worldBounds = context.WorldBounds;
            var minX = worldBounds.min.x;
            var maxX = worldBounds.max.x;
            var minZ = worldBounds.min.z;
            var maxZ = worldBounds.max.z;

            var acceptedPositions = new List<Vector2>(targetCount);
            var minDistanceSqr = category.MinDistanceBetweenInstances * category.MinDistanceBetweenInstances;
            var maxAttempts = Mathf.Max(targetCount, Mathf.CeilToInt(targetCount * category.AttemptsMultiplier));
            var accepted = 0;

            for (var attempt = 0; attempt < maxAttempts && accepted < targetCount; attempt++)
            {
                var x = Random.Range(minX, maxX);
                var z = Random.Range(minZ, maxZ);

                if (!TrySamplePoint(terrain, x, z, out var point, out var normal))
                {
                    continue;
                }

                var slope = Vector3.Angle(normal, Vector3.up);
                var slopeRange = category.AllowedSlopeRange;
                if (slope < slopeRange.x || slope > slopeRange.y)
                {
                    continue;
                }

                var heightRange = category.AllowedHeightRange;
                if (point.y < heightRange.x || point.y > heightRange.y)
                {
                    continue;
                }

                var point2D = new Vector2(point.x, point.z);
                if (minDistanceSqr > 0f && IsOverlapping(point2D, acceptedPositions, minDistanceSqr))
                {
                    continue;
                }

                var prefab = category.Prefabs[Random.Range(0, category.Prefabs.Length)];
                if (prefab == null)
                {
                    continue;
                }

                var instance = UnityEngine.Object.Instantiate(prefab, point, Quaternion.identity, categoryRoot);
                instance.name = $"{prefab.name}_{accepted + 1:D4}";

                var rotationY = category.RandomYRotation ? Random.Range(0f, 360f) : 0f;
                instance.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

                var scaleRange = category.RandomScaleRange;
                var uniformScale = Random.Range(scaleRange.x, scaleRange.y);
                instance.transform.localScale *= uniformScale;

                acceptedPositions.Add(point2D);
                accepted++;
            }

            if (accepted == 0)
            {
                UnityEngine.Object.DestroyImmediate(categoryRoot.gameObject);
            }
            else if (accepted < targetCount)
            {
                Debug.Log($"Prop category '{category.CategoryName}' placed {accepted}/{targetCount} instances after {maxAttempts} attempts.");
            }
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

        private static bool IsOverlapping(Vector2 point, List<Vector2> acceptedPositions, float minDistanceSqr)
        {
            for (var i = 0; i < acceptedPositions.Count; i++)
            {
                if ((acceptedPositions[i] - point).sqrMagnitude < minDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
