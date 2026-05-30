using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public interface ISpecification<in T>
    {
        bool IsSatisfiedBy(T candidate);
    }

    public sealed class BuildingAvailableForTierSpecification : ISpecification<GeneratedSettlementBuildingDefinition>
    {
        private readonly SettlementTier tier;

        public BuildingAvailableForTierSpecification(SettlementTier tier)
        {
            this.tier = tier;
        }

        public bool IsSatisfiedBy(GeneratedSettlementBuildingDefinition candidate)
        {
            return candidate != null && candidate.MinSettlementTier <= tier;
        }
    }
}
