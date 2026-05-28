using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.Generator.Common
{
    public sealed class SeededRandom
    {
        private readonly Random random;

        public SeededRandom(int seed)
        {
            random = new Random(seed == 0 ? 1 : seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return (float)random.NextDouble();
        }

        public float Range(float minInclusive, float maxInclusive)
        {
            return minInclusive + (maxInclusive - minInclusive) * NextFloat();
        }

        public bool Chance(float probability)
        {
            return NextFloat() <= Clamp01(probability);
        }

        public T Pick<T>(IReadOnlyList<T> values)
        {
            if (values == null || values.Count == 0)
            {
                return default;
            }

            return values[NextInt(0, values.Count)];
        }

        public void Shuffle<T>(IList<T> values)
        {
            if (values == null)
            {
                return;
            }

            for (var i = values.Count - 1; i > 0; i--)
            {
                var j = NextInt(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        public SeededRandom Fork(int salt)
        {
            return new SeededRandom(Hash(random.Next(), salt));
        }

        public static int Hash(int seed, int salt)
        {
            unchecked
            {
                var hash = seed == 0 ? 17 : seed;
                hash = hash * 397 ^ salt;
                hash ^= hash << 13;
                hash ^= hash >> 17;
                hash ^= hash << 5;
                return hash == 0 ? 1 : hash;
            }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }
}
