using System;

namespace LegendsOfWarAndMagic.Generator.Common.Noise
{
    public interface INoiseProvider
    {
        float Sample(float x, float y, int seed);
        float Fractal(float x, float y, int seed, int octaves, float persistence, float lacunarity);
    }

    public sealed class ValueNoiseProvider : INoiseProvider
    {
        public float Sample(float x, float y, int seed)
        {
            var x0 = FastFloor(x);
            var y0 = FastFloor(y);
            var x1 = x0 + 1;
            var y1 = y0 + 1;
            var sx = Smooth(x - x0);
            var sy = Smooth(y - y0);

            var n00 = Hash01(x0, y0, seed);
            var n10 = Hash01(x1, y0, seed);
            var n01 = Hash01(x0, y1, seed);
            var n11 = Hash01(x1, y1, seed);
            var ix0 = Lerp(n00, n10, sx);
            var ix1 = Lerp(n01, n11, sx);
            return Lerp(ix0, ix1, sy);
        }

        public float Fractal(float x, float y, int seed, int octaves, float persistence, float lacunarity)
        {
            var amplitude = 1f;
            var frequency = 1f;
            var value = 0f;
            var max = 0f;

            for (var i = 0; i < Math.Max(1, octaves); i++)
            {
                value += Sample(x * frequency, y * frequency, seed + i * 1013) * amplitude;
                max += amplitude;
                amplitude *= Clamp(persistence, 0.05f, 0.95f);
                frequency *= Math.Max(1f, lacunarity);
            }

            return max <= 0f ? 0f : Clamp01(value / max);
        }

        private static int FastFloor(float value)
        {
            var i = (int)value;
            return value < i ? i - 1 : i;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                var hash = seed;
                hash ^= x * 374761393;
                hash = (hash << 13) ^ hash;
                hash ^= y * 668265263;
                hash = hash * 1274126177;
                hash ^= hash >> 16;
                return (hash & 0x7fffffff) / (float)int.MaxValue;
            }
        }

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
