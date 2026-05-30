using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public sealed class SettlementStats
    {
        public SettlementStats(
            int population,
            int wealth,
            int safety,
            int food,
            int production,
            IReadOnlyList<SettlementService> services,
            IReadOnlyList<string> questHooks)
        {
            Population = WorldMath.Max(0, population);
            Wealth = WorldMath.Clamp(wealth, 0, 100);
            Safety = WorldMath.Clamp(safety, 0, 100);
            Food = WorldMath.Clamp(food, 0, 100);
            Production = WorldMath.Clamp(production, 0, 100);
            Services = services ?? Array.Empty<SettlementService>();
            QuestHooks = questHooks ?? Array.Empty<string>();
        }

        public int Population { get; }
        public int Wealth { get; }
        public int Safety { get; }
        public int Food { get; }
        public int Production { get; }
        public IReadOnlyList<SettlementService> Services { get; }
        public IReadOnlyList<string> QuestHooks { get; }
    }

    public sealed class SettlementEconomy
    {
        public SettlementEconomy(
            IReadOnlyList<ResourceAmount> stockpile,
            IReadOnlyList<ResourceAmount> production,
            IReadOnlyList<ResourceAmount> demand,
            float taxRate)
        {
            Stockpile = stockpile ?? Array.Empty<ResourceAmount>();
            Production = production ?? Array.Empty<ResourceAmount>();
            Demand = demand ?? Array.Empty<ResourceAmount>();
            TaxRate = WorldMath.Clamp01(taxRate);
        }

        public IReadOnlyList<ResourceAmount> Stockpile { get; }
        public IReadOnlyList<ResourceAmount> Production { get; }
        public IReadOnlyList<ResourceAmount> Demand { get; }
        public float TaxRate { get; }
    }

    public sealed class SettlementBuilding
    {
        public SettlementBuilding(string id, string definitionId, BuildingType type, int level)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            DefinitionId = definitionId ?? string.Empty;
            Type = type;
            Level = WorldMath.Max(1, level);
        }

        public string Id { get; }
        public string DefinitionId { get; }
        public BuildingType Type { get; }
        public int Level { get; }
    }
}
