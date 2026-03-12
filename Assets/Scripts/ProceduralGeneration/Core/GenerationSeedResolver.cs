using System;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Core
{
    /// <summary>
    /// Centralized seed handling for deterministic and random generation runs.
    /// </summary>
    public static class GenerationSeedResolver
    {
        public static int ResolveSeed(ProceduralLocationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return settings.SeedMode == SeedMode.Fixed
                ? settings.FixedSeed
                : GenerateRandomSeed();
        }

        public static int GenerateRandomSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }
    }
}
