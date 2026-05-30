using System;
using System.Collections.Generic;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Generator.Common;
using LegendsOfWarAndMagic.Generator.Common.Noise;
using LegendsOfWarAndMagic.Generator.Naming;
using GeneratedWorld = LegendsOfWarAndMagic.Game.World.Domain.World;

namespace LegendsOfWarAndMagic.Generator.World
{
    public sealed class WorldGenerationConfig
    {
        public const int MinPlayableLocationCount = 1;
        public const int MaxPlayableLocationCount = 15;

        private int playableLocationCount = MinPlayableLocationCount;

        public int Seed { get; set; }
        public int MinRegions { get; set; } = 8;
        public int MaxRegions { get; set; } = 20;
        public int PlayableLocationCount
        {
            get => Clamp(playableLocationCount, MinPlayableLocationCount, MaxPlayableLocationCount);
            set => playableLocationCount = Clamp(value, MinPlayableLocationCount, MaxPlayableLocationCount);
        }

        public WorldShapeType? ForcedShape { get; set; }
        public int MapSampleWidth { get; set; } = 128;
        public int MapSampleHeight { get; set; } = 96;
        public string GeneratorVersion { get; set; } = "top-down-worldgen-mvp-1";

        public int ResolveSeed()
        {
            return Seed == 0 ? Environment.TickCount : Seed;
        }

