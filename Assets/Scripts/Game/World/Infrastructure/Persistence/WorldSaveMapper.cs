using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Persistence
{
    public static class WorldSaveMapper
    {
        public static WorldSaveDto ToDto(GeneratedWorld world)
        {
            return new WorldSaveDto
            {
                id = world.Id.Value,
                seed = world.Seed.Value,
                name = world.Name.Value,
                shapeType = world.Shape.Type.ToString(),
                boundsWidth = world.Bounds.Width,
                boundsHeight = world.Bounds.Height,
                worldMapPath = world.Map.ImagePath,
                regions = world.Regions.Select(ToDto).ToArray(),
                locations = world.Locations.Select(ToDto).ToArray(),
                rivers = world.Rivers.Select(river => ToDto(river, river.Width)).ToArray(),
                mountainRanges = world.MountainRanges.Select(range => ToDto(range, range.Intensity)).ToArray(),
                lakes = world.Lakes.Select(lake => ToDto(lake, lake.Radius)).ToArray(),
                coastlines = world.Coastlines.Select(coast => ToDto(coast, 0f)).ToArray(),
                generatorVersion = world.Metadata.GeneratorVersion,
                createdUtc = world.Metadata.CreatedUtc.ToString("O", CultureInfo.InvariantCulture),
                configSnapshot = world.Metadata.ConfigSnapshot
            };
        }

        public static GeneratedWorld ToDomain(WorldSaveDto dto)
        {
            if (dto == null)
            {
                return null;
            }

            var regions = (dto.regions ?? Array.Empty<WorldRegionSaveDto>()).Select(ToDomain).ToArray();
            var locations = (dto.locations ?? Array.Empty<WorldLocationSaveDto>()).Select(ToDomain).ToArray();
            var markers = locations.Select(location => new WorldMapPoint(location.Id, location.WorldMapPosition, location.Name.Value)).ToArray();

            return new GeneratedWorld(
                new WorldId(dto.id),
                new WorldSeed(dto.seed),
                new WorldName(dto.name),
                new WorldShape(ParseEnum(dto.shapeType, WorldShapeType.HugeIsland), dto.shapeType),
                new WorldBounds(dto.boundsWidth <= 0f ? 1f : dto.boundsWidth, dto.boundsHeight <= 0f ? 1f : dto.boundsHeight),
                regions,
                locations,
                (dto.rivers ?? Array.Empty<WorldFeatureSaveDto>()).Select(ToRiver).ToArray(),
                (dto.mountainRanges ?? Array.Empty<WorldFeatureSaveDto>()).Select(ToMountainRange).ToArray(),
                (dto.lakes ?? Array.Empty<WorldFeatureSaveDto>()).Select(ToLake).ToArray(),
                (dto.coastlines ?? Array.Empty<WorldFeatureSaveDto>()).Select(ToCoastline).ToArray(),
                new WorldMap(dto.worldMapPath, markers),
                new WorldGenerationMetadata(
                    dto.generatorVersion,
                    ParseDate(dto.createdUtc),
                    dto.configSnapshot));
        }

        private static WorldRegionSaveDto ToDto(WorldRegion region)
        {
            return new WorldRegionSaveDto
            {
                id = region.Id.Value,
                name = region.Name.Value,
                type = region.Type.ToString(),
                dominantBiome = region.DominantBiome.ToString(),
                secondaryBiomes = region.SecondaryBiomes.Select(biome => biome.ToString()).ToArray(),
                boundary = region.Boundary.Points.Select(ToDto).ToArray(),
                locationIds = region.LocationIds.Select(locationId => locationId.Value).ToArray(),
                loreHook = region.LoreHook
            };
        }

        private static WorldLocationSaveDto ToDto(WorldLocation location)
        {
            return new WorldLocationSaveDto
            {
                id = location.Id.Value,
                name = location.Name.Value,
                type = location.Type.ToString(),
                regionId = location.RegionId.Value,
                position = ToDto(location.WorldMapPosition),
                dominantBiome = location.DominantBiome.ToString(),
                secondaryBiomes = location.SecondaryBiomes.Select(biome => biome.ToString()).ToArray(),
                terrainSeed = location.TerrainSeed,
                terrainSavePath = location.TerrainSavePath,
                localMapPath = location.Map.ImagePath,
                connections = location.Connections.Select(ToDto).ToArray(),
                gateways = location.Gateways.Select(ToDto).ToArray(),
                isStartLocation = location.IsStartLocation
            };
        }

        private static LocationConnectionSaveDto ToDto(LocationConnection connection)
        {
            return new LocationConnectionSaveDto
            {
                from = connection.From.Value,
                to = connection.To.Value,
                direction = connection.Direction.ToString(),
                type = connection.Type.ToString(),
                displayName = connection.DisplayName
            };
        }

        private static LocationGatewaySaveDto ToDto(LocationGateway gateway)
        {
            return new LocationGatewaySaveDto
            {
                fromLocationId = gateway.FromLocationId.Value,
                toLocationId = gateway.ToLocationId.Value,
                exitDirection = gateway.ExitDirection.ToString(),
                entryDirection = gateway.EntryDirection.ToString(),
                gatewayAreaNormalized = ToDto(gateway.GatewayAreaNormalized)
            };
        }

        private static WorldFeatureSaveDto ToDto(WorldFeature feature, float size)
        {
            return new WorldFeatureSaveDto
            {
                id = feature.Id,
                name = feature.Name,
                type = feature.Type.ToString(),
                points = feature.Points.Select(ToDto).ToArray(),
                size = size
            };
        }

        private static MapPointSaveDto ToDto(MapPoint point)
        {
            return new MapPointSaveDto { x = point.X, y = point.Y };
        }

        private static MapRectSaveDto ToDto(MapRect rect)
        {
            return new MapRectSaveDto { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height };
        }

        private static WorldRegion ToDomain(WorldRegionSaveDto dto)
        {
            return new WorldRegion(
                new RegionId(dto.id),
                new RegionName(dto.name),
                ParseEnum(dto.type, RegionType.Mixed),
                ParseEnum(dto.dominantBiome, BiomeType.Grassland),
                ParseBiomes(dto.secondaryBiomes),
                new RegionBoundary((dto.boundary ?? Array.Empty<MapPointSaveDto>()).Select(ToDomain).ToArray()),
                (dto.locationIds ?? Array.Empty<string>()).Select(id => new LocationId(id)).ToArray(),
                dto.loreHook);
        }

        private static WorldLocation ToDomain(WorldLocationSaveDto dto)
        {
            return new WorldLocation(
                new LocationId(dto.id),
                new LocationName(dto.name),
                ParseEnum(dto.type, LocationType.Wilderness),
                new RegionId(dto.regionId),
                ToDomain(dto.position),
                ParseEnum(dto.dominantBiome, BiomeType.Grassland),
                ParseBiomes(dto.secondaryBiomes),
                dto.terrainSeed,
                dto.terrainSavePath,
                new LocationMap(dto.localMapPath),
                (dto.connections ?? Array.Empty<LocationConnectionSaveDto>()).Select(ToDomain).ToArray(),
                (dto.gateways ?? Array.Empty<LocationGatewaySaveDto>()).Select(ToDomain).ToArray(),
                dto.isStartLocation);
        }

        private static LocationConnection ToDomain(LocationConnectionSaveDto dto)
        {
            return new LocationConnection(
                new LocationId(dto.from),
                new LocationId(dto.to),
                ParseEnum(dto.direction, WorldDirection.North),
                ParseEnum(dto.type, LocationConnectionType.LandPath),
                dto.displayName);
        }

        private static LocationGateway ToDomain(LocationGatewaySaveDto dto)
        {
            return new LocationGateway(
                new LocationId(dto.fromLocationId),
                new LocationId(dto.toLocationId),
                ParseEnum(dto.exitDirection, WorldDirection.North),
                ParseEnum(dto.entryDirection, WorldDirection.South),
                ToDomain(dto.gatewayAreaNormalized));
        }

        private static River ToRiver(WorldFeatureSaveDto dto)
        {
            return new River(dto.id, dto.name, ParsePoints(dto.points), dto.size <= 0f ? 0.008f : dto.size);
        }

        private static MountainRange ToMountainRange(WorldFeatureSaveDto dto)
        {
            return new MountainRange(dto.id, dto.name, ParsePoints(dto.points), dto.size);
        }

        private static Lake ToLake(WorldFeatureSaveDto dto)
        {
            return new Lake(dto.id, dto.name, ParsePoints(dto.points), dto.size <= 0f ? 0.02f : dto.size);
        }

        private static Coastline ToCoastline(WorldFeatureSaveDto dto)
        {
            return new Coastline(dto.id, dto.name, ParsePoints(dto.points));
        }

        private static IReadOnlyCollection<MapPoint> ParsePoints(MapPointSaveDto[] points)
        {
            return (points ?? Array.Empty<MapPointSaveDto>()).Select(ToDomain).ToArray();
        }

        private static MapPoint ToDomain(MapPointSaveDto dto)
        {
            return dto == null ? new MapPoint(0.5f, 0.5f) : new MapPoint(dto.x, dto.y);
        }

        private static MapRect ToDomain(MapRectSaveDto dto)
        {
            return dto == null ? new MapRect(0f, 0f, 0f, 0f) : new MapRect(dto.x, dto.y, dto.width, dto.height);
        }

        private static IReadOnlyCollection<BiomeType> ParseBiomes(string[] biomes)
        {
            return (biomes ?? Array.Empty<string>()).Select(value => ParseEnum(value, BiomeType.Grassland)).ToArray();
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            return Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;
        }
    }
}
