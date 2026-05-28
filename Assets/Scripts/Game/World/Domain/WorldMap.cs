using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public sealed class WorldMapPoint
    {
        public WorldMapPoint(LocationId locationId, MapPoint position, string label)
        {
            LocationId = locationId;
            Position = position;
            Label = label ?? string.Empty;
        }

        public LocationId LocationId { get; }
        public MapPoint Position { get; }
        public string Label { get; }
    }

    public sealed class WorldMap
    {
        public WorldMap(string imagePath, IReadOnlyCollection<WorldMapPoint> points)
        {
            ImagePath = imagePath ?? string.Empty;
            Points = points ?? Array.Empty<WorldMapPoint>();
        }

        public string ImagePath { get; }
        public IReadOnlyCollection<WorldMapPoint> Points { get; }
    }
}
