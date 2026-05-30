using LegendsOfWarAndMagic.Game.World.Domain.Common;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public static class WorldUnityMapping
    {
        public static Vector2 ToVector2(this WorldPoint2D point)
        {
            return new Vector2(point.X, point.Z);
        }

        public static WorldPoint2D ToWorldPoint2D(this Vector2 point)
        {
            return new WorldPoint2D(point.x, point.y);
        }

        public static Vector2 ToVector2(this WorldSize2D size)
        {
            return new Vector2(size.Width, size.Depth);
        }

        public static WorldSize2D ToWorldSize2D(this Vector2 size)
        {
            return new WorldSize2D(size.x, size.y);
        }
    }
}
