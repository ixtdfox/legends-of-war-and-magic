using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public readonly struct ResourceAmount
    {
        public ResourceAmount(string resourceId, int amount)
        {
            ResourceId = string.IsNullOrWhiteSpace(resourceId) ? "unknown" : resourceId.Trim();
            Amount = WorldMath.Max(0, amount);
        }

        public string ResourceId { get; }
        public int Amount { get; }
    }

    public sealed class SettlementUpgradeRequirement
    {
        public SettlementUpgradeRequirement(
            SettlementTier targetTier,
            int prosperity,
            int security,
            IReadOnlyList<ResourceAmount> resources,
            IReadOnlyList<BuildingType> requiredBuildings)
        {
            TargetTier = targetTier;
            Prosperity = WorldMath.Max(0, prosperity);
            Security = WorldMath.Max(0, security);
            Resources = resources ?? Array.Empty<ResourceAmount>();
            RequiredBuildings = requiredBuildings ?? Array.Empty<BuildingType>();
        }

        public SettlementTier TargetTier { get; }
        public int Prosperity { get; }
        public int Security { get; }
        public IReadOnlyList<ResourceAmount> Resources { get; }
        public IReadOnlyList<BuildingType> RequiredBuildings { get; }
    }

    public sealed class SettlementProgression
    {
        public SettlementProgression(
            int level,
            int prosperity,
            int security,
            int reputation,
            IReadOnlyList<SettlementUpgradeRequirement> upgradeRequirements)
        {
            Level = WorldMath.Max(1, level);
            Prosperity = WorldMath.Clamp(prosperity, 0, 100);
            Security = WorldMath.Clamp(security, 0, 100);
            Reputation = WorldMath.Clamp(reputation, -100, 100);
            UpgradeRequirements = upgradeRequirements ?? Array.Empty<SettlementUpgradeRequirement>();
        }

        public int Level { get; }
        public int Prosperity { get; }
        public int Security { get; }
        public int Reputation { get; }
        public IReadOnlyList<SettlementUpgradeRequirement> UpgradeRequirements { get; }

        public bool MeetsUpgradeRequirement(SettlementTier targetTier, IEnumerable<BuildingType> existingBuildings)
        {
            SettlementUpgradeRequirement requirement = null;
            for (var i = 0; i < UpgradeRequirements.Count; i++)
            {
                if (UpgradeRequirements[i].TargetTier == targetTier)
                {
                    requirement = UpgradeRequirements[i];
                    break;
                }
            }

            if (requirement == null || Prosperity < requirement.Prosperity || Security < requirement.Security)
            {
                return false;
            }

            var built = new HashSet<BuildingType>(existingBuildings ?? Array.Empty<BuildingType>());
            for (var i = 0; i < requirement.RequiredBuildings.Count; i++)
            {
                if (!built.Contains(requirement.RequiredBuildings[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
