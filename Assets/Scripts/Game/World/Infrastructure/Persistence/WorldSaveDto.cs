using System;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Persistence
{
    [Serializable]
    public sealed class WorldSaveDto
    {
        public string id;
        public int seed;
        public string name;
        public string shapeType;
        public float boundsWidth;
        public float boundsHeight;
        public string worldMapPath;
        public WorldRegionSaveDto[] regions;
        public WorldLocationSaveDto[] locations;
        public WorldFeatureSaveDto[] rivers;
        public WorldFeatureSaveDto[] mountainRanges;
        public WorldFeatureSaveDto[] lakes;
        public WorldFeatureSaveDto[] coastlines;
        public string generatorVersion;
        public string createdUtc;
        public string configSnapshot;
    }

    [Serializable]
    public sealed class WorldRegionSaveDto
    {
        public string id;
        public string name;
        public string type;
        public string dominantBiome;
        public string[] secondaryBiomes;
        public MapPointSaveDto[] boundary;
        public string[] locationIds;
        public string loreHook;
    }

    [Serializable]
    public sealed class WorldLocationSaveDto
    {
        public string id;
        public string name;
        public string type;
        public string regionId;
        public MapPointSaveDto position;
        public string dominantBiome;
        public string[] secondaryBiomes;
        public int terrainSeed;
        public string terrainSavePath;
        public string localMapPath;
        public LocationConnectionSaveDto[] connections;
        public LocationGatewaySaveDto[] gateways;
        public bool isStartLocation;
    }

    [Serializable]
    public sealed class LocationConnectionSaveDto
    {
        public string from;
        public string to;
        public string direction;
        public string type;
        public string displayName;
    }

    [Serializable]
    public sealed class LocationGatewaySaveDto
    {
        public string fromLocationId;
        public string toLocationId;
        public string exitDirection;
        public string entryDirection;
        public MapRectSaveDto gatewayAreaNormalized;
    }

    [Serializable]
    public sealed class WorldFeatureSaveDto
    {
        public string id;
        public string name;
        public string type;
        public MapPointSaveDto[] points;
        public float size;
    }

    [Serializable]
    public sealed class MapPointSaveDto
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class MapRectSaveDto
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }
}
