using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public sealed class SettlementBuildingDefinition
    {
        public SettlementBuildingDefinition(
            string id,
            string displayName,
            BuildingType type,
            int level,
            SettlementTier minSettlementTier,
            WorldSize2D footprintSize,
            IReadOnlyList<BuildingTag> tags,
            IReadOnlyList<SettlementService> services = null)
        {
            Id = string.IsNullOrWhiteSpace(id) ? $"{type}_Level{WorldMath.Max(1, level)}" : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName.Trim();
            Type = type;
            Level = WorldMath.Max(1, level);
            MinSettlementTier = minSettlementTier;
            FootprintSize = new WorldSize2D(WorldMath.Max(0.5f, footprintSize.Width), WorldMath.Max(0.5f, footprintSize.Depth));
            Tags = tags ?? Array.Empty<BuildingTag>();
            Services = services ?? Array.Empty<SettlementService>();
        }

        public string Id { get; }
        public string DisplayName { get; }
        public BuildingType Type { get; }
        public int Level { get; }
        public SettlementTier MinSettlementTier { get; }
        public WorldSize2D FootprintSize { get; }
        public IReadOnlyList<BuildingTag> Tags { get; }
        public IReadOnlyList<SettlementService> Services { get; }
    }
}
