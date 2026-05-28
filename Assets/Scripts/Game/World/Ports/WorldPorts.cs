using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Game.World.Ports
{
    public sealed class WorldSaveSummary
    {
        public WorldSaveSummary(
            string worldId,
            string worldName,
            int seed,
            WorldShapeType shapeType,
            DateTime createdUtc,
            string mapPreviewPath)
        {
            WorldId = worldId ?? string.Empty;
            WorldName = worldName ?? string.Empty;
            Seed = seed;
            ShapeType = shapeType;
            CreatedUtc = createdUtc;
            MapPreviewPath = mapPreviewPath ?? string.Empty;
        }

        public string WorldId { get; }
        public string WorldName { get; }
        public int Seed { get; }
        public WorldShapeType ShapeType { get; }
        public DateTime CreatedUtc { get; }
        public string MapPreviewPath { get; }
    }

    public interface IWorldRepository
    {
        string SaveRootPath { get; }
        string GetWorldDirectory(WorldId worldId);
        void Save(GeneratedWorld world);
        GeneratedWorld Load(WorldId worldId);
        IReadOnlyList<WorldSaveSummary> ListWorlds();
        bool Exists(WorldId worldId);
    }

    public sealed class WorldMapRenderSettings
    {
        public WorldMapRenderSettings(int width = 1536, int height = 1024)
        {
            Width = Math.Max(256, width);
            Height = Math.Max(256, height);
        }

        public int Width { get; }
        public int Height { get; }
    }

    public sealed class LocationMapRenderSettings
    {
        public LocationMapRenderSettings(int width = 768, int height = 768)
        {
            Width = Math.Max(256, width);
            Height = Math.Max(256, height);
        }

        public int Width { get; }
        public int Height { get; }
    }

    public sealed class WorldMapRenderResult
    {
        public WorldMapRenderResult(string imagePath, IReadOnlyCollection<WorldMapPoint> markers)
        {
            ImagePath = imagePath ?? string.Empty;
            Markers = markers ?? Array.Empty<WorldMapPoint>();
        }

        public string ImagePath { get; }
        public IReadOnlyCollection<WorldMapPoint> Markers { get; }
    }

    public sealed class LocationMapRenderResult
    {
        public LocationMapRenderResult(string imagePath)
        {
            ImagePath = imagePath ?? string.Empty;
        }

        public string ImagePath { get; }
    }

    public interface IWorldMapRenderer
    {
        WorldMapRenderResult Render(GeneratedWorld world, string outputPath, WorldMapRenderSettings settings);
    }

    public interface ILocationMapRenderer
    {
        LocationMapRenderResult Render(GeneratedWorld world, WorldLocation location, string outputPath, LocationMapRenderSettings settings);
    }

    public interface ILocationSceneRepository
    {
        string SaveLocationData(GeneratedWorld world, WorldLocation location, string locationDirectory);
    }

    public interface ILocationTransitionService
    {
        void EnterLocation(LocationId targetLocationId, LocationId fromLocationId, WorldDirection entryDirection);
    }
}