        public string BuildSnapshot()
        {
            return $"Seed={Seed};Regions={MinRegions}-{MaxRegions};Locations={PlayableLocationCount};Shape={ForcedShape};Samples={MapSampleWidth}x{MapSampleHeight};Version={GeneratorVersion}";
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public interface IWorldGenerator
    {
        GeneratedWorld Generate(WorldGenerationConfig config, Action<string> progress = null);
    }

    public interface IWorldGenerationStep
    {
        string Name { get; }
        void Execute(WorldGenerationContext context);
    }

    public sealed class WorldGenerationPipeline
    {
        private readonly IReadOnlyList<IWorldGenerationStep> steps;

        public WorldGenerationPipeline(IReadOnlyList<IWorldGenerationStep> steps)
        {
            this.steps = steps ?? Array.Empty<IWorldGenerationStep>();
        }

        public void Run(WorldGenerationContext context, Action<string> progress = null)
        {
            foreach (var step in steps)
            {
                progress?.Invoke(step.Name);
                step.Execute(context);
            }
        }
    }

    public sealed class TopDownWorldGenerator : IWorldGenerator
    {
        private readonly INoiseProvider noiseProvider;
        private readonly INameGenerator nameGenerator;

        public TopDownWorldGenerator()
            : this(new ValueNoiseProvider(), new FantasyNameGenerator())
        {
        }

        public TopDownWorldGenerator(INoiseProvider noiseProvider, INameGenerator nameGenerator)
        {
            this.noiseProvider = noiseProvider ?? throw new ArgumentNullException(nameof(noiseProvider));
            this.nameGenerator = nameGenerator ?? throw new ArgumentNullException(nameof(nameGenerator));
        }

        public GeneratedWorld Generate(WorldGenerationConfig config, Action<string> progress = null)
        {
            var safeConfig = config ?? new WorldGenerationConfig();
            var context = new WorldGenerationContext(safeConfig, noiseProvider, nameGenerator);
            BuildDefaultPipeline().Run(context, progress);
            return context.BuildWorld();
        }

        public static WorldGenerationPipeline BuildDefaultPipeline()
        {
            return new WorldGenerationPipeline(new IWorldGenerationStep[]
            {
                new Steps.SelectWorldShapeStep(),
                new Steps.GenerateLandWaterMaskStep(),
                new Steps.GenerateMacroElevationStep(),
                new Steps.GenerateClimateStep(),
                new Steps.GenerateBiomeLayoutStep(),
                new Steps.GenerateMountainRangesStep(),
                new Steps.GenerateRiversAndLakesStep(),
                new Steps.GenerateRegionsStep(),
                new Steps.SelectPlayableLocationsStep(),
                new Steps.GenerateLocationConnectionsStep(),
                new Steps.WorldNamingStep()
            });
        }
    }

    public sealed class WorldGenerationContext
    {
        public WorldGenerationContext(WorldGenerationConfig config, INoiseProvider noiseProvider, INameGenerator nameGenerator)
        {
            Config = config ?? new WorldGenerationConfig();
            Seed = Config.ResolveSeed();
            Random = new SeededRandom(Seed);
            Noise = noiseProvider ?? new ValueNoiseProvider();
            NameGenerator = nameGenerator ?? new FantasyNameGenerator();
            Width = Math.Max(32, Config.MapSampleWidth);
            Height = Math.Max(32, Config.MapSampleHeight);
            Land = new bool[Width, Height];
            Coastal = new bool[Width, Height];
            Elevation = new float[Width, Height];
            Temperature = new float[Width, Height];
            Humidity = new float[Width, Height];
            Biomes = new BiomeType[Width, Height];
        }

        public WorldGenerationConfig Config { get; }
        public int Seed { get; }
        public SeededRandom Random { get; }
        public INoiseProvider Noise { get; }
        public INameGenerator NameGenerator { get; }
        public int Width { get; }
        public int Height { get; }
        public WorldShapeType ShapeType { get; set; }
        public string WorldName { get; set; }
        public bool[,] Land { get; }
        public bool[,] Coastal { get; }
        public float[,] Elevation { get; }
        public float[,] Temperature { get; }
        public float[,] Humidity { get; }
        public BiomeType[,] Biomes { get; }
        public List<RegionDraft> Regions { get; } = new();
        public List<LocationDraft> Locations { get; } = new();
        public List<River> Rivers { get; set; } = new();
        public List<MountainRange> MountainRanges { get; set; } = new();
        public List<Lake> Lakes { get; set; } = new();
        public List<Coastline> Coastlines { get; set; } = new();

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public MapPoint ToMapPoint(int x, int y)
        {
            return new MapPoint(x / (float)Math.Max(1, Width - 1), y / (float)Math.Max(1, Height - 1));
        }

        public bool IsLandAtNormalized(float x, float y)
        {
            var ix = Clamp((int)Math.Round(x * (Width - 1)), 0, Width - 1);
            var iy = Clamp((int)Math.Round(y * (Height - 1)), 0, Height - 1);
            return Land[ix, iy];
        }

        public BiomeType BiomeAt(MapPoint point)
        {
            var ix = Clamp((int)Math.Round(point.X * (Width - 1)), 0, Width - 1);
            var iy = Clamp((int)Math.Round(point.Y * (Height - 1)), 0, Height - 1);
            return Biomes[ix, iy];
        }

        public float ElevationAt(MapPoint point)
        {
            var ix = Clamp((int)Math.Round(point.X * (Width - 1)), 0, Width - 1);
            var iy = Clamp((int)Math.Round(point.Y * (Height - 1)), 0, Height - 1);
            return Elevation[ix, iy];
        }

        public GeneratedWorld BuildWorld()
        {
            var locationsByRegion = Locations
                .GroupBy(location => location.RegionId.Value)
                .ToDictionary(group => group.Key, group => (IReadOnlyCollection<LocationId>)group.Select(location => location.Id).ToArray());

            var regions = new List<WorldRegion>();
            foreach (var region in Regions)
            {
                locationsByRegion.TryGetValue(region.Id.Value, out var regionLocationIds);
                regions.Add(new WorldRegion(
                    region.Id,
                    new RegionName(region.Name),
                    region.Type,
                    region.DominantBiome,
                    region.SecondaryBiomes,
                    new RegionBoundary(region.Boundary),
                    regionLocationIds ?? Array.Empty<LocationId>(),
                    region.LoreHook));
            }

            var locations = new List<WorldLocation>();
            foreach (var location in Locations)
            {
                locations.Add(new WorldLocation(
                    location.Id,
                    new LocationName(location.Name),
                    location.Type,
                    location.RegionId,
                    location.Position,
                    location.DominantBiome,
                    location.SecondaryBiomes,
                    location.TerrainSeed,
                    string.Empty,
                    new LocationMap(string.Empty),
                    location.Connections,
                    location.Gateways,
                    location.IsStartLocation));
            }

            var markers = locations.Select(location => new WorldMapPoint(location.Id, location.WorldMapPosition, location.Name.Value)).ToArray();
            return new GeneratedWorld(
                new WorldId(BuildDeterministicWorldId()),
                new WorldSeed(Seed),
                new WorldName(WorldName),
                new WorldShape(ShapeType, DescribeShape(ShapeType)),
                new WorldBounds(1f, 1f),
                regions,
                locations,
                Rivers,
                MountainRanges,
                Lakes,
                Coastlines,
                new WorldMap(string.Empty, markers),
                new WorldGenerationMetadata(Config.GeneratorVersion, DateTime.UtcNow, Config.BuildSnapshot()));
        }

        private string BuildDeterministicWorldId()
        {
            unchecked
            {
                var hash = Seed;
                hash = hash * 397 ^ (int)ShapeType;
                hash = hash * 397 ^ Config.PlayableLocationCount;
                hash = hash * 397 ^ Config.MinRegions;
                hash = hash * 397 ^ Config.MaxRegions;
                return $"world-{Math.Abs(hash):X8}";
            }
        }

        private static string DescribeShape(WorldShapeType type)
        {
            return type switch
            {
                WorldShapeType.HugeIsland => "A large island surrounded by sea.",
                WorldShapeType.IslandArchipelago => "A broken island chain with many sea routes.",
                WorldShapeType.ContinentPart => "The visible edge of a larger continent.",
                WorldShapeType.Peninsula => "A long peninsula reaching into open water.",
                WorldShapeType.PeninsulaAndIslands => "A coastal peninsula with nearby islands.",
                WorldShapeType.TwoPeninsulas => "Two land arms divided by a bay.",
                WorldShapeType.BrokenCoast => "A fractured coast of cliffs, bays, and inlets.",
                WorldShapeType.InlandSeaRegion => "A realm wrapped around a central inland sea.",
                WorldShapeType.MountainRingBasin => "A basin held inside high mountain arcs.",
                WorldShapeType.RiverDeltaRegion => "A low river delta flowing toward the sea.",
                _ => type.ToString()
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }

    public sealed class RegionDraft
    {
        public RegionId Id { get; set; }
        public string Name { get; set; }
        public RegionType Type { get; set; }
        public BiomeType DominantBiome { get; set; }
        public List<BiomeType> SecondaryBiomes { get; } = new();
        public List<MapPoint> Boundary { get; } = new();
        public string LoreHook { get; set; } = string.Empty;
        public MapPoint Center { get; set; }
    }

    public sealed class LocationDraft
    {
        public LocationId Id { get; set; }
        public string Name { get; set; }
        public LocationType Type { get; set; }
        public RegionId RegionId { get; set; }
        public MapPoint Position { get; set; }
        public BiomeType DominantBiome { get; set; }
        public List<BiomeType> SecondaryBiomes { get; } = new();
        public int TerrainSeed { get; set; }
        public bool IsStartLocation { get; set; }
        public List<LocationConnection> Connections { get; } = new();
        public List<LocationGateway> Gateways { get; } = new();
    }
}
