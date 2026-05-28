using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public sealed class World
    {
        public World(
            WorldId id,
            WorldSeed seed,
            WorldName name,
            WorldShape shape,
            WorldBounds bounds,
            IReadOnlyCollection<WorldRegion> regions,
            IReadOnlyCollection<WorldLocation> locations,
            IReadOnlyCollection<River> rivers,
            IReadOnlyCollection<MountainRange> mountainRanges,
            IReadOnlyCollection<Lake> lakes,
            IReadOnlyCollection<Coastline> coastlines,
            WorldMap map,
            WorldGenerationMetadata metadata)
        {
            Id = id;
            Seed = seed;
            Name = name;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Bounds = bounds;
            Regions = regions ?? Array.Empty<WorldRegion>();
            Locations = locations ?? Array.Empty<WorldLocation>();
            Rivers = rivers ?? Array.Empty<River>();
            MountainRanges = mountainRanges ?? Array.Empty<MountainRange>();
            Lakes = lakes ?? Array.Empty<Lake>();
            Coastlines = coastlines ?? Array.Empty<Coastline>();
            Map = map ?? new WorldMap(string.Empty, Array.Empty<WorldMapPoint>());
            Metadata = metadata ?? new WorldGenerationMetadata("unknown", DateTime.UtcNow, string.Empty);
        }

        public WorldId Id { get; }
        public WorldSeed Seed { get; }
        public WorldName Name { get; }
        public WorldShape Shape { get; }
        public WorldBounds Bounds { get; }
        public IReadOnlyCollection<WorldRegion> Regions { get; }
        public IReadOnlyCollection<WorldLocation> Locations { get; }
        public IReadOnlyCollection<River> Rivers { get; }
        public IReadOnlyCollection<MountainRange> MountainRanges { get; }
        public IReadOnlyCollection<Lake> Lakes { get; }
        public IReadOnlyCollection<Coastline> Coastlines { get; }
        public WorldMap Map { get; }
        public WorldGenerationMetadata Metadata { get; }

        public WorldLocation StartLocation => Locations.FirstOrDefault(location => location.IsStartLocation) ?? Locations.FirstOrDefault();

        public WorldLocation FindLocation(LocationId locationId)
        {
            foreach (var location in Locations)
            {
                if (location.Id.Equals(locationId))
                {
                    return location;
                }
            }

            return null;
        }

        public World WithMap(WorldMap map)
        {
            return new World(Id, Seed, Name, Shape, Bounds, Regions, Locations, Rivers, MountainRanges, Lakes, Coastlines, map, Metadata);
        }

        public World WithLocations(IReadOnlyCollection<WorldLocation> locations)
        {
            var mapPoints = new List<WorldMapPoint>();
            foreach (var location in locations ?? Array.Empty<WorldLocation>())
            {
                mapPoints.Add(new WorldMapPoint(location.Id, location.WorldMapPosition, location.Name.Value));
            }

            var map = new WorldMap(Map.ImagePath, mapPoints);
            return new World(Id, Seed, Name, Shape, Bounds, Regions, locations, Rivers, MountainRanges, Lakes, Coastlines, map, Metadata);
        }
    }
}
