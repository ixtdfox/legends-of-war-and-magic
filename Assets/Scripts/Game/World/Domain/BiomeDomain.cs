using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Game.World.Domain
{
    public enum BiomeType
    {
        TemperateForest,
        DarkForest,
        Grassland,
        Highlands,
        Mountains,
        Swamp,
        Desert,
        AshDesert,
        Tundra,
        SnowPeaks,
        TropicalCoast,
        RockyCoast,
        Riverlands,
        LakeDistrict,
        Wasteland
    }

    public readonly struct BiomeId
    {
        public BiomeId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim();
        }

        public string Value { get; }

        public override string ToString()
        {
            return Value;
        }
    }

    public sealed class Biome
    {
        public Biome(BiomeId id, BiomeProfile profile)
        {
            Id = id;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public BiomeId Id { get; }
        public BiomeProfile Profile { get; }
        public BiomeType Type => Profile.Type;
    }

    public sealed class BiomeProfile
    {
        public BiomeProfile(
            BiomeType type,
            float minTemperature,
            float maxTemperature,
            float minHumidity,
            float maxHumidity,
            float minElevation,
            float maxElevation,
            MapColor mapColor,
            IReadOnlyCollection<BiomeType> preferredNeighbors)
        {
            Type = type;
            MinTemperature = minTemperature;
            MaxTemperature = maxTemperature;
            MinHumidity = minHumidity;
            MaxHumidity = maxHumidity;
            MinElevation = minElevation;
            MaxElevation = maxElevation;
            MapColor = mapColor;
            PreferredNeighbors = preferredNeighbors ?? Array.Empty<BiomeType>();
        }

        public BiomeType Type { get; }
        public float MinTemperature { get; }
        public float MaxTemperature { get; }
        public float MinHumidity { get; }
        public float MaxHumidity { get; }
        public float MinElevation { get; }
        public float MaxElevation { get; }
        public MapColor MapColor { get; }
        public IReadOnlyCollection<BiomeType> PreferredNeighbors { get; }

        public float Score(float temperature, float humidity, float elevation)
        {
            var temperatureScore = RangeScore(temperature, MinTemperature, MaxTemperature);
            var humidityScore = RangeScore(humidity, MinHumidity, MaxHumidity);
            var elevationScore = RangeScore(elevation, MinElevation, MaxElevation);
            return temperatureScore * 0.38f + humidityScore * 0.32f + elevationScore * 0.30f;
        }

        private static float RangeScore(float value, float min, float max)
        {
            if (value >= min && value <= max)
            {
                return 1f;
            }

            var distance = value < min ? min - value : value - max;
            return Math.Max(0f, 1f - distance * 2.2f);
        }
    }

    public sealed class BiomeTransition
    {
        public BiomeTransition(BiomeType from, BiomeType to, float strength)
        {
            From = from;
            To = to;
            Strength = Math.Max(0f, strength);
        }

        public BiomeType From { get; }
        public BiomeType To { get; }
        public float Strength { get; }
    }

    public static class BiomeCatalog
    {
        private static readonly IReadOnlyDictionary<BiomeType, BiomeProfile> ProfilesByType = BuildProfiles();

        public static IEnumerable<BiomeProfile> AllProfiles => ProfilesByType.Values;

        public static BiomeProfile Get(BiomeType type)
        {
            return ProfilesByType[type];
        }

        public static BiomeProfile Select(float temperature, float humidity, float elevation, bool coastal, bool riverOrLake)
        {
            if (riverOrLake && elevation < 0.72f)
            {
                return humidity > 0.65f ? Get(BiomeType.Riverlands) : Get(BiomeType.LakeDistrict);
            }

            if (coastal && elevation < 0.42f)
            {
                return temperature > 0.58f && humidity > 0.50f
                    ? Get(BiomeType.TropicalCoast)
                    : Get(BiomeType.RockyCoast);
            }

            var bestProfile = Get(BiomeType.Grassland);
            var bestScore = float.MinValue;
            foreach (var profile in AllProfiles)
            {
                var score = profile.Score(temperature, humidity, elevation);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestProfile = profile;
                }
            }

            return bestProfile;
        }

        private static IReadOnlyDictionary<BiomeType, BiomeProfile> BuildProfiles()
        {
            var profiles = new Dictionary<BiomeType, BiomeProfile>
            {
                [BiomeType.TemperateForest] = Profile(BiomeType.TemperateForest, 0.34f, 0.76f, 0.48f, 0.92f, 0.10f, 0.62f, new MapColor(0.24f, 0.48f, 0.24f), BiomeType.Grassland, BiomeType.DarkForest, BiomeType.Riverlands),
                [BiomeType.DarkForest] = Profile(BiomeType.DarkForest, 0.22f, 0.62f, 0.58f, 1.00f, 0.16f, 0.68f, new MapColor(0.10f, 0.30f, 0.20f), BiomeType.TemperateForest, BiomeType.Swamp, BiomeType.Highlands),
                [BiomeType.Grassland] = Profile(BiomeType.Grassland, 0.32f, 0.86f, 0.22f, 0.68f, 0.06f, 0.52f, new MapColor(0.50f, 0.66f, 0.28f), BiomeType.TemperateForest, BiomeType.Highlands, BiomeType.Riverlands),
                [BiomeType.Highlands] = Profile(BiomeType.Highlands, 0.18f, 0.62f, 0.22f, 0.72f, 0.42f, 0.78f, new MapColor(0.43f, 0.51f, 0.34f), BiomeType.Grassland, BiomeType.Mountains, BiomeType.Tundra),
                [BiomeType.Mountains] = Profile(BiomeType.Mountains, 0.05f, 0.56f, 0.05f, 0.82f, 0.66f, 0.93f, new MapColor(0.47f, 0.45f, 0.39f), BiomeType.Highlands, BiomeType.SnowPeaks, BiomeType.RockyCoast),
                [BiomeType.Swamp] = Profile(BiomeType.Swamp, 0.38f, 0.88f, 0.76f, 1.00f, 0.00f, 0.34f, new MapColor(0.24f, 0.36f, 0.20f), BiomeType.DarkForest, BiomeType.Riverlands, BiomeType.LakeDistrict),
                [BiomeType.Desert] = Profile(BiomeType.Desert, 0.66f, 1.00f, 0.00f, 0.26f, 0.04f, 0.58f, new MapColor(0.73f, 0.61f, 0.34f), BiomeType.Wasteland, BiomeType.AshDesert, BiomeType.RockyCoast),
                [BiomeType.AshDesert] = Profile(BiomeType.AshDesert, 0.52f, 1.00f, 0.00f, 0.34f, 0.22f, 0.72f, new MapColor(0.40f, 0.36f, 0.32f), BiomeType.Desert, BiomeType.Wasteland, BiomeType.Mountains),
                [BiomeType.Tundra] = Profile(BiomeType.Tundra, 0.00f, 0.28f, 0.16f, 0.72f, 0.12f, 0.72f, new MapColor(0.62f, 0.68f, 0.63f), BiomeType.Highlands, BiomeType.SnowPeaks, BiomeType.Mountains),
                [BiomeType.SnowPeaks] = Profile(BiomeType.SnowPeaks, 0.00f, 0.22f, 0.00f, 0.92f, 0.72f, 1.00f, new MapColor(0.86f, 0.88f, 0.84f), BiomeType.Mountains, BiomeType.Tundra),
                [BiomeType.TropicalCoast] = Profile(BiomeType.TropicalCoast, 0.62f, 1.00f, 0.48f, 1.00f, 0.00f, 0.42f, new MapColor(0.34f, 0.62f, 0.38f), BiomeType.TemperateForest, BiomeType.Riverlands, BiomeType.RockyCoast),
                [BiomeType.RockyCoast] = Profile(BiomeType.RockyCoast, 0.12f, 0.86f, 0.10f, 0.82f, 0.00f, 0.56f, new MapColor(0.48f, 0.48f, 0.38f), BiomeType.Mountains, BiomeType.TropicalCoast, BiomeType.Grassland),
                [BiomeType.Riverlands] = Profile(BiomeType.Riverlands, 0.22f, 0.86f, 0.48f, 1.00f, 0.02f, 0.54f, new MapColor(0.33f, 0.56f, 0.34f), BiomeType.Grassland, BiomeType.TemperateForest, BiomeType.Swamp),
                [BiomeType.LakeDistrict] = Profile(BiomeType.LakeDistrict, 0.16f, 0.76f, 0.52f, 1.00f, 0.00f, 0.48f, new MapColor(0.36f, 0.55f, 0.45f), BiomeType.Riverlands, BiomeType.Swamp, BiomeType.TemperateForest),
                [BiomeType.Wasteland] = Profile(BiomeType.Wasteland, 0.28f, 0.86f, 0.00f, 0.38f, 0.10f, 0.78f, new MapColor(0.46f, 0.42f, 0.30f), BiomeType.Desert, BiomeType.AshDesert, BiomeType.Mountains)
            };

            return profiles;
        }

        private static BiomeProfile Profile(
            BiomeType type,
            float minTemperature,
            float maxTemperature,
            float minHumidity,
            float maxHumidity,
            float minElevation,
            float maxElevation,
            MapColor color,
            params BiomeType[] neighbors)
        {
            return new BiomeProfile(type, minTemperature, maxTemperature, minHumidity, maxHumidity, minElevation, maxElevation, color, neighbors);
        }
    }
}
