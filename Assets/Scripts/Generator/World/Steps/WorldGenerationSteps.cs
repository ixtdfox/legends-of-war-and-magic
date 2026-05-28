using System;
using System.Collections.Generic;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Generator.Common;
using LegendsOfWarAndMagic.Generator.Naming;
using static LegendsOfWarAndMagic.Generator.World.Steps.StepLocal;

namespace LegendsOfWarAndMagic.Generator.World.Steps
{
    public sealed class SelectWorldShapeStep : IWorldGenerationStep
    {
        public string Name => "Selecting world shape...";

        public void Execute(WorldGenerationContext context)
        {
            if (context.Config.ForcedShape.HasValue)
            {
                context.ShapeType = context.Config.ForcedShape.Value;
                return;
            }

            var shapes = (WorldShapeType[])Enum.GetValues(typeof(WorldShapeType));
            context.ShapeType = shapes[context.Random.NextInt(0, shapes.Length)];
        }
    }

    public sealed class GenerateLandWaterMaskStep : IWorldGenerationStep
    {
        public string Name => "Creating world form...";

        public void Execute(WorldGenerationContext context)
        {
            var landCells = 0;
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    var nx = x / (float)(context.Width - 1);
                    var ny = y / (float)(context.Height - 1);
                    var noise = context.Noise.Fractal(nx * 5.6f, ny * 5.6f, context.Seed + 11, 4, 0.55f, 2.05f);
                    var detail = context.Noise.Fractal(nx * 14.0f, ny * 14.0f, context.Seed + 29, 3, 0.52f, 2.1f);
                    var score = ShapeScore(context.ShapeType, nx, ny, noise, detail);
                    context.Land[x, y] = score > 0f;
                    if (context.Land[x, y])
                    {
                        landCells++;
                    }
                }
            }

            if (landCells < context.Width * context.Height * 0.18f)
            {
                ForceCentralLand(context);
            }

