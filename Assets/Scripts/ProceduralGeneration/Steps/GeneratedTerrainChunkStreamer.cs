using System;
using System.Collections;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    [DisallowMultipleComponent]
    public sealed class GeneratedTerrainChunkStreamer : MonoBehaviour, IProceduralTerrainSampler
    {
        private readonly Dictionary<Vector2Int, TerrainChunkRecord> loadedChunks = new();
        private readonly List<Terrain> loadedTerrains = new();
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

        public Bounds WorldBounds => sampler != null ? sampler.WorldBounds : default;
        public int LoadedChunkCount => loadedChunks.Count;
        public int PendingLoadCount => loadQueue.Count;
        public int ChunkCountX => chunkCountX;
        public int ChunkCountZ => chunkCountZ;
        public IReadOnlyList<Terrain> LoadedTerrains
        {
            get
            {
                loadedTerrains.Clear();
                foreach (var pair in loadedChunks)
                {
                    if (pair.Value.Terrain != null)
                    {
                        loadedTerrains.Add(pair.Value.Terrain);
                    }
                }

                return loadedTerrains;
            }
        }

        public void Configure(ProceduralLocationSettings generationSettings, int generationSeed, IProceduralTerrainSampler terrainSampler)
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

            var center = ResolveCenterChunk(worldPosition);
            RebuildLoadQueue(center, radius, true);
            var topologyChanged = ProcessLoadQueue(int.MaxValue);
            if (topologyChanged)
            {
                RefreshNeighborsAndStitch();
            }

            ApplyLods(worldPosition);
        }

        public IEnumerator PreloadAroundRoutine(Vector3 worldPosition, int radius, Action<int, int> progress)
        {
            if (!initialized)
            {
                progress?.Invoke(0, 0);
                yield break;
            }

            var center = ResolveCenterChunk(worldPosition);
            RebuildLoadQueue(center, radius, true);
            var total = loadQueue.Count;
            var completed = 0;
            progress?.Invoke(completed, total);

            while (loadQueue.Count > 0)
            {
                var changed = ProcessLoadQueue(1);
                completed = total - loadQueue.Count;
                if (changed)
                {
                    RefreshNeighborsAndStitch();
                }

                ApplyLods(worldPosition);
                progress?.Invoke(completed, total);
                yield return null;
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
            var center = ResolveCenterChunk(position);
            RebuildLoadQueue(center, settings.TerrainChunkLoadRadius, false);
            var topologyChanged = ProcessLoadQueue(settings.TerrainChunkMaxBuildsPerFrame);
            topologyChanged |= UnloadOutside(
                position,
                settings.TerrainChunkLoadRadius + settings.TerrainChunkUnloadBuffer,
                settings.TerrainChunkMaxUnloadsPerFrame);
            if (topologyChanged)
            {
                RefreshNeighborsAndStitch();
            }

            ApplyLods(position);
        }

        public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
        {
            if (sampler != null)
            {
                return sampler.TrySample(worldX, worldZ, out point, out normal);
            }

            point = default;
            normal = default;
            return false;
        }

        public float SampleHeight01(float worldX, float worldZ)
        {
            return sampler != null ? sampler.SampleHeight01(worldX, worldZ) : 0f;
        }

        public float SampleHeightMeters(float worldX, float worldZ)
        {
            return sampler != null ? sampler.SampleHeightMeters(worldX, worldZ) : 0f;
        }

        public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
        {
            return sampler != null ? sampler.SampleNormal(worldX, worldZ, sampleDistance) : Vector3.up;
        }

        public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
        {
            return sampler != null ? sampler.BuildHeightMap(resolution, minX, minZ, width, length) : new float[Mathf.Max(2, resolution), Mathf.Max(2, resolution)];
        }

        private Vector2Int ResolveCenterChunk(Vector3 worldPosition)
        {
            if (!TryResolveChunkCoord(worldPosition.x, worldPosition.z, out var center))
            {
                center = ClampChunkCoord(WorldToChunkCoord(worldPosition.x, worldPosition.z));
            }

            return center;
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

        private bool ProcessLoadQueue(int maxChunks)
        {
            var safeMax = Mathf.Max(1, maxChunks);
            var changed = false;
            for (var i = 0; i < safeMax && loadQueue.Count > 0; i++)
            {
                var coord = loadQueue[0];
                loadQueue.RemoveAt(0);
                queuedLoads.Remove(coord);

                if (!IsValidChunkCoord(coord) || loadedChunks.ContainsKey(coord))
                {
                    continue;
                }

                var record = CreateChunk(coord);
                if (record.Terrain == null)
                {
                    continue;
                }

                loadedChunks.Add(coord, record);
                changed = true;
            }

            return changed;
        }

        private bool UnloadOutside(Vector3 worldPosition, int radius, int maxUnloads)
        {
            if (loadedChunks.Count == 0)
            {
                return false;
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

            var unloadCount = Mathf.Min(toUnload.Count, Mathf.Max(1, maxUnloads));
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

            return unloadCount > 0;
        }

        private TerrainChunkRecord CreateChunk(Vector2Int coord)
        {
            var min = WorldBounds.min;
            var max = WorldBounds.max;
            var minX = min.x + coord.x * chunkSize;
            var minZ = min.z + coord.y * chunkSize;
            var width = Mathf.Min(chunkSize, max.x - minX);
            var length = Mathf.Min(chunkSize, max.z - minZ);
            if (width <= 0f || length <= 0f)
            {
                return default;
            }

            var terrainData = new TerrainData
            {
                heightmapResolution = SanitizeHeightmapResolution(settings.TerrainChunkHeightmapResolution),
                size = new Vector3(width, settings.TerrainHeight, length)
            };
            terrainData.SetHeights(0, 0, sampler.BuildHeightMap(terrainData.heightmapResolution, minX, minZ, width, length));

            var terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = $"TerrainChunk_{coord.x:D2}_{coord.y:D2}";
            terrainObject.transform.SetParent(transform, false);
            terrainObject.transform.position = new Vector3(minX, 0f, minZ);

            var terrain = terrainObject.GetComponent<Terrain>();
            ConfigureTerrainRenderCost(terrain, ResolvePixelError(2));
            GeneratedTerrainVisuals.Apply(terrain, settings, seed);
            TerrainDetailGenerationStep.ApplyToTerrain(settings, terrain, ResolveChunkSeed(coord), null);
            AddChunkGrassRenderer(terrain, coord);

            return new TerrainChunkRecord(coord, terrainObject, terrain);
        }

        private void AddChunkGrassRenderer(Terrain terrain, Vector2Int coord)
        {
            if (terrain == null || settings == null || settings.GpuGrassSettings == null || !settings.GpuGrassSettings.Enabled)
            {
                return;
            }

            var grassObject = new GameObject($"GeneratedGpuGrass_{coord.x:D2}_{coord.y:D2}");
            grassObject.transform.SetParent(terrain.transform, false);
            var renderer = grassObject.AddComponent<GeneratedGpuGrassRenderer>();
            renderer.Initialize(terrain, ResolveChunkSeed(coord), settings.GpuGrassSettings, settings.WaterLevel);
        }

        private int ResolveChunkSeed(Vector2Int coord)
        {
            return unchecked(seed + coord.x * 73856093 + coord.y * 19349663);
        }

        private void RefreshNeighborsAndStitch()
        {
            // Unity Terrain uses linked neighbours for LOD edge triangulation; matching border samples removes residual cracks.
            foreach (var pair in loadedChunks)
            {
                var coord = pair.Key;
                var terrain = pair.Value.Terrain;
                if (terrain == null)
                {
                    continue;
                }

                terrain.SetNeighbors(
                    TryGetTerrain(new Vector2Int(coord.x - 1, coord.y)),
                    TryGetTerrain(new Vector2Int(coord.x, coord.y + 1)),
                    TryGetTerrain(new Vector2Int(coord.x + 1, coord.y)),
                    TryGetTerrain(new Vector2Int(coord.x, coord.y - 1)));
            }

            foreach (var pair in loadedChunks)
            {
                var coord = pair.Key;
                if (loadedChunks.TryGetValue(new Vector2Int(coord.x + 1, coord.y), out var east))
                {
                    StitchEastWest(pair.Value.Terrain, east.Terrain);
                }

                if (loadedChunks.TryGetValue(new Vector2Int(coord.x, coord.y + 1), out var north))
                {
                    StitchSouthNorth(pair.Value.Terrain, north.Terrain);
                }
            }
        }

        private void ApplyLods(Vector3 worldPosition)
        {
            foreach (var pair in loadedChunks)
            {
                var record = pair.Value;
                if (record.Terrain == null)
                {
                    continue;
                }

                var lod = ResolveLod(record.Bounds, worldPosition);
                record.Terrain.heightmapPixelError = ResolvePixelError(lod);
            }
        }

        private int ResolveLod(Bounds bounds, Vector3 worldPosition)
        {
            var closestX = Mathf.Clamp(worldPosition.x, bounds.min.x, bounds.max.x);
            var closestZ = Mathf.Clamp(worldPosition.z, bounds.min.z, bounds.max.z);
            var dx = worldPosition.x - closestX;
            var dz = worldPosition.z - closestZ;
            var distance = Mathf.Sqrt(dx * dx + dz * dz);
            if (distance <= chunkSize * 0.85f)
            {
                return 0;
            }

            return distance <= chunkSize * 1.85f ? 1 : 2;
        }

        private float ResolvePixelError(int lod)
        {
            var errors = settings != null ? settings.TerrainChunkLodPixelErrors : new Vector3(4f, 14f, 34f);
            return lod switch
            {
                0 => Mathf.Max(1f, errors.x),
                1 => Mathf.Max(1f, errors.y),
                _ => Mathf.Max(1f, errors.z)
            };
        }

        private Terrain TryGetTerrain(Vector2Int coord)
        {
            return loadedChunks.TryGetValue(coord, out var record) ? record.Terrain : null;
        }

        private Vector2Int WorldToChunkCoord(float worldX, float worldZ)
        {
            var min = WorldBounds.min;
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

        private static void ConfigureTerrainRenderCost(Terrain terrain, float pixelError)
        {
            if (terrain == null)
            {
                return;
            }

            terrain.drawInstanced = true;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.reflectionProbeUsage = ReflectionProbeUsage.Off;
            terrain.heightmapPixelError = Mathf.Max(1f, pixelError);
            terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 1200f);
            terrain.allowAutoConnect = false;
        }

        private static int SanitizeHeightmapResolution(int requestedResolution)
        {
            var clamped = Mathf.Clamp(requestedResolution, 33, 4097);
            return Mathf.ClosestPowerOfTwo(clamped - 1) + 1;
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

        private static void StitchEastWest(Terrain west, Terrain east)
        {
            if (!TryGetCompatibleTerrainData(west, east, out var westData, out var eastData, out var resolution))
            {
                return;
            }

            var westEdge = westData.GetHeights(resolution - 1, 0, 1, resolution);
            var eastEdge = eastData.GetHeights(0, 0, 1, resolution);
            for (var y = 0; y < resolution; y++)
            {
                var height = (westEdge[y, 0] + eastEdge[y, 0]) * 0.5f;
                westEdge[y, 0] = height;
                eastEdge[y, 0] = height;
            }

            westData.SetHeights(resolution - 1, 0, westEdge);
            eastData.SetHeights(0, 0, eastEdge);
        }

        private static void StitchSouthNorth(Terrain south, Terrain north)
        {
            if (!TryGetCompatibleTerrainData(south, north, out var southData, out var northData, out var resolution))
            {
                return;
            }

            var southEdge = southData.GetHeights(0, resolution - 1, resolution, 1);
            var northEdge = northData.GetHeights(0, 0, resolution, 1);
            for (var x = 0; x < resolution; x++)
            {
                var height = (southEdge[0, x] + northEdge[0, x]) * 0.5f;
                southEdge[0, x] = height;
                northEdge[0, x] = height;
            }

            southData.SetHeights(0, resolution - 1, southEdge);
            northData.SetHeights(0, 0, northEdge);
        }

        private static bool TryGetCompatibleTerrainData(
            Terrain first,
            Terrain second,
            out TerrainData firstData,
            out TerrainData secondData,
            out int resolution)
        {
            firstData = first != null ? first.terrainData : null;
            secondData = second != null ? second.terrainData : null;
            resolution = firstData != null ? firstData.heightmapResolution : 0;
            return firstData != null &&
                   secondData != null &&
                   resolution > 1 &&
                   secondData.heightmapResolution == resolution;
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
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif
            UnityEngine.Object.Destroy(target);
        }

        private readonly struct TerrainChunkRecord
        {
            public TerrainChunkRecord(Vector2Int coord, GameObject root, Terrain terrain)
            {
                Coord = coord;
                Root = root;
                Terrain = terrain;
                Bounds = terrain != null && terrain.terrainData != null
                    ? new Bounds(
                        terrain.transform.position + new Vector3(terrain.terrainData.size.x * 0.5f, terrain.terrainData.size.y * 0.5f, terrain.terrainData.size.z * 0.5f),
                        terrain.terrainData.size)
                    : default;
            }

            public Vector2Int Coord { get; }
            public GameObject Root { get; }
            public Terrain Terrain { get; }
            public Bounds Bounds { get; }
        }
    }
}
