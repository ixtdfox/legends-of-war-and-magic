using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public interface ISettlementCommand
    {
        string SettlementId { get; }
    }

    public interface ISettlementEvent
    {
        string SettlementId { get; }
        DateTime OccurredUtc { get; }
    }

    public sealed class UpgradeSettlementCommand : ISettlementCommand
    {
        public UpgradeSettlementCommand(string settlementId, SettlementTier targetTier)
        {
            SettlementId = settlementId ?? string.Empty;
            TargetTier = targetTier;
        }

        public string SettlementId { get; }
        public SettlementTier TargetTier { get; }
    }

    public sealed class ConstructSettlementBuildingCommand : ISettlementCommand
    {
        public ConstructSettlementBuildingCommand(string settlementId, string buildingDefinitionId)
        {
            SettlementId = settlementId ?? string.Empty;
            BuildingDefinitionId = buildingDefinitionId ?? string.Empty;
        }

        public string SettlementId { get; }
        public string BuildingDefinitionId { get; }
    }

    public sealed class SettlementUpgradedEvent : ISettlementEvent
    {
        public SettlementUpgradedEvent(string settlementId, SettlementTier newTier, DateTime occurredUtc)
        {
            SettlementId = settlementId ?? string.Empty;
            NewTier = newTier;
            OccurredUtc = occurredUtc;
        }

        public string SettlementId { get; }
        public SettlementTier NewTier { get; }
        public DateTime OccurredUtc { get; }
    }

    public sealed class SettlementBuildingConstructedEvent : ISettlementEvent
    {
        public SettlementBuildingConstructedEvent(string settlementId, string buildingDefinitionId, DateTime occurredUtc)
        {
            SettlementId = settlementId ?? string.Empty;
            BuildingDefinitionId = buildingDefinitionId ?? string.Empty;
            OccurredUtc = occurredUtc;
        }

        public string SettlementId { get; }
        public string BuildingDefinitionId { get; }
        public DateTime OccurredUtc { get; }
    }

    public sealed class SettlementEventJournal
    {
        private readonly List<ISettlementEvent> events = new();

        public IReadOnlyList<ISettlementEvent> Events => events;

        public void Append(ISettlementEvent settlementEvent)
        {
            if (settlementEvent != null)
            {
                events.Add(settlementEvent);
            }
        }
    }
}
