using System;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    public enum MapSizeOption
    {
        Small,
        Medium,
        Large
    }

    public enum LandTypeOption
    {
        Mainland,
        Islands,
        Archipelago
    }

    public enum WaterAmountOption
    {
        Low,
        Normal,
        High
    }

    public enum ReliefOption
    {
        Plains,
        Hills,
        Mountains
    }

    public enum PropDensityOption
    {
        Low,
        Normal,
        High
    }

    [Serializable]
    public sealed class MapGenerationRequest
    {
        public MapSizeOption MapSize { get; set; } = MapSizeOption.Medium;
        public LandTypeOption LandType { get; set; } = LandTypeOption.Mainland;
        public WaterAmountOption WaterAmount { get; set; } = WaterAmountOption.Normal;
        public ReliefOption Relief { get; set; } = ReliefOption.Hills;
        public PropDensityOption PropDensity { get; set; } = PropDensityOption.Normal;
        public float TreeDensity { get; set; } = 0.72f;
        public string SeedText { get; set; } = string.Empty;

        public static MapGenerationRequest CreateDefault()
        {
            return new MapGenerationRequest();
        }

        public int ResolveSeed()
        {
            if (string.IsNullOrWhiteSpace(SeedText))
            {
                return Guid.NewGuid().GetHashCode();
            }

            return int.TryParse(SeedText.Trim(), out var parsedSeed)
                ? parsedSeed
                : StableStringHash(SeedText.Trim());
        }

        public string BuildSummary(int resolvedSeed)
        {
            return $"Size={MapSize}, Land={LandType}, Water={WaterAmount}, Relief={Relief}, Props={PropDensity}, Forest={Clamp01(TreeDensity):P0}, Seed={resolvedSeed}";
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private static int StableStringHash(string value)
        {
            unchecked
            {
                var hash = 23;
                for (var i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return hash == 0 ? 1 : hash;
            }
        }
    }
}
