using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public sealed class WorldFeatureGenerationRequest
    {
        public WorldFeatureGenerationRequest(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler terrainSampler,
            int seed)
        {
            Settings = settings;
            TerrainSampler = terrainSampler;
            Seed = seed;
        }

        public ProceduralLocationSettings Settings { get; }
        public IProceduralTerrainSampler TerrainSampler { get; }
        public int Seed { get; }
    }
}
