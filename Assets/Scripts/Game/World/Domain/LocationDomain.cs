using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public enum LocationType
    {
        Wilderness,
        Coast,
        Forest,
        Desert,
        MountainPass,
        Swamp,
        Island,
        RiverCrossing,
        Ruins,
        TownArea,
        DungeonArea
    }

    public enum LocationConnectionType
    {
        LandPath,
        MountainPass,
        ForestTrail,
        RiverCrossing,
        BoatRoute,
        CavePassage
    }

    public readonly struct LocationId : IEquatable<LocationId>
    {
        public LocationId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        }

        public string Value { get; }

        public static LocationId NewId()
        {
            return new LocationId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(LocationId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is LocationId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }
    }

    public readonly struct LocationName
    {
        public LocationName(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? "Unnamed Location" : value.Trim();
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }

    public sealed class LocationConnection
    {
        public LocationConnection(
            LocationId from,
            LocationId to,
            WorldDirection direction,
            LocationConnectionType type,
            string displayName)
        {
            From = from;
            To = to;
            Direction = direction;
            Type = type;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? type.ToString() : displayName;
        }

        public LocationId From { get; }
        public LocationId To { get; }
        public WorldDirection Direction { get; }
        public LocationConnectionType Type { get; }
        public string DisplayName { get; }
    }

    public sealed class LocationGateway
    {
        public LocationGateway(
            LocationId fromLocationId,
            LocationId toLocationId,
            WorldDirection exitDirection,
            WorldDirection entryDirection,
            MapRect gatewayAreaNormalized)
        {
            FromLocationId = fromLocationId;
            ToLocationId = toLocationId;
            ExitDirection = exitDirection;
            EntryDirection = entryDirection;
            GatewayAreaNormalized = gatewayAreaNormalized;
        }

        public LocationId FromLocationId { get; }
        public LocationId ToLocationId { get; }
        public WorldDirection ExitDirection { get; }
        public WorldDirection EntryDirection { get; }
        public MapRect GatewayAreaNormalized { get; }
    }

    public sealed class LocationMap
    {
        public LocationMap(string imagePath)
        {
            ImagePath = imagePath ?? string.Empty;
        }

        public string ImagePath { get; }
    }

    public sealed class WorldLocation
    {
        public WorldLocation(
            LocationId id,
            LocationName name,
            LocationType type,
            RegionId regionId,
            MapPoint worldMapPosition,
            BiomeType dominantBiome,
            IReadOnlyCollection<BiomeType> secondaryBiomes,
            int terrainSeed,
            string terrainSavePath,
            LocationMap map,
            IReadOnlyCollection<LocationConnection> connections,
            IReadOnlyCollection<LocationGateway> gateways,
            bool isStartLocation)
        {
            Id = id;
            Name = name;
            Type = type;
            RegionId = regionId;
            WorldMapPosition = worldMapPosition;
            DominantBiome = dominantBiome;
            SecondaryBiomes = secondaryBiomes ?? Array.Empty<BiomeType>();
            TerrainSeed = terrainSeed == 0 ? 1 : terrainSeed;
            TerrainSavePath = terrainSavePath ?? string.Empty;
            Map = map ?? new LocationMap(string.Empty);
            Connections = connections ?? Array.Empty<LocationConnection>();
            Gateways = gateways ?? Array.Empty<LocationGateway>();
            IsStartLocation = isStartLocation;
        }

        public LocationId Id { get; }
        public LocationName Name { get; }
        public LocationType Type { get; }
        public RegionId RegionId { get; }
        public MapPoint WorldMapPosition { get; }
        public BiomeType DominantBiome { get; }
        public IReadOnlyCollection<BiomeType> SecondaryBiomes { get; }
        public int TerrainSeed { get; }
        public string TerrainSavePath { get; }
        public LocationMap Map { get; }
        public IReadOnlyCollection<LocationConnection> Connections { get; }
        public IReadOnlyCollection<LocationGateway> Gateways { get; }
        public bool IsStartLocation { get; }

        public WorldLocation WithRuntimeAssets(string terrainSavePath, string locationMapImagePath)
        {
            return new WorldLocation(
                Id,
                Name,
                Type,
                RegionId,
                WorldMapPosition,
                DominantBiome,
                SecondaryBiomes,
                TerrainSeed,
                terrainSavePath,
                new LocationMap(locationMapImagePath),
                Connections,
                Gateways,
                IsStartLocation);
        }

        public WorldLocation WithConnections(
            IReadOnlyCollection<LocationConnection> connections,
            IReadOnlyCollection<LocationGateway> gateways)
        {
            return new WorldLocation(
                Id,
                Name,
                Type,
                RegionId,
                WorldMapPosition,
                DominantBiome,
                SecondaryBiomes,
                TerrainSeed,
                TerrainSavePath,
                Map,
                connections,
                gateways,
                IsStartLocation);
        }
    }
}
