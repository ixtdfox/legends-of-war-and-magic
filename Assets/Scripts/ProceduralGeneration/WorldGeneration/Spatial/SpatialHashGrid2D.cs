using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial
{
    public sealed class SpatialHashGrid2D<T> : ISpatialIndex2D<T>
    {
        private readonly Dictionary<Vector2Int, List<Entry>> buckets = new();
        private readonly List<T> queryResults = new();
        private readonly HashSet<int> queryIds = new();
        private int nextId;

        public SpatialHashGrid2D(float cellSize)
        {
            CellSize = Mathf.Max(0.1f, cellSize);
        }

        public float CellSize { get; }
        public int Count { get; private set; }

        public void Insert(Bounds2D bounds, T item)
        {
            var id = nextId++;
            var entry = new Entry(id, bounds, item);
            var min = WorldToCell(bounds.Min);
            var max = WorldToCell(bounds.Max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var key = new Vector2Int(x, y);
                    if (!buckets.TryGetValue(key, out var entries))
                    {
                        entries = new List<Entry>();
                        buckets.Add(key, entries);
                    }

                    entries.Add(entry);
                }
            }

            Count++;
        }

        public IReadOnlyList<T> Query(Bounds2D area)
        {
            queryResults.Clear();
            queryIds.Clear();

            var min = WorldToCell(area.Min);
            var max = WorldToCell(area.Max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!buckets.TryGetValue(new Vector2Int(x, y), out var entries))
                    {
                        continue;
                    }

                    for (var i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        if (!queryIds.Add(entry.Id) || !entry.Bounds.Intersects(area))
                        {
                            continue;
                        }

                        queryResults.Add(entry.Item);
                    }
                }
            }

            return queryResults;
        }

        public IReadOnlyList<T> QueryRadius(Vector2 center, float radius)
        {
            queryResults.Clear();
            queryIds.Clear();

            var safeRadius = Mathf.Max(0f, radius);
            var area = Bounds2D.FromCircle(center, safeRadius);
            var min = WorldToCell(area.Min);
            var max = WorldToCell(area.Max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!buckets.TryGetValue(new Vector2Int(x, y), out var entries))
                    {
                        continue;
                    }

                    for (var i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        if (!queryIds.Add(entry.Id) || !entry.Bounds.IntersectsCircle(center, safeRadius))
                        {
                            continue;
                        }

                        queryResults.Add(entry.Item);
                    }
                }
            }

            return queryResults;
        }

        public bool Any(Bounds2D area)
        {
            var min = WorldToCell(area.Min);
            var max = WorldToCell(area.Max);
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!buckets.TryGetValue(new Vector2Int(x, y), out var entries))
                    {
                        continue;
                    }

                    for (var i = 0; i < entries.Count; i++)
                    {
                        if (entries[i].Bounds.Intersects(area))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Clear()
        {
            buckets.Clear();
            queryResults.Clear();
            queryIds.Clear();
            Count = 0;
            nextId = 0;
        }

        private Vector2Int WorldToCell(Vector2 point)
        {
            return new Vector2Int(
                Mathf.FloorToInt(point.x / CellSize),
                Mathf.FloorToInt(point.y / CellSize));
        }

        private readonly struct Entry
        {
            public Entry(int id, Bounds2D bounds, T item)
            {
                Id = id;
                Bounds = bounds;
                Item = item;
            }

            public int Id { get; }
            public Bounds2D Bounds { get; }
            public T Item { get; }
        }
    }
}
