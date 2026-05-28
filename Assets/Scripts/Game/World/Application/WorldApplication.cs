using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.Generator.Location;
using LegendsOfWarAndMagic.Generator.World;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Game.World.Application
{
    public sealed class WorldService
    {
        private readonly IWorldRepository repository;

        public WorldService(IWorldRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<WorldSaveSummary> ListSavedWorlds()
        {
            return repository.ListWorlds();
        }

        public GeneratedWorld Load(WorldId worldId)
        {
            return repository.Load(worldId);
        }
    }

    public sealed class NewWorldUseCase
    {
        private readonly IWorldGenerator worldGenerator;
        private readonly IWorldMapRenderer worldMapRenderer;
        private readonly ILocationTerrainGenerator locationTerrainGenerator;
        private readonly ILocationMapRenderer locationMapRenderer;
        private readonly IWorldRepository repository;

        public NewWorldUseCase(
            IWorldGenerator worldGenerator,
            IWorldMapRenderer worldMapRenderer,
            ILocationTerrainGenerator locationTerrainGenerator,
            ILocationMapRenderer locationMapRenderer,
            IWorldRepository repository)
        {
            this.worldGenerator = worldGenerator ?? throw new ArgumentNullException(nameof(worldGenerator));
            this.worldMapRenderer = worldMapRenderer ?? throw new ArgumentNullException(nameof(worldMapRenderer));
            this.locationTerrainGenerator = locationTerrainGenerator ?? throw new ArgumentNullException(nameof(locationTerrainGenerator));
            this.locationMapRenderer = locationMapRenderer ?? throw new ArgumentNullException(nameof(locationMapRenderer));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public GeneratedWorld Execute(WorldGenerationConfig config, Action<string> progress = null)
        {
            progress?.Invoke("Creating world shape...");
            var generatedWorld = worldGenerator.Generate(config, progress);
            var worldDirectory = repository.GetWorldDirectory(generatedWorld.Id);

            progress?.Invoke("Drawing world map...");
            var worldMapPath = System.IO.Path.Combine(worldDirectory, "Maps", "world_map.png");
            var mapResult = worldMapRenderer.Render(generatedWorld, worldMapPath, new WorldMapRenderSettings());
            generatedWorld = generatedWorld.WithMap(new WorldMap(mapResult.ImagePath, mapResult.Markers));

            var generatedLocations = new List<WorldLocation>();
            var index = 0;
            foreach (var location in generatedWorld.Locations)
            {
                index++;
                progress?.Invoke($"Generating locations: {index} / {generatedWorld.Locations.Count}...");
                var locationDirectory = System.IO.Path.Combine(worldDirectory, "Locations", location.Id.Value);
                var terrain = locationTerrainGenerator.Generate(generatedWorld, location, LocationGenerationConfig.CreateDefault());
                var terrainPath = terrain.Save(locationDirectory);
                var locationMapPath = System.IO.Path.Combine(locationDirectory, "location_map.png");
                var locationMap = locationMapRenderer.Render(generatedWorld, location.WithRuntimeAssets(terrainPath, locationMapPath), locationMapPath, new LocationMapRenderSettings());
                generatedLocations.Add(location.WithRuntimeAssets(terrainPath, locationMap.ImagePath));
            }

            generatedWorld = generatedWorld.WithLocations(generatedLocations);
            progress?.Invoke("Saving world...");
            repository.Save(generatedWorld);
            return generatedWorld;
        }
    }

    public sealed class LoadWorldUseCase
    {
        private readonly IWorldRepository repository;

        public LoadWorldUseCase(IWorldRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public GeneratedWorld Execute(WorldId worldId)
        {
            return repository.Load(worldId);
        }
    }

    public sealed class EnterLocationUseCase
    {
        private readonly ILocationTransitionService transitionService;

        public EnterLocationUseCase(ILocationTransitionService transitionService)
        {
            this.transitionService = transitionService ?? throw new ArgumentNullException(nameof(transitionService));
        }

        public void Execute(LocationGateway gateway)
        {
            if (gateway == null)
            {
                return;
            }

            transitionService.EnterLocation(gateway.ToLocationId, gateway.FromLocationId, gateway.EntryDirection);
        }
    }

    public sealed class GetWorldMapUseCase
    {
        public WorldMap Execute(GeneratedWorld world)
        {
            return world?.Map;
        }
    }

    public sealed class GetLocationMapUseCase
    {
        public LocationMap Execute(WorldLocation location)
        {
            return location?.Map;
        }
    }

    public static class GeneratedWorldSession
    {
        public static GeneratedWorld CurrentWorld { get; private set; }
        public static LocationId CurrentLocationId { get; private set; }
        public static LocationId PreviousLocationId { get; private set; }
        public static WorldDirection EntryDirection { get; private set; }

        public static bool HasWorld => CurrentWorld != null;

        public static WorldLocation CurrentLocation
        {
            get
            {
                if (CurrentWorld == null)
                {
                    return null;
                }

                return CurrentWorld.FindLocation(CurrentLocationId) ?? CurrentWorld.StartLocation;
            }
        }

        public static void Start(GeneratedWorld world)
        {
            CurrentWorld = world;
            var start = world?.StartLocation;
            CurrentLocationId = start?.Id ?? default;
            PreviousLocationId = default;
            EntryDirection = WorldDirection.South;
        }

        public static void Enter(LocationId targetLocationId, LocationId fromLocationId, WorldDirection entryDirection)
        {
            PreviousLocationId = fromLocationId;
            CurrentLocationId = targetLocationId;
            EntryDirection = entryDirection;
        }

        public static void Clear()
        {
            CurrentWorld = null;
            CurrentLocationId = default;
            PreviousLocationId = default;
            EntryDirection = WorldDirection.South;
        }
    }
}
