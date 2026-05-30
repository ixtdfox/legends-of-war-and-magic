using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial
{
    public interface ISpatialIndex2D<T>
    {
        void Insert(Bounds2D bounds, T item);
        IReadOnlyList<T> Query(Bounds2D area);
        IReadOnlyList<T> QueryRadius(Vector2 center, float radius);
        bool Any(Bounds2D area);
        void Clear();
    }
}
