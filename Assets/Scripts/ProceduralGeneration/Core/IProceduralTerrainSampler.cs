using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Core
{
    public interface IProceduralTerrainSampler
    {
        Bounds WorldBounds { get; }
        bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal);
        float SampleHeight01(float worldX, float worldZ);
        float SampleHeightMeters(float worldX, float worldZ);
        Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f);
        float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length);
    }
}
