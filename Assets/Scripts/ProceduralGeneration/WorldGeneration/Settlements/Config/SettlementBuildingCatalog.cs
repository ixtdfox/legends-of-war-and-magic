using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config
{
    [CreateAssetMenu(
        fileName = "SettlementBuildingCatalog",
        menuName = "Legends of War and Magic/Procedural Generation/GeneratedSettlement Building Catalog",
        order = 10)]
    public sealed class SettlementBuildingCatalog : ScriptableObject
    {
        [SerializeField] private SettlementBuildingConfigEntry[] buildings = Array.Empty<SettlementBuildingConfigEntry>();
        [SerializeField] private SettlementTierConfig[] tiers = Array.Empty<SettlementTierConfig>();

        public IReadOnlyList<SettlementBuildingConfigEntry> Buildings => buildings;
        public IReadOnlyList<SettlementTierConfig> Tiers => tiers;

        public IReadOnlyList<GeneratedSettlementBuildingDefinition> BuildDefinitions()
        {
            var definitions = new List<GeneratedSettlementBuildingDefinition>();
            for (var i = 0; i < buildings.Length; i++)
            {
                var entry = buildings[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                definitions.Add(entry.ToDefinition());
            }

            return definitions.Count > 0 ? definitions : DefaultSettlementBuildingCatalog.CreateDefinitions();
        }

        public SettlementTierConfig ResolveTierConfig(SettlementTier tier)
        {
            for (var i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] != null && tiers[i].Tier == tier)
                {
                    return tiers[i];
                }
            }

            return DefaultSettlementBuildingCatalog.CreateTierConfig(tier);
        }
    }
}
