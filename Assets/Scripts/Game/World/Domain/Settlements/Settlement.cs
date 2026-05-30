using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public sealed class Settlement
    {
        private readonly List<SettlementBuilding> buildings;

        public Settlement(
            string id,
            string name,
            SettlementTier tier,
            FactionId? ownerFactionId,
            WorldPoint2D worldPosition,
            float radius,
            IReadOnlyList<SettlementBuilding> buildings,
            SettlementStats stats,
            SettlementProgression progression,
            SettlementEconomy economy)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? tier.ToString() : name.Trim();
            Tier = tier;
            OwnerFactionId = ownerFactionId;
            WorldPosition = worldPosition;
            Radius = WorldMath.Max(1f, radius);
            this.buildings = buildings == null ? new List<SettlementBuilding>() : new List<SettlementBuilding>(buildings);
            Stats = stats ?? new SettlementStats(0, 0, 0, 0, 0, Array.Empty<SettlementService>(), Array.Empty<string>());
            Progression = progression ?? new SettlementProgression(1, 0, 0, 0, Array.Empty<SettlementUpgradeRequirement>());
            Economy = economy ?? new SettlementEconomy(Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), 0f);
        }

        public string Id { get; }
        public string Name { get; }
        public SettlementTier Tier { get; private set; }
        public FactionId? OwnerFactionId { get; private set; }
        public WorldPoint2D WorldPosition { get; }
        public float Radius { get; }
        public IReadOnlyList<SettlementBuilding> Buildings => buildings;
        public SettlementStats Stats { get; }
        public SettlementProgression Progression { get; }
        public SettlementEconomy Economy { get; }

        public bool CanConstructBuilding(SettlementBuildingDefinition definition)
        {
            return definition != null && Tier >= definition.MinSettlementTier;
        }

        public SettlementBuilding ConstructBuilding(SettlementBuildingDefinition definition, string instanceId = null)
        {
            if (!CanConstructBuilding(definition))
            {
                throw new InvalidOperationException($"Settlement tier {Tier} cannot construct {definition?.Id ?? "unknown"}.");
            }

            var building = new SettlementBuilding(instanceId, definition.Id, definition.Type, definition.Level);
            buildings.Add(building);
            return building;
        }

        public bool CanUpgradeTo(SettlementTier targetTier)
        {
            if (targetTier <= Tier || targetTier > SettlementTier.Capital)
            {
                return false;
            }

            var types = new List<BuildingType>(buildings.Count);
            for (var i = 0; i < buildings.Count; i++)
            {
                types.Add(buildings[i].Type);
            }

            return Progression.MeetsUpgradeRequirement(targetTier, types);
        }
    }
}
