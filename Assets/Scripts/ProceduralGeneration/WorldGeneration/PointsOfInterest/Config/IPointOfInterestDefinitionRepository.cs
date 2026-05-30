using System.Collections.Generic;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config
{
    public interface IPointOfInterestDefinitionRepository
    {
        IReadOnlyList<PointOfInterestConfigEntry> GetAll();
    }

    public sealed class PointOfInterestCatalogRepository : IPointOfInterestDefinitionRepository
    {
        private readonly PointOfInterestCatalog catalog;

        public PointOfInterestCatalogRepository(PointOfInterestCatalog catalog)
        {
            this.catalog = catalog;
        }

        public IReadOnlyList<PointOfInterestConfigEntry> GetAll()
        {
            return catalog != null ? catalog.ResolveEntries() : DefaultPointOfInterestCatalog.CreateEntries();
        }
    }
}
