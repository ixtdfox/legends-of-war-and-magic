using System;
using System.Collections;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    [DisallowMultipleComponent]
    public sealed class GeneratedPropChunkStreamer : MonoBehaviour
    {
        private const int MaxRuntimeLoadRadius = 1;
        private const int MaxBootstrapLoadRadius = 1;

        private readonly Dictionary<Vector2Int, PropChunkRecord> loadedChunks = new();
        private readonly List<Vector2Int> loadQueue = new();
        private readonly HashSet<Vector2Int> queuedLoads = new();

        private ProceduralLocationSettings settings;
        private IProceduralTerrainSampler sampler;
        private int seed;
        private int chunkCountX;
        private int chunkCountZ;
        private float chunkSize;
        private Transform trackingTarget;
        private bool initialized;
        private bool hasQueuedCenter;
        private Vector2Int queuedCenter;
        private int queuedRadius;
        private bool buildRoutineRunning;
        private bool hasPropExclusion;
        private Vector3 propExclusionCenter;
        private float propExclusionRadius;

        public int LoadedChunkCount => loadedChunks.Count;
        public int PendingLoadCount => loadQueue.Count;

        public void Configure(
            ProceduralLocationSettings generationSettings,
            int generationSeed,
            IProceduralTerrainSampler terrainSampler)
        {
            settings = generationSettings;
            seed = generationSeed;
            sampler = terrainSampler;
            chunkSize = Mathf.Max(32f, settings != null ? settings.TerrainChunkSize : 500f);
            chunkCountX = settings != null ? Mathf.CeilToInt(settings.WorldWidth / chunkSize) : 0;
            chunkCountZ = settings != null ? Mathf.CeilToInt(settings.WorldLength / chunkSize) : 0;
            initialized = settings != null && sampler != null && chunkCountX > 0 && chunkCountZ > 0;

            if (!initialized)
            {
                return;
            }

            LoadAroundImmediate(Vector3.zero, settings.TerrainChunkInitialLoadRadius);
        }

        public void LoadAroundImmediate(Vector3 worldPosition, int radius)
        {
            if (!initialized)
            {
                return;
            }

            RebuildLoadQueue(ResolveCenterChunk(worldPosition), ClampLoadRadius(radius, false), true);
            while (TryDequeueNext(out var coord))
            {
                var routine = CreateChunkRoutine(coord);
                while (routine.MoveNext())
                {
                }
            }
        }

        public IEnumerator PreloadAroundRoutine(Vector3 worldPosition, int radius, Action<int, int> progress)
        {
            if (!initialized)
            {
                progress?.Invoke(0, 0);
                yield break;
            }

            hasPropExclusion = true;
            propExclusionCenter = worldPosition;
            propExclusionRadius = 9.5f;

            RebuildLoadQueue(ResolveCenterChunk(worldPosition), ClampLoadRadius(radius, true), true);
            var total = loadQueue.Count;
            var completed = 0;
            progress?.Invoke(completed, total);

            while (TryDequeueNext(out var coord))
            {
                yield return CreateChunkRoutine(coord);
                completed = total - loadQueue.Count;
                progress?.Invoke(completed, total);
            }
        }

        private void Update()
        {
            if (!initialized || settings == null)
            {
                return;
            }

            if (trackingTarget == null)
            {
                trackingTarget = ResolveTrackingTarget();
            }

            if (trackingTarget == null)
            {
                return;
            }

            var position = trackingTarget.position;
            RebuildLoadQueue(ResolveCenterChunk(position), ClampLoadRadius(settings.TerrainChunkLoadRadius, false), false);
            UnloadOutside(position, ClampLoadRadius(settings.TerrainChunkLoadRadius, false) + settings.TerrainChunkUnloadBuffer);

            if (!buildRoutineRunning && loadQueue.Count > 0)
            {
                StartCoroutine(ProcessQueuedChunksRoutine());
            }
        }

        private IEnumerator ProcessQueuedChunksRoutine()
        {
            buildRoutineRunning = true;
            var maxBuilds = Mathf.Max(1, settings != null ? settings.TerrainChunkMaxBuildsPerFrame : 1);
            for (var i = 0; i < maxBuilds && TryDequeueNext(out var coord); i++)
            {
                yield return CreateChunkRoutine(coord);
            }

            buildRoutineRunning = false;
        }

        private IEnumerator CreateChunkRoutine(Vector2Int coord)
        {
            if (!IsValidChunkCoord(coord) || loadedChunks.ContainsKey(coord))
            {
                yield break;
            }

            var bounds = ResolveChunkBounds(coord);
            if (bounds.size.x <= 0f || bounds.size.z <= 0f)
            {
                yield break;
            }

            var root = new GameObject($"PropChunk_{coord.x:D2}_{coord.y:D2}");
            root.transform.SetParent(transform, false);

            var chunkSeed = ResolveChunkSeed(coord);
            var chunkContext = new GenerationContext(settings, chunkSeed, root.transform)
            {
                TerrainSampler = sampler,
                HasPropExclusion = hasPropExclusion,
                PropExclusionCenter = propExclusionCenter,
                PropExclusionRadius = propExclusionRadius
            };

            yield return PropPlacementStep.GenerateIntoRootRoutine(
                chunkContext,
                root.transform,
                bounds,
                chunkSeed,
                null);

            loadedChunks[coord] = new PropChunkRecord(coord, root);
        }

        private bool TryDequeueNext(out Vector2Int coord)
        {
            while (loadQueue.Count > 0)
            {
                coord = loadQueue[0];
                loadQueue.RemoveAt(0);
                queuedLoads.Remove(coord);

                if (IsValidChunkCoord(coord) && !loadedChunks.ContainsKey(coord))
                {
                    return true;
                }
            }

            coord = default;
            return false;
        }

        private void RebuildLoadQueue(Vector2Int center, int radius, bool force)
        {
            var safeRadius = Mathf.Max(0, radius);
            if (!force && hasQueuedCenter && queuedCenter == center && queuedRadius == safeRadius)
            {
                return;
            }

            hasQueuedCenter = true;
            queuedCenter = center;
            queuedRadius = safeRadius;
            loadQueue.Clear();
            queuedLoads.Clear();

            var candidates = new List<Vector2Int>();
            for (var z = center.y - safeRadius; z <= center.y + safeRadius; z++)
            {
                for (var x = center.x - safeRadius; x <= center.x + safeRadius; x++)
                {
                    var coord = new Vector2Int(x, z);
                    if (!IsValidChunkCoord(coord) || loadedChunks.ContainsKey(coord) || queuedLoads.Contains(coord))
                    {
                        continue;
                    }

                    candidates.Add(coord);
                }
            }

            candidates.Sort((left, right) =>
                ChunkDistanceSqr(left, center).CompareTo(ChunkDistanceSqr(right, center)));

            for (var i = 0; i < candidates.Count; i++)
            {
                loadQueue.Add(candidates[i]);
                queuedLoads.Add(candidates[i]);
            }
        }

        private void UnloadOutside(Vector3 worldPosition, int radius)
        {
            if (loadedChunks.Count == 0)
            {
                return;
            }

            var center = ClampChunkCoord(WorldToChunkCoord(worldPosition.x, worldPosition.z));
            var safeRadius = Mathf.Max(0, radius);
            var toUnload = new List<Vector2Int>();
            foreach (var pair in loadedChunks)
            {
                var coord = pair.Key;
                if (Mathf.Abs(coord.x - center.x) > safeRadius || Mathf.Abs(coord.y - center.y) > safeRadius)
                {
                    toUnload.Add(coord);
                }
            }

            toUnload.Sort((left, right) =>
                ChunkDistanceSqr(right, center).CompareTo(ChunkDistanceSqr(left, center)));

            var unloadCount = Mathf.Min(toUnload.Count, Mathf.Max(1, settings.TerrainChunkMaxUnloadsPerFrame));
            for (var i = 0; i < unloadCount; i++)
            {
                var coord = toUnload[i];
                if (!loadedChunks.TryGetValue(coord, out var record))
                {
                    continue;
                }

                SafeDestroy(record.Root);
                loadedChunks.Remove(coord);
            }
        }

        private Bounds ResolveChunkBounds(Vector2Int coord)
        {
            var worldBounds = settings.GetWorldBounds();
            var minX = worldBounds.min.x + coord.x * chunkSize;
            var minZ = worldBounds.min.z + coord.y * chunkSize;
            var width = Mathf.Min(chunkSize, worldBounds.max.x - minX);
            var length = Mathf.Min(chunkSize, worldBounds.max.z - minZ);
            return new Bounds(
                new Vector3(minX + width * 0.5f, 0f, minZ + length * 0.5f),
                new Vector3(width, settings.TerrainHeight, length));
        }

        private int ResolveChunkSeed(Vector2Int coord)
        {
            return unchecked(seed + coord.x * 73856093 + coord.y * 19349663 + 1572869);
        }

        private int ClampLoadRadius(int requestedRadius, bool bootstrap)
        {
            return Mathf.Clamp(requestedRadius, 0, bootstrap ? MaxBootstrapLoadRadius : MaxRuntimeLoadRadius);
        }

        private Vector2Int ResolveCenterChunk(Vector3 worldPosition)
        {
            if (!TryResolveChunkCoord(worldPosition.x, worldPosition.z, out var center))
            {
                center = ClampChunkCoord(WorldToChunkCoord(worldPosition.x, worldPosition.z));
            }

            return center;
        }

        private Vector2Int WorldToChunkCoord(float worldX, float worldZ)
        {
            var min = settings.GetWorldBounds().min;
            return new Vector2Int(
                Mathf.FloorToInt((worldX - min.x) / chunkSize),
                Mathf.FloorToInt((worldZ - min.z) / chunkSize));
        }

        private bool TryResolveChunkCoord(float worldX, float worldZ, out Vector2Int coord)
        {
            coord = WorldToChunkCoord(worldX, worldZ);
            return IsValidChunkCoord(coord);
        }

        private Vector2Int ClampChunkCoord(Vector2Int coord)
        {
            return new Vector2Int(
                Mathf.Clamp(coord.x, 0, Mathf.Max(0, chunkCountX - 1)),
                Mathf.Clamp(coord.y, 0, Mathf.Max(0, chunkCountZ - 1)));
        }

        private bool IsValidChunkCoord(Vector2Int coord)
        {
            return coord.x >= 0 && coord.x < chunkCountX && coord.y >= 0 && coord.y < chunkCountZ;
        }

        private static int ChunkDistanceSqr(Vector2Int coord, Vector2Int center)
        {
            var dx = coord.x - center.x;
            var dy = coord.y - center.y;
            return dx * dx + dy * dy;
        }

        private static Transform ResolveTrackingTarget()
        {
            GameObject player = null;
            try
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                player = null;
            }

            if (player != null)
            {
                return player.transform;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private static void SafeDestroy(GameObject target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }

        private readonly struct PropChunkRecord
        {
            public PropChunkRecord(Vector2Int coord, GameObject root)
            {
                Coord = coord;
                Root = root;
            }

            public Vector2Int Coord { get; }
            public GameObject Root { get; }
        }
    }
}
