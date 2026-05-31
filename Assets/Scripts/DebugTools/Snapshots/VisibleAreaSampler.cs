using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Snapshots
{
    public static class VisibleAreaSampler
    {
        public static object Capture(
            Camera camera,
            Transform player,
            IProceduralTerrainSampler terrainSampler,
            WorldGenerationMaskSet masks,
            IReadOnlyList<Terrain> terrains)
        {
            if (camera == null)
            {
                return new { error = "No active camera found" };
            }

            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            var terrainEntries = new List<object>();
            if (terrains != null)
            {
                for (var i = 0; i < terrains.Count; i++)
                {
                    var terrain = terrains[i];
                    if (terrain == null || terrain.terrainData == null)
                    {
                        continue;
                    }

                    var bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
                    terrainEntries.Add(new
                    {
                        terrain.name,
                        terrain.transform.position,
                        size = terrain.terrainData.size,
                        bounds,
                        inFrustum = GeometryUtility.TestPlanesAABB(planes, bounds),
                        heightmapResolution = terrain.terrainData.heightmapResolution,
                        terrain.heightmapPixelError
                    });
                }
            }

            return new
            {
                camera = CameraSnapshotSerializer.CaptureCamera(camera),
                playerPosition = player != null ? player.position : Vector3.zero,
                frustumPlanes = SerializePlanes(planes),
                visibleTerrains = terrainEntries,
                raycastSamples = CaptureRaycasts(camera, terrainSampler, masks),
                nearbyVisibleObjects = SceneObjectSnapshotSerializer.CaptureNearbyObjects(
                    player != null ? player.position : camera.transform.position,
                    planes)
            };
        }

        private static object[] CaptureRaycasts(Camera camera, IProceduralTerrainSampler terrainSampler, WorldGenerationMaskSet masks)
        {
            var points = new[]
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.35f),
                new Vector2(0.5f, 0.65f),
                new Vector2(0.30f, 0.50f),
                new Vector2(0.70f, 0.50f),
                new Vector2(0.25f, 0.35f),
                new Vector2(0.75f, 0.35f),
                new Vector2(0.25f, 0.65f),
                new Vector2(0.75f, 0.65f)
            };

            var samples = new List<object>(points.Length);
            for (var i = 0; i < points.Length; i++)
            {
                var viewport = points[i];
                var ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
                if (Physics.Raycast(ray, out var hit, camera.farClipPlane))
                {
                    samples.Add(BuildSample(viewport, ray, hit.point, hit.distance, hit.collider != null ? hit.collider.gameObject : null, terrainSampler, masks));
                }
                else
                {
                    var point = ray.origin + ray.direction * Mathf.Min(250f, camera.farClipPlane);
                    samples.Add(BuildSample(viewport, ray, point, -1f, null, terrainSampler, masks));
                }
            }

            return samples.ToArray();
        }

        private static object BuildSample(
            Vector2 viewport,
            Ray ray,
            Vector3 point,
            float distance,
            GameObject hitObject,
            IProceduralTerrainSampler terrainSampler,
            WorldGenerationMaskSet masks)
        {
            var terrainPoint = point;
            var normal = Vector3.up;
            if (terrainSampler != null && terrainSampler.TrySample(point.x, point.z, out var sampledPoint, out var sampledNormal))
            {
                terrainPoint = sampledPoint;
                normal = sampledNormal;
            }

            var xz = new Vector2(point.x, point.z);
            return new
            {
                viewport,
                rayOrigin = ray.origin,
                rayDirection = ray.direction,
                distance,
                hitPosition = point,
                sampledTerrainPosition = terrainPoint,
                height = terrainPoint.y,
                normal,
                slopeDegrees = Vector3.Angle(normal, Vector3.up),
                roadMask = masks != null ? masks.Evaluate(xz, GenerationZoneKind.Road) : 0f,
                settlementMask = masks != null ? masks.Evaluate(xz, GenerationZoneKind.SettlementFootprint) : 0f,
                materialOrTexture = "Unity Terrain material data is stored in terrain-data.json/terrain-mesh-metadata.json when available",
                objectHit = hitObject != null
                    ? new
                    {
                        hitObject.name,
                        category = SceneObjectSnapshotSerializer.Categorize(hitObject),
                        layer = LayerMask.LayerToName(hitObject.layer),
                        position = hitObject.transform.position
                    }
                    : null
            };
        }

        private static object[] SerializePlanes(Plane[] planes)
        {
            if (planes == null)
            {
                return new object[0];
            }

            var result = new object[planes.Length];
            for (var i = 0; i < planes.Length; i++)
            {
                result[i] = new
                {
                    normal = planes[i].normal,
                    distance = planes[i].distance
                };
            }

            return result;
        }
    }
}