            MarkCoasts(context);
            context.Coastlines = BuildCoastlines(context);
        }

        private static float ShapeScore(WorldShapeType shape, float x, float y, float noise, float detail)
        {
            var cx = x - 0.5f;
            var cy = y - 0.5f;
            var radial = (float)Math.Sqrt(cx * cx + cy * cy) / 0.7071067f;
            var elliptical = (float)Math.Sqrt(cx * cx * 0.82f + cy * cy * 1.18f) / 0.7071067f;
            var coastNoise = (noise - 0.5f) * 0.32f + (detail - 0.5f) * 0.12f;

            switch (shape)
            {
                case WorldShapeType.HugeIsland:
                    return 0.58f - radial + coastNoise;
                case WorldShapeType.IslandArchipelago:
                {
                    var chain = 0.20f - Math.Abs(y - (0.56f + (noise - 0.5f) * 0.20f));
                    return Math.Max(chain, 0.34f - radial) + (noise - 0.42f) * 0.70f + (detail - 0.5f) * 0.22f;
                }
                case WorldShapeType.ContinentPart:
                    return 0.52f - x + coastNoise + (noise - 0.5f) * 0.24f;
                case WorldShapeType.Peninsula:
                {
                    var spine = 0.28f - Math.Abs(y - 0.52f - (noise - 0.5f) * 0.18f);
                    return Math.Max(0.44f - x, spine - x * 0.18f) + coastNoise * 0.7f;
                }
                case WorldShapeType.PeninsulaAndIslands:
                {
                    var peninsula = Math.Max(0.38f - x, 0.22f - Math.Abs(y - 0.48f));
                    var islands = noise > 0.62f && x > 0.48f ? 0.14f + (noise - 0.62f) * 1.1f - radial * 0.22f : -0.4f;
                    return Math.Max(peninsula, islands) + coastNoise * 0.9f;
                }
                case WorldShapeType.TwoPeninsulas:
                {
                    var upper = 0.26f - Math.Abs(y - 0.30f - (noise - 0.5f) * 0.08f);
                    var lower = 0.26f - Math.Abs(y - 0.72f + (noise - 0.5f) * 0.08f);
                    var arm = Math.Max(upper, lower) - Math.Abs(x - 0.42f) * 0.26f;
                    return Math.Max(0.24f - x, arm) + coastNoise * 0.62f;
                }
                case WorldShapeType.BrokenCoast:
                    return 0.46f - x + (noise - 0.48f) * 0.72f + (detail - 0.5f) * 0.22f;
                case WorldShapeType.InlandSeaRegion:
                {
                    var sea = 0.32f - radial;
                    var outerLand = 0.78f - elliptical + coastNoise;
                    return sea > 0f ? -sea * 1.3f + coastNoise * 0.2f : outerLand;
                }
                case WorldShapeType.MountainRingBasin:
                    return 0.72f - radial + coastNoise * 0.65f;
                case WorldShapeType.RiverDeltaRegion:
                    return 0.58f - x * 0.72f - Math.Abs(y - 0.50f) * 0.24f + coastNoise;
                default:
                    return 0.56f - radial + coastNoise;
            }
        }

        private static void ForceCentralLand(WorldGenerationContext context)
        {
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    var nx = x / (float)(context.Width - 1) - 0.5f;
                    var ny = y / (float)(context.Height - 1) - 0.5f;
                    var distance = (float)Math.Sqrt(nx * nx + ny * ny);
                    if (distance < 0.34f)
                    {
                        context.Land[x, y] = true;
                    }
                }
            }
        }

        private static void MarkCoasts(WorldGenerationContext context)
        {
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (!context.Land[x, y])
                    {
                        continue;
                    }

                    for (var oy = -1; oy <= 1; oy++)
                    {
                        for (var ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                            {
                                continue;
                            }

                            var px = x + ox;
                            var py = y + oy;
                            if (!context.IsInside(px, py) || !context.Land[px, py])
                            {
                                context.Coastal[x, y] = true;
                            }
                        }
                    }
                }
            }
        }

        private static List<Coastline> BuildCoastlines(WorldGenerationContext context)
        {
            var points = new List<MapPoint>();
            var stride = Math.Max(1, context.Width / 64);
            for (var y = 0; y < context.Height; y += stride)
            {
                for (var x = 0; x < context.Width; x += stride)
                {
                    if (context.Coastal[x, y])
                    {
                        points.Add(context.ToMapPoint(x, y));
                    }
                }
            }

            return new List<Coastline> { new Coastline("coast-main", "Known Coastline", points) };
        }
    }

    public sealed class GenerateMacroElevationStep : IWorldGenerationStep
    {
        public string Name => "Raising mountains and lowlands...";

        public void Execute(WorldGenerationContext context)
        {
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (!context.Land[x, y])
                    {
                        context.Elevation[x, y] = 0f;
                        continue;
                    }

                    var nx = x / (float)(context.Width - 1);
                    var ny = y / (float)(context.Height - 1);
                    var macro = context.Noise.Fractal(nx * 3.2f, ny * 3.2f, context.Seed + 101, 5, 0.55f, 2.0f);
                    var ridges = context.Noise.Fractal(nx * 8.4f, ny * 8.4f, context.Seed + 173, 4, 0.50f, 2.15f);
                    var shapeLift = ShapeElevation(context.ShapeType, nx, ny);
                    var coastalCut = context.Coastal[x, y] ? 0.18f : 0f;
                    context.Elevation[x, y] = Clamp01(macro * 0.54f + ridges * ridges * 0.32f + shapeLift - coastalCut);
                }
            }
        }

        private static float ShapeElevation(WorldShapeType shape, float x, float y)
        {
            var cx = x - 0.5f;
            var cy = y - 0.5f;
            var radial = (float)Math.Sqrt(cx * cx + cy * cy);
            return shape switch
            {
                WorldShapeType.MountainRingBasin => SmoothRange(0.28f, 0.47f, radial) * (1f - SmoothRange(0.48f, 0.70f, radial)) * 0.42f,
                WorldShapeType.RiverDeltaRegion => (1f - x) * 0.16f - Math.Max(0f, x - 0.66f) * 0.20f,
                WorldShapeType.ContinentPart => (1f - x) * 0.18f,
                WorldShapeType.Peninsula => (1f - x) * 0.10f,
                WorldShapeType.HugeIsland => Math.Max(0f, 0.40f - radial) * 0.38f,
                _ => 0.08f
            };
        }
    }

    public sealed class GenerateClimateStep : IWorldGenerationStep
    {
        public string Name => "Laying out climate...";

        public void Execute(WorldGenerationContext context)
        {
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    var nx = x / (float)(context.Width - 1);
                    var ny = y / (float)(context.Height - 1);
                    var warmthNoise = context.Noise.Fractal(nx * 2.2f, ny * 2.2f, context.Seed + 211, 3, 0.5f, 2f);
                    var humidityNoise = context.Noise.Fractal(nx * 3.8f, ny * 3.8f, context.Seed + 257, 4, 0.55f, 2f);
                    var latitudeWarmth = 1f - Math.Abs(ny - 0.38f) * 1.35f;
                    var coastalHumidity = context.Coastal[x, y] ? 0.25f : 0f;
                    var elevation = context.Elevation[x, y];

                    context.Temperature[x, y] = Clamp01(latitudeWarmth * 0.68f + warmthNoise * 0.24f - elevation * 0.34f + 0.10f);
                    context.Humidity[x, y] = Clamp01(humidityNoise * 0.58f + coastalHumidity + (1f - elevation) * 0.14f);
                }
            }
        }
    }

    public sealed class GenerateBiomeLayoutStep : IWorldGenerationStep
    {
        public string Name => "Placing biomes...";

        public void Execute(WorldGenerationContext context)
        {
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (!context.Land[x, y])
                    {
                        context.Biomes[x, y] = BiomeType.LakeDistrict;
                        continue;
                    }

                    var coastal = context.Coastal[x, y];
                    context.Biomes[x, y] = BiomeCatalog
                        .Select(context.Temperature[x, y], context.Humidity[x, y], context.Elevation[x, y], coastal, false)
                        .Type;
                }
            }
        }
    }

    public sealed class GenerateMountainRangesStep : IWorldGenerationStep
    {
        public string Name => "Marking mountain ranges...";

        public void Execute(WorldGenerationContext context)
        {
            var peaks = new List<(int X, int Y, float Height)>();
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (context.Land[x, y] && context.Elevation[x, y] > 0.62f)
                    {
                        peaks.Add((x, y, context.Elevation[x, y]));
                    }
                }
            }

            peaks.Sort((a, b) => b.Height.CompareTo(a.Height));
            var ranges = new List<MountainRange>();
            var count = Math.Min(context.Random.NextInt(2, 5), Math.Max(1, peaks.Count / 18));
            for (var i = 0; i < count && peaks.Count > 0; i++)
            {
                var start = peaks[Math.Min(i * Math.Max(1, peaks.Count / Math.Max(1, count)), peaks.Count - 1)];
                var points = new List<MapPoint>();
                var angle = context.Random.Range(0f, (float)Math.PI * 2f);
                var length = context.Random.NextInt(9, 18);
                for (var step = -length; step <= length; step += 2)
                {
                    var px = Clamp((int)Math.Round(start.X + Math.Cos(angle) * step), 0, context.Width - 1);
                    var py = Clamp((int)Math.Round(start.Y + Math.Sin(angle) * step), 0, context.Height - 1);
                    if (context.Land[px, py])
                    {
                        points.Add(context.ToMapPoint(px, py));
                    }
                }

                if (points.Count >= 2)
                {
                    ranges.Add(new MountainRange($"mountains-{i}", $"Mountain Range {i + 1}", points, start.Height));
                }
            }

            context.MountainRanges = ranges;
        }
    }

    public sealed class GenerateRiversAndLakesStep : IWorldGenerationStep
    {
        public string Name => "Drawing rivers and lakes...";

        public void Execute(WorldGenerationContext context)
        {
            var rivers = new List<River>();
            var highCells = new List<(int X, int Y, float Height)>();
            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (context.Land[x, y] && context.Elevation[x, y] > 0.54f && !context.Coastal[x, y])
                    {
                        highCells.Add((x, y, context.Elevation[x, y]));
                    }
                }
            }

            highCells.Sort((a, b) => b.Height.CompareTo(a.Height));
            var riverCount = Math.Min(context.Random.NextInt(3, 7), Math.Max(1, highCells.Count / 28));
            for (var i = 0; i < riverCount && highCells.Count > 0; i++)
            {
                var start = highCells[Math.Min(i * Math.Max(1, highCells.Count / riverCount), highCells.Count - 1)];
                var end = FindNearestCoastOrEdge(context, start.X, start.Y);
                var points = BuildCurvedPath(context, start.X, start.Y, end.X, end.Y, context.Seed + i * 97);
                if (points.Count >= 3)
                {
                    rivers.Add(new River($"river-{i}", $"River {i + 1}", points, context.Random.Range(0.006f, 0.014f)));
                }
            }

            context.Rivers = rivers;
            context.Lakes = BuildLakes(context);
        }

        private static (int X, int Y) FindNearestCoastOrEdge(WorldGenerationContext context, int x, int y)
        {
            var best = (X: x, Y: y);
            var bestDistance = float.MaxValue;
            for (var py = 0; py < context.Height; py += 2)
            {
                for (var px = 0; px < context.Width; px += 2)
                {
                    if (!context.Coastal[px, py] && px != 0 && py != 0 && px != context.Width - 1 && py != context.Height - 1)
                    {
                        continue;
                    }

                    var dx = px - x;
                    var dy = py - y;
                    var distance = dx * dx + dy * dy;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = (px, py);
                    }
                }
            }

            return best;
        }

        private static List<MapPoint> BuildCurvedPath(WorldGenerationContext context, int sx, int sy, int ex, int ey, int seed)
        {
            var points = new List<MapPoint>();
            var steps = Math.Max(8, (int)Math.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy)));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var wiggle = (context.Noise.Fractal(t * 4.2f, seed * 0.001f, seed, 3, 0.5f, 2f) - 0.5f) * 8f;
                var x = (int)Math.Round(Lerp(sx, ex, t) + wiggle * (1f - Math.Abs(t - 0.5f) * 2f));
                var y = (int)Math.Round(Lerp(sy, ey, t) - wiggle * 0.45f * (1f - Math.Abs(t - 0.5f) * 2f));
                x = Clamp(x, 0, context.Width - 1);
                y = Clamp(y, 0, context.Height - 1);
                if (context.Land[x, y] || context.Coastal[x, y])
                {
                    points.Add(context.ToMapPoint(x, y));
                    if (context.Land[x, y] && context.Elevation[x, y] < 0.54f)
                    {
                        context.Biomes[x, y] = BiomeType.Riverlands;
                    }
                }
            }

            return points;
        }

        private static List<Lake> BuildLakes(WorldGenerationContext context)
        {
            var lakes = new List<Lake>();
            var count = context.Random.NextInt(2, 5);
            for (var i = 0; i < count; i++)
            {
                var point = FindRandomLowland(context);
                if (point.X < 0)
                {
                    break;
                }

                var radius = context.Random.Range(0.018f, 0.044f);
                var shoreline = new List<MapPoint>();
                var samples = 18;
                for (var s = 0; s < samples; s++)
                {
                    var angle = s / (float)samples * (float)Math.PI * 2f;
                    shoreline.Add(new MapPoint(
                        point.X / (float)(context.Width - 1) + (float)Math.Cos(angle) * radius,
                        point.Y / (float)(context.Height - 1) + (float)Math.Sin(angle) * radius));
                }

                lakes.Add(new Lake($"lake-{i}", $"Lake {i + 1}", shoreline, radius));
            }

            return lakes;
        }

        private static (int X, int Y) FindRandomLowland(WorldGenerationContext context)
        {
            for (var attempt = 0; attempt < 300; attempt++)
            {
                var x = context.Random.NextInt(4, context.Width - 4);
                var y = context.Random.NextInt(4, context.Height - 4);
                if (context.Land[x, y] && !context.Coastal[x, y] && context.Elevation[x, y] < 0.42f)
                {
                    return (x, y);
                }
            }

            return (-1, -1);
        }
    }

    public sealed class GenerateRegionsStep : IWorldGenerationStep
    {
        public string Name => "Dividing named regions...";

        public void Execute(WorldGenerationContext context)
        {
            context.Regions.Clear();
            var requested = context.Random.NextInt(context.Config.MinRegions, context.Config.MaxRegions + 1);
            var centers = PickSpreadCenters(context, requested);
            if (centers.Count == 0)
            {
                centers.Add(new MapPoint(0.5f, 0.5f));
            }

            var cellBuckets = new List<List<(int X, int Y)>>();
            for (var i = 0; i < centers.Count; i++)
            {
                cellBuckets.Add(new List<(int X, int Y)>());
            }

            for (var y = 0; y < context.Height; y++)
            {
                for (var x = 0; x < context.Width; x++)
                {
                    if (!context.Land[x, y])
                    {
                        continue;
                    }

                    var point = context.ToMapPoint(x, y);
                    var nearest = 0;
                    var best = float.MaxValue;
                    for (var i = 0; i < centers.Count; i++)
                    {
                        var distance = point.DistanceTo(centers[i]);
                        if (distance < best)
                        {
                            best = distance;
                            nearest = i;
                        }
                    }

                    cellBuckets[nearest].Add((x, y));
                }
            }

            for (var i = 0; i < centers.Count; i++)
            {
                if (cellBuckets[i].Count == 0)
                {
                    continue;
                }

                var biomeCounts = new Dictionary<BiomeType, int>();
                var minX = context.Width;
                var minY = context.Height;
                var maxX = 0;
                var maxY = 0;
                foreach (var cell in cellBuckets[i])
                {
                    minX = Math.Min(minX, cell.X);
                    minY = Math.Min(minY, cell.Y);
                    maxX = Math.Max(maxX, cell.X);
                    maxY = Math.Max(maxY, cell.Y);
                    var biome = context.Biomes[cell.X, cell.Y];
                    biomeCounts.TryGetValue(biome, out var count);
                    biomeCounts[biome] = count + 1;
                }

                var dominant = biomeCounts.OrderByDescending(pair => pair.Value).First().Key;
                var secondary = biomeCounts.OrderByDescending(pair => pair.Value)
                    .Skip(1)
                    .Take(2)
                    .Select(pair => pair.Key)
                    .ToArray();

                var boundary = new List<MapPoint>
                {
                    context.ToMapPoint(minX, minY),
                    context.ToMapPoint(maxX, minY),
                    context.ToMapPoint(maxX, maxY),
                    context.ToMapPoint(minX, maxY)
                };

                var region = new RegionDraft
                {
                    Id = new RegionId($"region-{i:00}-{Math.Abs(SeededRandom.Hash(context.Seed, i)):X8}"),
                    Name = $"Region {i + 1}",
                    Type = RegionTypeFromBiome(dominant, IsMostlyCoastal(context, cellBuckets[i])),
                    DominantBiome = dominant,
                    Center = centers[i],
                    LoreHook = "Lore hook placeholder"
                };
                region.SecondaryBiomes.AddRange(secondary);
                region.Boundary.AddRange(boundary);
                context.Regions.Add(region);
            }
        }

        private static List<MapPoint> PickSpreadCenters(WorldGenerationContext context, int requested)
        {
            var centers = new List<MapPoint>();
            for (var attempt = 0; attempt < 2000 && centers.Count < requested; attempt++)
            {
                var x = context.Random.NextInt(0, context.Width);
                var y = context.Random.NextInt(0, context.Height);
                if (!context.Land[x, y])
                {
                    continue;
                }

                var point = context.ToMapPoint(x, y);
                var minDistance = centers.Count < 4 ? 0.13f : 0.08f;
                if (centers.All(existing => existing.DistanceTo(point) >= minDistance))
                {
                    centers.Add(point);
                }
            }

            return centers;
        }

        private static bool IsMostlyCoastal(WorldGenerationContext context, IReadOnlyList<(int X, int Y)> cells)
        {
            var coastal = 0;
            foreach (var cell in cells)
            {
                if (context.Coastal[cell.X, cell.Y])
                {
                    coastal++;
                }
            }

            return coastal > cells.Count * 0.28f;
        }

        private static RegionType RegionTypeFromBiome(BiomeType biome, bool coastal)
        {
            if (coastal)
            {
                return biome == BiomeType.TropicalCoast || biome == BiomeType.RockyCoast ? RegionType.Coast : RegionType.Island;
            }

            return biome switch
            {
                BiomeType.TemperateForest or BiomeType.DarkForest => RegionType.Forest,
                BiomeType.Desert or BiomeType.AshDesert => RegionType.Desert,
                BiomeType.Mountains or BiomeType.SnowPeaks => RegionType.Mountain,
                BiomeType.Swamp => RegionType.Swamp,
                BiomeType.Highlands or BiomeType.Tundra => RegionType.Highlands,
                BiomeType.Riverlands or BiomeType.LakeDistrict => RegionType.Riverlands,
                BiomeType.Wasteland => RegionType.Wasteland,
                _ => RegionType.Mixed
            };
        }
    }

    public sealed class SelectPlayableLocationsStep : IWorldGenerationStep
    {
        public string Name => "Selecting playable locations...";

        public void Execute(WorldGenerationContext context)
        {
            context.Locations.Clear();
            var candidates = new List<(RegionDraft Region, MapPoint Position, BiomeType Biome)>();
            foreach (var region in context.Regions)
            {
                var pos = FindNearestLand(context, region.Center);
                candidates.Add((region, pos, context.BiomeAt(pos)));
                if (context.Random.Chance(0.45f))
                {
                    var nearby = new MapPoint(
                        region.Center.X + context.Random.Range(-0.08f, 0.08f),
                        region.Center.Y + context.Random.Range(-0.08f, 0.08f));
                    nearby = FindNearestLand(context, nearby);
                    candidates.Add((region, nearby, context.BiomeAt(nearby)));
                }
            }

            context.Random.Shuffle(candidates);
            var selected = new List<(RegionDraft Region, MapPoint Position, BiomeType Biome)>();
            foreach (var candidate in candidates.OrderByDescending(candidate => CandidateScore(context, candidate.Position, selected)))
            {
                if (selected.Count >= context.Config.PlayableLocationCount)
                {
                    break;
                }

                if (selected.Count < context.Regions.Count || selected.All(existing => existing.Position.DistanceTo(candidate.Position) > 0.065f))
                {
                    selected.Add(candidate);
                }
            }

            while (selected.Count < context.Config.PlayableLocationCount && candidates.Count > 0)
            {
                selected.Add(candidates[context.Random.NextInt(0, candidates.Count)]);
            }

            var startIndex = SelectStartLocationIndex(selected);
            for (var i = 0; i < selected.Count; i++)
            {
                var candidate = selected[i];
                var secondary = candidate.Region.SecondaryBiomes
                    .Where(biome => biome != candidate.Biome)
                    .Take(2)
                    .ToArray();
                context.Locations.Add(new LocationDraft
                {
                    Id = new LocationId($"loc-{i:00}-{Math.Abs(SeededRandom.Hash(context.Seed, i + 7001)):X8}"),
                    Name = $"Location {i + 1}",
                    Type = ResolveLocationType(context, candidate.Position, candidate.Biome),
                    RegionId = candidate.Region.Id,
                    Position = candidate.Position,
                    DominantBiome = candidate.Biome,
                    TerrainSeed = SeededRandom.Hash(context.Seed, i + 30017),
                    IsStartLocation = i == startIndex
                });
                context.Locations[i].SecondaryBiomes.AddRange(secondary);
            }
        }

        private static float CandidateScore(WorldGenerationContext context, MapPoint position, IReadOnlyList<(RegionDraft Region, MapPoint Position, BiomeType Biome)> selected)
        {
            if (selected.Count == 0)
            {
                var centerDistance = position.DistanceTo(new MapPoint(0.5f, 0.5f));
                return 1f - centerDistance;
            }

            var minDistance = selected.Min(existing => existing.Position.DistanceTo(position));
            var biomeBonus = selected.Any(existing => existing.Biome == context.BiomeAt(position)) ? 0f : 0.12f;
            return minDistance + biomeBonus;
        }

        private static int SelectStartLocationIndex(IReadOnlyList<(RegionDraft Region, MapPoint Position, BiomeType Biome)> selected)
        {
            var bestIndex = 0;
            var bestScore = float.MinValue;
            for (var i = 0; i < selected.Count; i++)
            {
                var biome = selected[i].Biome;
                var biomeScore = biome is BiomeType.Grassland or BiomeType.TemperateForest or BiomeType.Riverlands ? 0.4f : 0f;
                var centerScore = 1f - selected[i].Position.DistanceTo(new MapPoint(0.5f, 0.5f));
                var score = biomeScore + centerScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static MapPoint FindNearestLand(WorldGenerationContext context, MapPoint target)
        {
            var startX = Clamp((int)Math.Round(target.X * (context.Width - 1)), 0, context.Width - 1);
            var startY = Clamp((int)Math.Round(target.Y * (context.Height - 1)), 0, context.Height - 1);
            if (context.Land[startX, startY])
            {
                return context.ToMapPoint(startX, startY);
            }

            for (var radius = 1; radius < Math.Max(context.Width, context.Height); radius++)
            {
                for (var y = startY - radius; y <= startY + radius; y++)
                {
                    for (var x = startX - radius; x <= startX + radius; x++)
                    {
                        if (context.IsInside(x, y) && context.Land[x, y])
                        {
                            return context.ToMapPoint(x, y);
                        }
                    }
                }
            }

            return new MapPoint(0.5f, 0.5f);
        }

        private static LocationType ResolveLocationType(WorldGenerationContext context, MapPoint position, BiomeType biome)
        {
            var ix = Clamp((int)Math.Round(position.X * (context.Width - 1)), 0, context.Width - 1);
            var iy = Clamp((int)Math.Round(position.Y * (context.Height - 1)), 0, context.Height - 1);
            if (context.Coastal[ix, iy])
            {
                return biome == BiomeType.TropicalCoast || biome == BiomeType.RockyCoast ? LocationType.Coast : LocationType.Island;
            }

            return biome switch
            {
                BiomeType.TemperateForest or BiomeType.DarkForest => LocationType.Forest,
                BiomeType.Desert or BiomeType.AshDesert => LocationType.Desert,
                BiomeType.Mountains or BiomeType.SnowPeaks or BiomeType.Highlands => LocationType.MountainPass,
                BiomeType.Swamp => LocationType.Swamp,
                BiomeType.Riverlands or BiomeType.LakeDistrict => LocationType.RiverCrossing,
                _ => LocationType.Wilderness
            };
        }
    }

    public sealed class GenerateLocationConnectionsStep : IWorldGenerationStep
    {
        public string Name => "Connecting locations...";

        public void Execute(WorldGenerationContext context)
        {
            foreach (var location in context.Locations)
            {
                location.Connections.Clear();
                location.Gateways.Clear();
            }

            var edges = BuildMinimumSpanningEdges(context.Locations);
            AddDirectionalNeighborEdges(context.Locations, edges);
            foreach (var edge in edges)
            {
                AddConnectionPair(edge.From, edge.To);
            }
        }

        private static List<(LocationDraft From, LocationDraft To)> BuildMinimumSpanningEdges(IReadOnlyList<LocationDraft> locations)
        {
            var edges = new List<(LocationDraft From, LocationDraft To)>();
            if (locations.Count <= 1)
            {
                return edges;
            }

            var connected = new HashSet<LocationDraft> { locations[0] };
            while (connected.Count < locations.Count)
            {
                var bestDistance = float.MaxValue;
                LocationDraft bestFrom = null;
                LocationDraft bestTo = null;
                foreach (var from in connected)
                {
                    foreach (var to in locations)
                    {
                        if (connected.Contains(to))
                        {
                            continue;
                        }

                        var distance = from.Position.DistanceTo(to.Position);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestFrom = from;
                            bestTo = to;
                        }
                    }
                }

                if (bestFrom == null || bestTo == null)
                {
                    break;
                }

                edges.Add((bestFrom, bestTo));
                connected.Add(bestTo);
            }

            return edges;
        }

        private static void AddDirectionalNeighborEdges(IReadOnlyList<LocationDraft> locations, List<(LocationDraft From, LocationDraft To)> edges)
        {
            foreach (var location in locations)
            {
                var nearest = locations
                    .Where(other => other != location)
                    .OrderBy(other => location.Position.DistanceTo(other.Position))
                    .Take(3);
                foreach (var other in nearest)
                {
                    if (location.Connections.Count >= 4)
                    {
                        break;
                    }

                    if (HasEdge(edges, location, other))
                    {
                        continue;
                    }

                    if (location.Position.DistanceTo(other.Position) < 0.28f)
                    {
                        edges.Add((location, other));
                    }
                }
            }
        }

        private static bool HasEdge(IEnumerable<(LocationDraft From, LocationDraft To)> edges, LocationDraft a, LocationDraft b)
        {
            foreach (var edge in edges)
            {
                if ((edge.From == a && edge.To == b) || (edge.From == b && edge.To == a))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddConnectionPair(LocationDraft a, LocationDraft b)
        {
            var direction = ResolveDirection(a.Position, b.Position);
            var opposite = Opposite(direction);
            var type = ResolveConnectionType(a, b);
            a.Connections.Add(new LocationConnection(a.Id, b.Id, direction, type, type.ToString()));
            b.Connections.Add(new LocationConnection(b.Id, a.Id, opposite, type, type.ToString()));
            a.Gateways.Add(new LocationGateway(a.Id, b.Id, direction, opposite, GatewayRect(direction)));
            b.Gateways.Add(new LocationGateway(b.Id, a.Id, opposite, direction, GatewayRect(opposite)));
        }

        private static WorldDirection ResolveDirection(MapPoint from, MapPoint to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return dx >= 0f ? WorldDirection.East : WorldDirection.West;
            }

            return dy >= 0f ? WorldDirection.North : WorldDirection.South;
        }

        private static WorldDirection Opposite(WorldDirection direction)
        {
            return direction switch
            {
                WorldDirection.North => WorldDirection.South,
                WorldDirection.South => WorldDirection.North,
                WorldDirection.East => WorldDirection.West,
                WorldDirection.West => WorldDirection.East,
                WorldDirection.NorthEast => WorldDirection.SouthWest,
                WorldDirection.NorthWest => WorldDirection.SouthEast,
                WorldDirection.SouthEast => WorldDirection.NorthWest,
                WorldDirection.SouthWest => WorldDirection.NorthEast,
                _ => WorldDirection.South
            };
        }

        private static MapRect GatewayRect(WorldDirection direction)
        {
            return direction switch
            {
                WorldDirection.North => new MapRect(0.42f, 0.94f, 0.16f, 0.06f),
                WorldDirection.South => new MapRect(0.42f, 0.00f, 0.16f, 0.06f),
                WorldDirection.East => new MapRect(0.94f, 0.42f, 0.06f, 0.16f),
                WorldDirection.West => new MapRect(0.00f, 0.42f, 0.06f, 0.16f),
                _ => new MapRect(0.42f, 0.42f, 0.16f, 0.16f)
            };
        }

        private static LocationConnectionType ResolveConnectionType(LocationDraft a, LocationDraft b)
        {
            if (a.Type == LocationType.MountainPass || b.Type == LocationType.MountainPass)
            {
                return LocationConnectionType.MountainPass;
            }

            if (a.Type == LocationType.RiverCrossing || b.Type == LocationType.RiverCrossing)
            {
                return LocationConnectionType.RiverCrossing;
            }

            if (a.Type == LocationType.Forest || b.Type == LocationType.Forest)
            {
                return LocationConnectionType.ForestTrail;
            }

            if (a.Type == LocationType.Island || b.Type == LocationType.Island || a.Type == LocationType.Coast || b.Type == LocationType.Coast)
            {
                return LocationConnectionType.BoatRoute;
            }

            return LocationConnectionType.LandPath;
        }
    }

    public sealed class WorldNamingStep : IWorldGenerationStep
    {
        public string Name => "Naming regions and locations...";

        public void Execute(WorldGenerationContext context)
        {
            context.WorldName = context.NameGenerator.Next(NameKind.WorldName, context.Random.Fork(5001));

            foreach (var region in context.Regions)
            {
                region.Name = context.NameGenerator.Next(NameKind.RegionName, context.Random.Fork(StableHash(region.Id.Value)));
            }

            foreach (var location in context.Locations)
            {
                location.Name = context.NameGenerator.Next(NameKind.LocationName, context.Random.Fork(StableHash(location.Id.Value)));
            }

            RenameFeatures(context);
            RebuildConnectionDisplayNames(context);
        }

        private static void RenameFeatures(WorldGenerationContext context)
        {
            context.Rivers = context.Rivers
                .Select((river, index) => new River(river.Id, context.NameGenerator.Next(NameKind.RiverName, context.Random.Fork(6000 + index)), river.Points, river.Width))
                .ToList();
            context.MountainRanges = context.MountainRanges
                .Select((range, index) => new MountainRange(range.Id, context.NameGenerator.Next(NameKind.MountainRangeName, context.Random.Fork(7000 + index)), range.Points, range.Intensity))
                .ToList();
            context.Lakes = context.Lakes
                .Select((lake, index) => new Lake(lake.Id, context.NameGenerator.Next(NameKind.LakeName, context.Random.Fork(8000 + index)), lake.Points, lake.Radius))
                .ToList();
        }

        private static void RebuildConnectionDisplayNames(WorldGenerationContext context)
        {
            var namesById = context.Locations.ToDictionary(location => location.Id.Value, location => location.Name);
            foreach (var location in context.Locations)
            {
                var rebuilt = new List<LocationConnection>();
                foreach (var connection in location.Connections)
                {
                    namesById.TryGetValue(connection.To.Value, out var targetName);
                    rebuilt.Add(new LocationConnection(
                        connection.From,
                        connection.To,
                        connection.Direction,
                        connection.Type,
                        $"{ConnectionLabel(connection.Type)} to {targetName ?? connection.To.Value}"));
                }

                location.Connections.Clear();
                location.Connections.AddRange(rebuilt);
            }
        }

        private static string ConnectionLabel(LocationConnectionType type)
        {
            return type switch
            {
                LocationConnectionType.MountainPass => "Pass",
                LocationConnectionType.ForestTrail => "Trail",
                LocationConnectionType.RiverCrossing => "Crossing",
                LocationConnectionType.BoatRoute => "Boat route",
                LocationConnectionType.CavePassage => "Cave passage",
                _ => "Road"
            };
        }
    }

    internal static class StepLocal
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        public static float SmoothRange(float min, float max, float value)
        {
            var t = max <= min ? 0f : Clamp01((value - min) / (max - min));
            return t * t * (3f - 2f * t);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static int StableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                if (value != null)
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return hash == 0 ? 1 : hash;
            }
        }
    }
}
