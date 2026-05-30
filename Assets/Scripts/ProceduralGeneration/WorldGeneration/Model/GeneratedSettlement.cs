using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Domain.Common;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public sealed class GeneratedSettlementDiagnostics
    {
        public GeneratedSettlementDiagnostics(
            int seed,
            float siteScore,
            string layoutStrategy,
            IReadOnlyList<string> debugTags,
            IReadOnlyList<string> rejectedReasons)
        {
            Seed = seed;
            SiteScore = siteScore;
            LayoutStrategy = layoutStrategy ?? string.Empty;
            DebugTags = debugTags ?? Array.Empty<string>();
            RejectedReasons = rejectedReasons ?? Array.Empty<string>();
        }

        public int Seed { get; }
        public float SiteScore { get; }
        public string LayoutStrategy { get; }
        public IReadOnlyList<string> DebugTags { get; }
        public IReadOnlyList<string> RejectedReasons { get; }
    }

    public sealed class GeneratedSettlementDistrict
    {
        public GeneratedSettlementDistrict(string id, SettlementDistrictType type, string name, Vector2 center, float radius)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Type = type;
            Name = string.IsNullOrWhiteSpace(name) ? type.ToString() : name;
            Center = center;
            Radius = Mathf.Max(1f, radius);
        }

        public string Id { get; }
        public SettlementDistrictType Type { get; }
        public string Name { get; }
        public Vector2 Center { get; }
        public float Radius { get; }
    }

    public sealed class GeneratedSettlementBuildingDefinition
    {
        private readonly SettlementBuildingDefinition buildingDefinition;

        public GeneratedSettlementBuildingDefinition(
            string id,
            string displayName,
            BuildingType type,
            int level,
            SettlementTier minSettlementTier,
            Vector2 footprintSize,
            IReadOnlyList<BuildingTag> tags,
            string prefabKey)
        {
            buildingDefinition = new SettlementBuildingDefinition(
                id,
                displayName,
                type,
                level,
                minSettlementTier,
                new WorldSize2D(footprintSize.x, footprintSize.y),
                tags);
            PrefabKey = prefabKey ?? string.Empty;
        }

        public SettlementBuildingDefinition BuildingDefinition => buildingDefinition;
        public string Id => buildingDefinition.Id;
        public string DisplayName => buildingDefinition.DisplayName;
        public BuildingType Type => buildingDefinition.Type;
        public int Level => buildingDefinition.Level;
        public SettlementTier MinSettlementTier => buildingDefinition.MinSettlementTier;
        public Vector2 FootprintSize => buildingDefinition.FootprintSize.ToVector2();
        public IReadOnlyList<BuildingTag> Tags => buildingDefinition.Tags;
        public string PrefabKey { get; }
    }

    public sealed class GeneratedSettlementBuilding
    {
        public GeneratedSettlementBuilding(
            string id,
            GeneratedSettlementBuildingDefinition definition,
            string districtId,
            Vector2 worldPosition,
            float rotationDegrees,
            Vector2 footprintSize)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            DistrictId = districtId ?? string.Empty;
            WorldPosition = worldPosition;
            RotationDegrees = rotationDegrees;
            FootprintSize = new Vector2(Mathf.Max(0.5f, footprintSize.x), Mathf.Max(0.5f, footprintSize.y));
        }

        public string Id { get; }
        public GeneratedSettlementBuildingDefinition Definition { get; }
        public string DistrictId { get; }
        public Vector2 WorldPosition { get; }
        public float RotationDegrees { get; }
        public Vector2 FootprintSize { get; }
    }

    public sealed class GeneratedSettlementRoadSegment
    {
        public GeneratedSettlementRoadSegment(string id, IReadOnlyList<Vector2> points, float width, bool primary)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Points = points ?? Array.Empty<Vector2>();
            Width = Mathf.Max(0.25f, width);
            Primary = primary;
        }

        public string Id { get; }
        public IReadOnlyList<Vector2> Points { get; }
        public float Width { get; }
        public bool Primary { get; }
    }

    public sealed class GeneratedSettlement
    {
        public GeneratedSettlement(
            string id,
            string name,
            SettlementTier tier,
            FactionId? ownerFactionId,
            Vector2 worldPosition,
            float radius,
            IReadOnlyList<GeneratedSettlementDistrict> districts,
            IReadOnlyList<GeneratedSettlementBuilding> buildings,
            IReadOnlyList<GeneratedSettlementRoadSegment> internalRoads,
            SettlementStats stats,
            SettlementProgression progression,
            SettlementEconomy economy,
            GeneratedSettlementDiagnostics diagnostics)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? tier.ToString() : name;
            Tier = tier;
            OwnerFactionId = ownerFactionId;
            WorldPosition = worldPosition;
            Radius = Mathf.Max(1f, radius);
            Districts = districts ?? Array.Empty<GeneratedSettlementDistrict>();
            Buildings = buildings ?? Array.Empty<GeneratedSettlementBuilding>();
            InternalRoads = internalRoads ?? Array.Empty<GeneratedSettlementRoadSegment>();
            Stats = stats ?? new SettlementStats(0, 0, 0, 0, 0, Array.Empty<SettlementService>(), Array.Empty<string>());
            Progression = progression ?? new SettlementProgression(1, 0, 0, 0, Array.Empty<SettlementUpgradeRequirement>());
            Economy = economy ?? new SettlementEconomy(Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(), 0f);
            Diagnostics = diagnostics ?? new GeneratedSettlementDiagnostics(0, 0f, string.Empty, Array.Empty<string>(), Array.Empty<string>());
        }

        public string Id { get; }
        public string Name { get; }
        public SettlementTier Tier { get; }
        public FactionId? OwnerFactionId { get; }
        public Vector2 WorldPosition { get; }
        public float Radius { get; }
        public IReadOnlyList<GeneratedSettlementDistrict> Districts { get; }
        public IReadOnlyList<GeneratedSettlementBuilding> Buildings { get; }
        public IReadOnlyList<GeneratedSettlementRoadSegment> InternalRoads { get; }
        public SettlementStats Stats { get; }
        public SettlementProgression Progression { get; }
        public SettlementEconomy Economy { get; }
        public GeneratedSettlementDiagnostics Diagnostics { get; }
    }
}
