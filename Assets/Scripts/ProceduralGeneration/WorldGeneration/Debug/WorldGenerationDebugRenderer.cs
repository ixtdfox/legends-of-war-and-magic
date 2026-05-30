using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Debug
{
    public static class WorldGenerationDebugRoot
    {
        public static void Ensure(GenerationContext context, bool visibleOnCreate = false)
        {
            if (context?.GeneratedRoot == null || context.WorldLayers == null)
            {
                return;
            }

            var debugObject = new GameObject("WorldGenerationDebug");
            debugObject.transform.SetParent(context.GeneratedRoot, false);
            var renderer = debugObject.AddComponent<WorldGenerationDebugRenderer>();
            renderer.Initialize(context.WorldLayers, context.Settings != null ? context.Settings.Roads : null);
            renderer.enabled = visibleOnCreate;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldGenerationDebugRenderer : MonoBehaviour
    {
        [SerializeField] private bool showSettlements = true;
        [SerializeField] private bool showRoads = true;
        [SerializeField] private bool showPointsOfInterest = true;
        [SerializeField] private bool showMasks;
        [SerializeField] private bool showLabels;
        [SerializeField] private bool showRoadInfluenceWidths = true;
        [SerializeField] private bool showRoadProfileSamples;
        [SerializeField] private bool showRoadModifiedHeightSamples;
        [SerializeField] private bool showRoadGradientCorrections;

        private WorldGenerationLayers layers;
        private RoadGenerationConfig roadConfig;

        public void Initialize(WorldGenerationLayers worldLayers, RoadGenerationConfig generationRoadConfig = null)
        {
            layers = worldLayers;
            roadConfig = generationRoadConfig;
        }

        private void OnDrawGizmos()
        {
            if (layers == null)
            {
                return;
            }

            if (showSettlements)
            {
                DrawSettlements();
            }

            if (showPointsOfInterest)
            {
                DrawPointsOfInterest();
            }

            if (showRoads)
            {
                DrawRoads();
            }

            if (showMasks)
            {
                DrawMasks();
            }
        }

        private void DrawSettlements()
        {
            var settlements = layers.Settlements;
            for (var i = 0; i < settlements.Count; i++)
            {
                var settlement = settlements[i];
                Gizmos.color = new Color(1f, 0.78f, 0.22f, 0.75f);
                DrawCircle(settlement.WorldPosition, settlement.Radius, 32);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(ToWorld(settlement.WorldPosition, 3f), 4f);
                DrawLabel(settlement.WorldPosition, 8f, $"{settlement.Tier}: {settlement.Name}");

                for (var buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    var building = settlement.Buildings[buildingIndex];
                    Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.65f);
                    DrawRect(building.WorldPosition, building.FootprintSize, building.RotationDegrees);
                }

                for (var roadIndex = 0; roadIndex < settlement.InternalRoads.Count; roadIndex++)
                {
                    DrawPolyline(settlement.InternalRoads[roadIndex].Points, new Color(1f, 0.9f, 0.35f, 0.9f), 0.5f);
                }
            }
        }

        private void DrawPointsOfInterest()
        {
            var points = layers.PointsOfInterest;
            for (var i = 0; i < points.Count; i++)
            {
                var poi = points[i];
                Gizmos.color = new Color(0.95f, 0.25f, 0.85f, 0.75f);
                DrawCircle(poi.WorldPosition, poi.Radius, 24);
                Gizmos.DrawCube(ToWorld(poi.WorldPosition, 5f), Vector3.one * 5f);
                DrawLabel(poi.WorldPosition, 8f, $"{poi.Type} ({poi.DangerLevel})");
            }
        }

        private void DrawRoads()
        {
            var roads = layers.RoadNetwork.Segments;
            for (var i = 0; i < roads.Count; i++)
            {
                var road = roads[i];
                DrawPolyline(road.Points, new Color(0.95f, 0.55f, 0.18f, 0.95f), 1f);
                if (showRoadInfluenceWidths)
                {
                    DrawRoadInfluence(road);
                }
            }

            if (showRoadProfileSamples)
            {
                DrawRoadProfileSamples();
            }

            if (showRoadModifiedHeightSamples)
            {
                DrawRoadModifiedSamples();
            }

            if (showRoadGradientCorrections)
            {
                DrawRoadGradientCorrections();
            }
        }

        private void DrawMasks()
        {
            var masks = layers.Masks;
            for (var i = 0; i < masks.Zones.Count; i++)
            {
                var zone = masks.Zones[i];
                Gizmos.color = ResolveMaskColor(zone.Kind);
                DrawCircle(zone.Center, zone.Radius, 20);
            }

            for (var i = 0; i < masks.Paths.Count; i++)
            {
                var path = masks.Paths[i];
                DrawPolyline(path.Points, ResolveMaskColor(path.Kind), 0.25f);
            }
        }

        private static Color ResolveMaskColor(GenerationZoneKind kind)
        {
            return kind switch
            {
                GenerationZoneKind.NoSpawn => new Color(1f, 0.1f, 0.1f, 0.35f),
                GenerationZoneKind.ReducedVegetation => new Color(0.1f, 1f, 0.1f, 0.25f),
                GenerationZoneKind.Road => new Color(1f, 0.55f, 0.1f, 0.45f),
                GenerationZoneKind.PointOfInterestFootprint => new Color(0.9f, 0.1f, 1f, 0.35f),
                _ => new Color(0.2f, 0.6f, 1f, 0.28f)
            };
        }

        private static void DrawCircle(Vector2 center, float radius, int segments)
        {
            var previous = center + Vector2.right * radius;
            for (var i = 1; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(ToWorld(previous, 0.4f), ToWorld(next, 0.4f));
                previous = next;
            }
        }

        private static void DrawRect(Vector2 center, Vector2 size, float rotationDegrees)
        {
            var rotation = Quaternion.Euler(0f, rotationDegrees, 0f);
            var half = new Vector3(size.x * 0.5f, 0f, size.y * 0.5f);
            var c = ToWorld(center, 0.6f);
            var a = c + rotation * new Vector3(-half.x, 0f, -half.z);
            var b = c + rotation * new Vector3(half.x, 0f, -half.z);
            var d = c + rotation * new Vector3(-half.x, 0f, half.z);
            var e = c + rotation * new Vector3(half.x, 0f, half.z);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, e);
            Gizmos.DrawLine(e, d);
            Gizmos.DrawLine(d, a);
        }

        private static void DrawPolyline(System.Collections.Generic.IReadOnlyList<Vector2> points, Color color, float height)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            Gizmos.color = color;
            for (var i = 1; i < points.Count; i++)
            {
                Gizmos.DrawLine(ToWorld(points[i - 1], height), ToWorld(points[i], height));
            }
        }

        private void DrawRoadInfluence(GeneratedRoadSegment road)
        {
            if (road == null || road.Points == null || road.Points.Count < 2)
            {
                return;
            }

            var settings = roadConfig != null ? roadConfig.Resolve(road.Type) : new RoadGenerationConfig().Resolve(road.Type);
            var visibleHalfWidth = road.Width * 0.5f;
            var roadBedHalfWidth = visibleHalfWidth + settings.HeightBedExtraWidth;
            var shoulderEnd = roadBedHalfWidth + settings.ShoulderWidth;
            var outerEnd = shoulderEnd + settings.OuterSmoothWidth;

            DrawOffsetPolyline(road.Points, visibleHalfWidth, new Color(0.95f, 0.70f, 0.20f, 0.45f), 1.05f);
            DrawOffsetPolyline(road.Points, roadBedHalfWidth, new Color(0.15f, 0.85f, 1f, 0.40f), 1.12f);
            DrawOffsetPolyline(road.Points, shoulderEnd, new Color(0.25f, 1f, 0.25f, 0.35f), 1.18f);
            DrawOffsetPolyline(road.Points, outerEnd, new Color(1f, 0.25f, 0.25f, 0.30f), 1.24f);
        }

        private static void DrawOffsetPolyline(System.Collections.Generic.IReadOnlyList<Vector2> points, float offset, Color color, float height)
        {
            if (points == null || points.Count < 2 || offset <= 0f)
            {
                return;
            }

            Gizmos.color = color;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];
                var delta = b - a;
                if (delta.sqrMagnitude <= 0.001f)
                {
                    continue;
                }

                var normal = new Vector2(-delta.y, delta.x).normalized;
                Gizmos.DrawLine(ToWorld(a + normal * offset, height), ToWorld(b + normal * offset, height));
                Gizmos.DrawLine(ToWorld(a - normal * offset, height), ToWorld(b - normal * offset, height));
            }
        }

        private static void DrawRoadProfileSamples()
        {
            var samples = RoadTerrainCarvingDebug.LastProfileSamples;
            Gizmos.color = new Color(0.15f, 0.65f, 1f, 0.85f);
            for (var i = 0; i < samples.Count; i++)
            {
                var position = samples[i].Position + Vector3.up * 1.5f;
                Gizmos.DrawSphere(position, 1.1f);
            }
        }

        private static void DrawRoadModifiedSamples()
        {
            var samples = RoadTerrainCarvingDebug.LastModifiedSamples;
            for (var i = 0; i < samples.Count; i++)
            {
                Gizmos.color = samples[i].Zone switch
                {
                    RoadCarvingZone.RoadBed => new Color(0.10f, 0.85f, 1f, 0.70f),
                    RoadCarvingZone.Shoulder => new Color(0.25f, 1f, 0.25f, 0.55f),
                    RoadCarvingZone.OuterSmoothing => new Color(1f, 0.85f, 0.15f, 0.45f),
                    _ => new Color(1f, 1f, 1f, 0.35f)
                };
                Gizmos.DrawCube(samples[i].Position, Vector3.one * 1.4f);
            }
        }

        private static void DrawRoadGradientCorrections()
        {
            var corrections = RoadTerrainCarvingDebug.LastGradientCorrections;
            Gizmos.color = new Color(1f, 0.10f, 0.95f, 0.85f);
            for (var i = 0; i < corrections.Count; i++)
            {
                Gizmos.DrawSphere(corrections[i].Position, Mathf.Clamp(corrections[i].DeltaMeters, 0.6f, 2.4f));
            }
        }

        private static Vector3 ToWorld(Vector2 point, float y)
        {
            return new Vector3(point.x, y, point.y);
        }

        private void DrawLabel(Vector2 point, float y, string text)
        {
            if (!showLabels || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

#if UNITY_EDITOR
            Handles.Label(ToWorld(point, y), text);
#endif
        }
    }
}
