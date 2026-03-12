using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Places simple markers on the world corners to visualize the bounded playable area.
    /// </summary>
    public sealed class CreateBoundaryMarkersStep : IGenerationStep
    {
        public void Execute(GenerationContext context)
        {
            var markersRoot = new GameObject("GeneratedBoundaries").transform;
            markersRoot.SetParent(context.GeneratedRoot, false);

            var extents = context.WorldBounds.extents;
            CreateMarker(new Vector3(-extents.x, 0.5f, -extents.z), markersRoot, "SW");
            CreateMarker(new Vector3(extents.x, 0.5f, -extents.z), markersRoot, "SE");
            CreateMarker(new Vector3(-extents.x, 0.5f, extents.z), markersRoot, "NW");
            CreateMarker(new Vector3(extents.x, 0.5f, extents.z), markersRoot, "NE");

            context.RecordSpawn("BoundaryMarkers", 4);
        }

        private static void CreateMarker(Vector3 position, Transform parent, string suffix)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = $"BoundaryMarker_{suffix}";
            marker.transform.SetParent(parent, false);
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(5f, 1f, 5f);
        }
    }
}
