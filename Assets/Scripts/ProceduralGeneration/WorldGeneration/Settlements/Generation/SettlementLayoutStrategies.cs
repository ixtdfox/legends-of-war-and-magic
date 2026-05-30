using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public sealed class CampLayoutStrategy : ISettlementLayoutStrategy
    {
        public string Name => nameof(CampLayoutStrategy);
        public bool Supports(SettlementTier tier) => tier == SettlementTier.Camp;

        public SettlementLayout Generate(SettlementLayoutContext context)
        {
            var random = new System.Random(DeterministicRandom.Hash(context.Seed, 101));
            var solver = new BuildingPlacementSolver(8f);
            var buildings = BuildBuildingPlan(context, random);
            var roads = new List<GeneratedSettlementRoadSegment>();
            var points = new List<Vector2> { context.Site.Position };
            var district = new GeneratedSettlementDistrict($"{context.Site.Tier}_core", SettlementDistrictType.Core, "Camp Circle", context.Site.Position, context.Site.Radius);
            var districts = new[] { district };

            for (var i = 0; i < buildings.Count; i++)
            {
                var angle = i / (float)Mathf.Max(1, buildings.Count) * Mathf.PI * 2f + random.Range(-0.32f, 0.32f);
                var distance = random.Range(context.Site.Radius * 0.22f, context.Site.Radius * 0.62f);
                var position = context.Site.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var rotation = angle * Mathf.Rad2Deg + 180f + random.Range(-18f, 18f);
                points.Add(position);
                solver.TryPlace(
                    buildings[i],
                    district.Id,
                    position,
                    rotation,
                    context.PlotPadding,
                    context.SlopeSampler,
                    context.MaxBuildingSlope,
                    context.ForbiddenSampler,
                    out _);
            }

            return new SettlementLayout(districts, solver.Buildings, roads, points, solver.Rejections);
        }

        internal static List<GeneratedSettlementBuildingDefinition> BuildBuildingPlan(SettlementLayoutContext context, System.Random random)
        {
            var result = new List<GeneratedSettlementBuildingDefinition>();
            AddRequired(context, result);

            var optional = ResolveAvailableDefinitions(context);
            while (result.Count < context.TargetBuildingCount && optional.Count > 0)
            {
                var weighted = PickWeighted(optional, context.Site.Tier, random);
                if (weighted == null)
                {
                    break;
                }

                result.Add(weighted);
            }

            return result;
        }

        private static void AddRequired(SettlementLayoutContext context, List<GeneratedSettlementBuildingDefinition> result)
        {
            for (var i = 0; i < context.RequiredBuildingTypes.Count; i++)
            {
                var definition = FindDefinition(context, context.RequiredBuildingTypes[i]);
                if (definition != null)
                {
                    result.Add(definition);
                }
            }
        }

        private static GeneratedSettlementBuildingDefinition FindDefinition(SettlementLayoutContext context, BuildingType type)
        {
            GeneratedSettlementBuildingDefinition best = null;
            for (var i = 0; i < context.BuildingDefinitions.Count; i++)
            {
                var definition = context.BuildingDefinitions[i];
                if (definition.Type != type || definition.MinSettlementTier > context.Site.Tier)
                {
                    continue;
                }

                if (best == null || definition.Level > best.Level)
                {
                    best = definition;
                }
            }

            return best;
        }

        private static List<GeneratedSettlementBuildingDefinition> ResolveAvailableDefinitions(SettlementLayoutContext context)
        {
            var result = new List<GeneratedSettlementBuildingDefinition>();
            for (var i = 0; i < context.BuildingDefinitions.Count; i++)
            {
                var definition = context.BuildingDefinitions[i];
                if (definition.MinSettlementTier > context.Site.Tier)
                {
                    continue;
                }

                if (context.OptionalBuildingTypes.Count > 0 && !Contains(context.OptionalBuildingTypes, definition.Type))
                {
                    continue;
                }

                result.Add(definition);
            }

            return result;
        }

        private static bool Contains(IReadOnlyList<BuildingType> values, BuildingType value)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static GeneratedSettlementBuildingDefinition PickWeighted(IReadOnlyList<GeneratedSettlementBuildingDefinition> definitions, SettlementTier tier, System.Random random)
        {
            var total = 0f;
            for (var i = 0; i < definitions.Count; i++)
            {
                total += ResolveWeight(definitions[i], tier);
            }

            if (total <= 0f)
            {
                return definitions.Count > 0 ? definitions[0] : null;
            }

            var pick = random.Range(0f, total);
            for (var i = 0; i < definitions.Count; i++)
            {
                pick -= ResolveWeight(definitions[i], tier);
                if (pick <= 0f)
                {
                    return definitions[i];
                }
            }

            return definitions[definitions.Count - 1];
        }

        private static float ResolveWeight(GeneratedSettlementBuildingDefinition definition, SettlementTier tier)
        {
            var tierGap = (int)tier - (int)definition.MinSettlementTier;
            var baseWeight = definition.Type switch
            {
                BuildingType.House => 4.4f,
                BuildingType.Tent => 3.8f,
                BuildingType.Farm => 2.4f,
                BuildingType.Storage => 2.1f,
                BuildingType.Well => 1.2f,
                BuildingType.Wall => tier >= SettlementTier.Town ? 1.6f : 0.7f,
                BuildingType.Gate => tier >= SettlementTier.Town ? 1.2f : 0.1f,
                BuildingType.TownHall => 0.75f,
                BuildingType.Temple => 0.45f,
                BuildingType.Barracks => 0.7f,
                _ => 1f
            };

            return Mathf.Max(0.05f, baseWeight + tierGap * 0.25f - (definition.Level - 1) * 0.35f);
        }
    }

    public sealed class HamletLayoutStrategy : OrganicRoadLayoutStrategy
    {
        public override string Name => nameof(HamletLayoutStrategy);
        public override bool Supports(SettlementTier tier) => tier == SettlementTier.Hamlet;
        protected override int RoadArmCount => 2;
        protected override float PlotRoadOffsetFactor => 0.22f;
    }

    public sealed class VillageOrganicLayoutStrategy : OrganicRoadLayoutStrategy
    {
        public override string Name => nameof(VillageOrganicLayoutStrategy);
        public override bool Supports(SettlementTier tier) => tier == SettlementTier.Village;
        protected override int RoadArmCount => 3;
        protected override float PlotRoadOffsetFactor => 0.28f;
    }

    public sealed class TownDistrictLayoutStrategy : OrganicRoadLayoutStrategy
    {
        public override string Name => nameof(TownDistrictLayoutStrategy);
        public override bool Supports(SettlementTier tier) => tier == SettlementTier.Town;
        protected override int RoadArmCount => 5;
        protected override float PlotRoadOffsetFactor => 0.32f;
        protected override bool UseDistricts => true;
    }

    public sealed class CityWalledLayoutStrategy : OrganicRoadLayoutStrategy
    {
        public override string Name => nameof(CityWalledLayoutStrategy);
        public override bool Supports(SettlementTier tier) => tier == SettlementTier.City || tier == SettlementTier.Capital;
        protected override int RoadArmCount => 6;
        protected override float PlotRoadOffsetFactor => 0.36f;
        protected override bool UseDistricts => true;
        protected override bool UseWalls => true;
    }

    public abstract class OrganicRoadLayoutStrategy : ISettlementLayoutStrategy
    {
        public abstract string Name { get; }
        protected virtual int RoadArmCount => 3;
        protected virtual float PlotRoadOffsetFactor => 0.28f;
        protected virtual bool UseDistricts => false;
        protected virtual bool UseWalls => false;

        public abstract bool Supports(SettlementTier tier);

        public SettlementLayout Generate(SettlementLayoutContext context)
        {
            var random = new System.Random(DeterministicRandom.Hash(context.Seed, (int)context.Site.Tier * 4099 + 211));
            var districts = BuildDistricts(context, random);
            var roads = BuildRoads(context, random);
            var debugPoints = BuildDebugPoints(roads, districts);
            var solver = new BuildingPlacementSolver(Mathf.Max(8f, context.PlotPadding * 3f));
            var buildingPlan = CampLayoutStrategy.BuildBuildingPlan(context, random);

            PlaceBuildingsAlongRoads(context, random, roads, districts, buildingPlan, solver);
            if (UseWalls)
            {
                PlaceWallsAndGates(context, random, districts[0], solver);
            }

            return new SettlementLayout(districts, solver.Buildings, roads, debugPoints, solver.Rejections);
        }

        private IReadOnlyList<GeneratedSettlementDistrict> BuildDistricts(SettlementLayoutContext context, System.Random random)
        {
            var districts = new List<GeneratedSettlementDistrict>
            {
                new("core", SettlementDistrictType.Core, "Core", context.Site.Position, context.Site.Radius * 0.28f)
            };

            if (!UseDistricts)
            {
                return districts;
            }

            var types = new[]
            {
                SettlementDistrictType.Residential,
                SettlementDistrictType.Market,
                SettlementDistrictType.Craft,
                SettlementDistrictType.Military,
                SettlementDistrictType.Sacred,
                SettlementDistrictType.Rural
            };
            var count = Mathf.Clamp(RoadArmCount - 1, 2, types.Length);
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f + random.Range(-0.25f, 0.25f);
                var center = context.Site.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * context.Site.Radius * random.Range(0.32f, 0.56f);
                districts.Add(new GeneratedSettlementDistrict(
                    $"district_{types[i]}",
                    types[i],
                    types[i].ToString(),
                    center,
                    context.Site.Radius * random.Range(0.18f, 0.28f)));
            }

            return districts;
        }

        private IReadOnlyList<GeneratedSettlementRoadSegment> BuildRoads(SettlementLayoutContext context, System.Random random)
        {
            var roads = new List<GeneratedSettlementRoadSegment>();
            var baseAngle = random.Range(0f, Mathf.PI * 2f);
            for (var arm = 0; arm < RoadArmCount; arm++)
            {
                var angle = baseAngle + arm / (float)RoadArmCount * Mathf.PI * 2f + random.Range(-0.18f, 0.18f);
                var start = context.Site.Position - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * context.Site.Radius * 0.18f;
                var end = context.Site.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * context.Site.Radius * random.Range(0.72f, 0.94f);
                var midOffsetAngle = angle + Mathf.PI * 0.5f;
                var mid = (start + end) * 0.5f +
                          new Vector2(Mathf.Cos(midOffsetAngle), Mathf.Sin(midOffsetAngle)) *
                          context.Site.Radius * random.Range(-0.16f, 0.16f);

                roads.Add(new GeneratedSettlementRoadSegment(
                    $"internal_road_{arm:D2}",
                    new[] { start, mid, end },
                    context.RoadWidth * (arm == 0 ? 1.25f : 0.82f),
                    arm == 0));
            }

            if (UseDistricts)
            {
                var ring = new List<Vector2>();
                var samples = Mathf.Max(8, RoadArmCount * 2);
                for (var i = 0; i <= samples; i++)
                {
                    var angle = baseAngle + i / (float)samples * Mathf.PI * 2f;
                    var radius = context.Site.Radius * (0.36f + Mathf.Sin(i * 1.7f) * 0.035f);
                    ring.Add(context.Site.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }

                roads.Add(new GeneratedSettlementRoadSegment("internal_ring_road", ring, context.RoadWidth * 0.75f, false));
            }

            return roads;
        }

        private static IReadOnlyList<Vector2> BuildDebugPoints(
            IReadOnlyList<GeneratedSettlementRoadSegment> roads,
            IReadOnlyList<GeneratedSettlementDistrict> districts)
        {
            var points = new List<Vector2>();
            for (var i = 0; i < districts.Count; i++)
            {
                points.Add(districts[i].Center);
            }

            for (var roadIndex = 0; roadIndex < roads.Count; roadIndex++)
            {
                var road = roads[roadIndex];
                for (var pointIndex = 0; pointIndex < road.Points.Count; pointIndex++)
                {
                    points.Add(road.Points[pointIndex]);
                }
            }

            return points;
        }

        private void PlaceBuildingsAlongRoads(
            SettlementLayoutContext context,
            System.Random random,
            IReadOnlyList<GeneratedSettlementRoadSegment> roads,
            IReadOnlyList<GeneratedSettlementDistrict> districts,
            IReadOnlyList<GeneratedSettlementBuildingDefinition> buildingPlan,
            BuildingPlacementSolver solver)
        {
            if (roads.Count == 0 || buildingPlan.Count == 0)
            {
                return;
            }

            var roadCursor = 0;
            for (var i = 0; i < buildingPlan.Count; i++)
            {
                var definition = buildingPlan[i];
                var placed = false;
                for (var attempt = 0; attempt < 16 && !placed; attempt++)
                {
                    var road = roads[roadCursor % roads.Count];
                    roadCursor++;
                    if (road.Points.Count < 2)
                    {
                        continue;
                    }

                    var segmentIndex = random.Range(1, road.Points.Count - 1);
                    var a = road.Points[segmentIndex - 1];
                    var b = road.Points[segmentIndex];
                    var t = random.Range(0.15f, 0.85f);
                    var center = Vector2.Lerp(a, b, t);
                    var tangent = (b - a).normalized;
                    if (tangent.sqrMagnitude <= 0.001f)
                    {
                        tangent = (center - context.Site.Position).normalized;
                    }

                    var normal = new Vector2(-tangent.y, tangent.x);
                    if (random.Chance(0.5f))
                    {
                        normal = -normal;
                    }

                    var roadDistance = Mathf.Max(definition.FootprintSize.x, definition.FootprintSize.y) * PlotRoadOffsetFactor + road.Width + context.PlotPadding;
                    var jitter = tangent * random.Range(-definition.FootprintSize.x * 0.35f, definition.FootprintSize.x * 0.35f);
                    var position = center + normal * roadDistance + jitter;
                    var delta = position - context.Site.Position;
                    if (delta.magnitude > context.Site.Radius * 0.92f)
                    {
                        position = context.Site.Position + delta.normalized * context.Site.Radius * random.Range(0.55f, 0.88f);
                    }

                    var district = FindNearestDistrict(districts, position);
                    placed = solver.TryPlace(
                        definition,
                        district.Id,
                        position,
                        Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg + 90f,
                        context.PlotPadding,
                        context.SlopeSampler,
                        context.MaxBuildingSlope,
                        context.ForbiddenSampler,
                        out _);
                }
            }
        }

        private static GeneratedSettlementDistrict FindNearestDistrict(IReadOnlyList<GeneratedSettlementDistrict> districts, Vector2 position)
        {
            var best = districts[0];
            var bestDistance = float.MaxValue;
            for (var i = 0; i < districts.Count; i++)
            {
                var distance = (districts[i].Center - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = districts[i];
                }
            }

            return best;
        }

        private static void PlaceWallsAndGates(
            SettlementLayoutContext context,
            System.Random random,
            GeneratedSettlementDistrict coreDistrict,
            BuildingPlacementSolver solver)
        {
            var wall = FindDefinition(context, BuildingType.Wall);
            var gate = FindDefinition(context, BuildingType.Gate);
            if (wall == null && gate == null)
            {
                return;
            }

            var wallCount = context.Site.Tier == SettlementTier.Capital ? 16 : 12;
            for (var i = 0; i < wallCount; i++)
            {
                var isGate = i % (wallCount / 4) == 0;
                var definition = isGate && gate != null ? gate : wall;
                if (definition == null)
                {
                    continue;
                }

                var angle = i / (float)wallCount * Mathf.PI * 2f;
                var position = context.Site.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * context.Site.Radius * 0.95f;
                solver.TryPlace(
                    definition,
                    coreDistrict.Id,
                    position,
                    angle * Mathf.Rad2Deg + 90f + random.Range(-3f, 3f),
                    context.PlotPadding * 0.25f,
                    context.SlopeSampler,
                    context.MaxBuildingSlope + 8f,
                    _ => false,
                    out _);
            }
        }

        private static GeneratedSettlementBuildingDefinition FindDefinition(SettlementLayoutContext context, BuildingType type)
        {
            GeneratedSettlementBuildingDefinition best = null;
            for (var i = 0; i < context.BuildingDefinitions.Count; i++)
            {
                var definition = context.BuildingDefinitions[i];
                if (definition.Type != type || definition.MinSettlementTier > context.Site.Tier)
                {
                    continue;
                }

                if (best == null || definition.Level > best.Level)
                {
                    best = definition;
                }
            }

            return best;
        }
    }
}
