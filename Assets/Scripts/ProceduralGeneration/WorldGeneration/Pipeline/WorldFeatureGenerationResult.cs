namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline
{
    public sealed class WorldFeatureGenerationResult
    {
        public WorldFeatureGenerationResult(WorldGenerationLayers layers)
        {
            Layers = layers;
        }

        public WorldGenerationLayers Layers { get; }
    }
}
