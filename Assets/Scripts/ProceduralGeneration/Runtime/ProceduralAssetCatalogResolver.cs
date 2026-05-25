using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    public static class ProceduralAssetCatalogResolver
    {
        private const string DefaultCatalogResourcePath = "ProceduralGeneration/DefaultEnvironmentAssetCatalog";

        public static ProceduralEnvironmentAssetCatalog LoadDefaultCatalog()
        {
            return Resources.Load<ProceduralEnvironmentAssetCatalog>(DefaultCatalogResourcePath);
        }
    }
}
