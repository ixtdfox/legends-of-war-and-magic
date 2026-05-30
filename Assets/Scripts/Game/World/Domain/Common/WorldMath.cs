namespace LegendsOfWarAndMagic.Game.World.Domain.Common
{
    public static class WorldMath
    {
        public static int Max(int left, int right)
        {
            return left > right ? left : right;
        }

        public static float Max(float left, float right)
        {
            return left > right ? left : right;
        }

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }
}
