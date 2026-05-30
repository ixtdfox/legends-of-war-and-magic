using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public sealed class SettlementSite
    {
        public SettlementSite(SettlementTier tier, Vector2 position, float radius, float score, IReadOnlyList<string> scoreTags)
        {
            Tier = tier;
            Position = position;
            Radius = Mathf.Max(1f, radius);
            Score = score;
            ScoreTags = scoreTags ?? Array.Empty<string>();
        }

        public SettlementTier Tier { get; }
        public Vector2 Position { get; }
        public float Radius { get; }
        public float Score { get; }
        public IReadOnlyList<string> ScoreTags { get; }
    }

    public sealed class SettlementLayout
    {
        public SettlementLayout(
            IReadOnlyList<GeneratedSettlementDistrict> districts,
            IReadOnlyList<GeneratedSettlementBuilding> buildings,
            IReadOnlyList<GeneratedSettlementRoadSegment> roads,
            IReadOnlyList<Vector2> debugPoints,
            IReadOnlyList<RejectedPlacement> rejectedPlacements)
        {
            Districts = districts ?? Array.Empty<GeneratedSettlementDistrict>();
            Buildings = buildings ?? Array.Empty<GeneratedSettlementBuilding>();
            Roads = roads ?? Array.Empty<GeneratedSettlementRoadSegment>();
            DebugPoints = debugPoints ?? Array.Empty<Vector2>();
            RejectedPlacements = rejectedPlacements ?? Array.Empty<RejectedPlacement>();
        }

        public IReadOnlyList<GeneratedSettlementDistrict> Districts { get; }
        public IReadOnlyList<GeneratedSettlementBuilding> Buildings { get; }
        public IReadOnlyList<GeneratedSettlementRoadSegment> Roads { get; }
        public IReadOnlyList<Vector2> DebugPoints { get; }
        public IReadOnlyList<RejectedPlacement> RejectedPlacements { get; }
    }

    public sealed class SettlementLayoutContext
    {
        public SettlementLayoutContext(
            int seed,
            SettlementSite site,
            IReadOnlyList<GeneratedSettlementBuildingDefinition> buildingDefinitions,
            IReadOnlyList<BuildingType> requiredBuildingTypes,
            IReadOnlyList<BuildingType> optionalBuildingTypes,
            int targetBuildingCount,
            float roadWidth,
            float plotPadding,
            float maxBuildingSlope,
            Func<Vector2, float> slopeSampler,
            Func<Vector2, bool> forbiddenSampler)
        {
            Seed = seed;
            Site = site ?? throw new ArgumentNullException(nameof(site));
            BuildingDefinitions = buildingDefinitions ?? Array.Empty<GeneratedSettlementBuildingDefinition>();
            RequiredBuildingTypes = requiredBuildingTypes ?? Array.Empty<BuildingType>();
            OptionalBuildingTypes = optionalBuildingTypes ?? Array.Empty<BuildingType>();
            TargetBuildingCount = Mathf.Max(1, targetBuildingCount);
            RoadWidth = Mathf.Max(0.5f, roadWidth);
            PlotPadding = Mathf.Max(0f, plotPadding);
            MaxBuildingSlope = Mathf.Max(1f, maxBuildingSlope);
            SlopeSampler = slopeSampler ?? (_ => 0f);
            ForbiddenSampler = forbiddenSampler ?? (_ => false);
        }

        public int Seed { get; }
        public SettlementSite Site { get; }
        public IReadOnlyList<GeneratedSettlementBuildingDefinition> BuildingDefinitions { get; }
        public IReadOnlyList<BuildingType> RequiredBuildingTypes { get; }
        public IReadOnlyList<BuildingType> OptionalBuildingTypes { get; }
        public int TargetBuildingCount { get; }
        public float RoadWidth { get; }
        public float PlotPadding { get; }
        public float MaxBuildingSlope { get; }
        public Func<Vector2, float> SlopeSampler { get; }
        public Func<Vector2, bool> ForbiddenSampler { get; }
    }

    public sealed class RejectedPlacement
    {
        public RejectedPlacement(Vector2 position, string reason)
        {
            Position = position;
            Reason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason;
        }

        public Vector2 Position { get; }
        public string Reason { get; }
    }

    public interface ISettlementLayoutStrategy
    {
        string Name { get; }
        bool Supports(SettlementTier tier);
        SettlementLayout Generate(SettlementLayoutContext context);
    }
}
