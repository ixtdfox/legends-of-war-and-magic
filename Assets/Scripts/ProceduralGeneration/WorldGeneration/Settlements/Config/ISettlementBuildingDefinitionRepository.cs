using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config
{
    public interface ISettlementBuildingDefinitionRepository
    {
        IReadOnlyList<GeneratedSettlementBuildingDefinition> GetAll();
        SettlementTierConfig GetTierConfig(SettlementTier tier);
    }

    public sealed class SettlementBuildingCatalogRepository : ISettlementBuildingDefinitionRepository
    {
        private readonly SettlementBuildingCatalog catalog;

        public SettlementBuildingCatalogRepository(SettlementBuildingCatalog catalog)
        {
            this.catalog = catalog;
        }

        public IReadOnlyList<GeneratedSettlementBuildingDefinition> GetAll()
        {
            return catalog != null ? catalog.BuildDefinitions() : DefaultSettlementBuildingCatalog.CreateDefinitions();
        }

        public SettlementTierConfig GetTierConfig(SettlementTier tier)
        {
            return catalog != null ? catalog.ResolveTierConfig(tier) : DefaultSettlementBuildingCatalog.CreateTierConfig(tier);
        }
    }
}
