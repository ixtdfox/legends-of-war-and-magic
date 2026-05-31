using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Snapshots
{
    public static class SceneObjectSnapshotSerializer
    {
        public static object CaptureNearbyObjects(Vector3 origin, Plane[] frustumPlanes, float maxDistance = 260f, int maxObjects = 220)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var objects = new List<object>();
            var categoryCounts = new Dictionary<string, int>();

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var distance = Vector3.Distance(origin, renderer.bounds.center);
                if (distance > maxDistance)
                {
                    continue;
                }

                var inFrustum = frustumPlanes != null && frustumPlanes.Length > 0 && GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
                var category = Categorize(renderer.gameObject);
                categoryCounts.TryGetValue(category, out var count);
                categoryCounts[category] = count + 1;

                objects.Add(new
                {
                    name = renderer.gameObject.name,
                    path = BuildPath(renderer.transform),
                    category,
                    type = renderer.GetType().Name,
                    layer = LayerMask.LayerToName(renderer.gameObject.layer),
                    position = renderer.transform.position,
                    rotation = renderer.transform.rotation,
                    scale = renderer.transform.lossyScale,
                    distanceToPlayer = distance,
                    inCameraFrustum = inFrustum,
                    bounds = renderer.bounds,
                    materialNames = renderer.sharedMaterials != null
                        ? renderer.sharedMaterials.Where(material => material != null).Select(material => material.name).ToArray()
                        : Array.Empty<string>()
                });
            }

            return new
            {
                origin,
                maxDistance,
                totalConsidered = renderers.Length,
                capturedCount = objects.Count,
                categoryCounts,
                objects = objects.OrderBy(item => ReadDistance(item)).Take(maxObjects).ToArray()
            };
        }

        public static string Categorize(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return "unknown";
            }

            var name = gameObject.name.ToLowerInvariant();
            if (name.Contains("terrain")) return "terrain";
            if (name.Contains("grass")) return "grass";
            if (name.Contains("tree") || name.Contains("forest")) return "trees";
            if (name.Contains("rock") || name.Contains("stone")) return "rocks";
            if (name.Contains("road")) return "roads";
            if (name.Contains("settlement") || name.Contains("building") || name.Contains("house")) return "settlements";
            if (name.Contains("poi") || name.Contains("pointofinterest")) return "poi";
            if (name.Contains("water") || name.Contains("river")) return "water";
            if (name.Contains("prop")) return "props";
            return "objects";
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static float ReadDistance(object item)
        {
            var property = item.GetType().GetProperty("distanceToPlayer");
            return property != null && property.GetValue(item) is float distance ? distance : float.MaxValue;
        }
    }
}
