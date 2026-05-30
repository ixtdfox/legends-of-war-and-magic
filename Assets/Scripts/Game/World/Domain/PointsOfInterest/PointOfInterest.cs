using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Common;

namespace LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest
{
    public enum PointOfInterestType
    {
        CaveEntrance,
        BanditCamp,
        AncientTemple,
        AbandonedHouse,
        Ruins,
        Shrine,
        StoneCircle,
        Watchtower,
        Graveyard,
        MonsterNest,
        ResourceNode,
        HiddenCache,
        Portal,
        Obelisk
    }

    public enum PointOfInterestState
    {
        Undiscovered,
        Known,
        Cleared,
        Occupied,
        Sealed
    }

    public sealed class PointOfInterest
    {
        public PointOfInterest(
            string id,
            string name,
            PointOfInterestType type,
            WorldPoint2D worldPosition,
            float radius,
            int dangerLevel,
            FactionId? factionId,
            IReadOnlyList<string> tags,
            PointOfInterestState state)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? type.ToString() : name.Trim();
            Type = type;
            WorldPosition = worldPosition;
            Radius = WorldMath.Max(1f, radius);
            DangerLevel = WorldMath.Clamp(dangerLevel, 0, 10);
            FactionId = factionId;
            Tags = tags ?? Array.Empty<string>();
            State = state;
        }

        public string Id { get; }
        public string Name { get; }
        public PointOfInterestType Type { get; }
        public WorldPoint2D WorldPosition { get; }
        public float Radius { get; }
        public int DangerLevel { get; }
        public FactionId? FactionId { get; }
        public IReadOnlyList<string> Tags { get; }
        public PointOfInterestState State { get; private set; }

        public void MarkKnown()
        {
            if (State == PointOfInterestState.Undiscovered)
            {
                State = PointOfInterestState.Known;
            }
        }

        public void MarkCleared()
        {
            if (State == PointOfInterestState.Sealed)
            {
                throw new InvalidOperationException("A sealed point of interest cannot be cleared directly.");
            }

            State = PointOfInterestState.Cleared;
        }
    }
}
