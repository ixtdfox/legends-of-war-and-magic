using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Snapshots
{
    public static class TerrainMeshObjExporter
    {
        public static object Export(string objPath, IReadOnlyList<Terrain> terrains, Vector3 referencePosition, int maxTerrains = 9, int maxResolution = 65)
        {
            var selected = (terrains ?? Array.Empty<Terrain>())
                .Where(terrain => terrain != null && terrain.terrainData != null)
                .OrderBy(terrain => Vector3.Distance(referencePosition, terrain.transform.position + terrain.terrainData.size * 0.5f))
                .Take(Mathf.Max(1, maxTerrains))
                .ToArray();

            var metadata = new List<object>();
            var builder = new StringBuilder(1024 * 64);
            builder.AppendLine("# Legends of War and Magic debug terrain mesh export");
            builder.AppendLine("# Unity coordinates: X/Z horizontal, Y up, meters");

            var vertexOffset = 1;
            var totalVertices = 0;
            var totalTriangles = 0;
            for (var i = 0; i < selected.Length; i++)
            {
                var terrain = selected[i];
                var data = terrain.terrainData;
                var resolution = Mathf.Clamp(Mathf.Min(data.heightmapResolution, maxResolution), 2, maxResolution);
                var vertexCount = resolution * resolution;
                var triangleCount = (resolution - 1) * (resolution - 1) * 2;
                builder.Append("g ").Append(SafeName(terrain.name)).AppendLine();

                for (var z = 0; z < resolution; z++)
                {
                    var v = z / (float)(resolution - 1);
                    for (var x = 0; x < resolution; x++)
                    {
                        var u = x / (float)(resolution - 1);
                        var height = data.GetInterpolatedHeight(u, v);
                        var world = terrain.transform.position + new Vector3(u * data.size.x, height, v * data.size.z);
                        AppendVertex(builder, "v", world);
                    }
                }

                for (var z = 0; z < resolution; z++)
                {
                    var v = z / (float)(resolution - 1);
                    for (var x = 0; x < resolution; x++)
                    {
                        var u = x / (float)(resolution - 1);
                        AppendVertex(builder, "vn", data.GetInterpolatedNormal(u, v));
                    }
                }

                for (var z = 0; z < resolution - 1; z++)
                {
                    for (var x = 0; x < resolution - 1; x++)
                    {
                        var a = vertexOffset + z * resolution + x;
                        var b = vertexOffset + z * resolution + x + 1;
                        var c = vertexOffset + (z + 1) * resolution + x;
                        var d = vertexOffset + (z + 1) * resolution + x + 1;
                        builder.Append("f ").Append(a).Append("//").Append(a).Append(' ')
                            .Append(c).Append("//").Append(c).Append(' ')
                            .Append(b).Append("//").Append(b).AppendLine();
                        builder.Append("f ").Append(b).Append("//").Append(b).Append(' ')
                            .Append(c).Append("//").Append(c).Append(' ')
                            .Append(d).Append("//").Append(d).AppendLine();
                    }
                }

                var bounds = new Bounds(terrain.transform.position + data.size * 0.5f, data.size);
                metadata.Add(new
                {
                    terrain.name,
                    position = terrain.transform.position,
                    size = data.size,
                    bounds,
                    sourceHeightmapResolution = data.heightmapResolution,
                    exportedResolution = resolution,
                    vertexStart = vertexOffset,
                    vertexCount,
                    triangleCount,
                    heightmapPixelError = terrain.heightmapPixelError,
                    lodLevelEstimate = EstimateLod(terrain.heightmapPixelError),
                    simplified = data.heightmapResolution > resolution
                });
                vertexOffset += vertexCount;
                totalVertices += vertexCount;
                totalTriangles += triangleCount;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(objPath));
            File.WriteAllText(objPath, builder.ToString(), Encoding.UTF8);

            return new
            {
                objPath,
                coordinateSystem = "Unity world coordinates, X/Z horizontal, Y up, meters",
                exportedTerrainCount = selected.Length,
                maxTerrains,
                maxResolution,
                totalVertices,
                totalTriangles,
                terrains = metadata
            };
        }

        public static IReadOnlyList<Terrain> SelectRelevantTerrains(Camera camera, Transform player, int maxTerrains = 9)
        {
            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            var reference = player != null ? player.position : camera != null ? camera.transform.position : Vector3.zero;
            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            return terrains
                .Where(terrain => terrain != null && terrain.terrainData != null)
                .OrderBy(terrain =>
                {
                    var bounds = new Bounds(terrain.transform.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
                    var inFrustum = planes != null && GeometryUtility.TestPlanesAABB(planes, bounds);
                    return (inFrustum ? 0f : 100000f) + Vector3.Distance(reference, bounds.center);
                })
                .Take(Mathf.Max(1, maxTerrains))
                .ToArray();
        }

        private static void AppendVertex(StringBuilder builder, string prefix, Vector3 value)
        {
            builder.Append(prefix).Append(' ')
                .Append(value.x.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
                .Append(value.y.ToString("0.######", CultureInfo.InvariantCulture)).Append(' ')
                .Append(value.z.ToString("0.######", CultureInfo.InvariantCulture)).AppendLine();
        }

        private static int EstimateLod(float pixelError)
        {
            if (pixelError <= 8f) return 0;
            return pixelError <= 24f ? 1 : 2;
        }

        private static string SafeName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "terrain"
                : value.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
