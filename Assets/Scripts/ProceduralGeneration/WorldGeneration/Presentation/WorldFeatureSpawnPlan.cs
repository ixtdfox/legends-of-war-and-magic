using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Presentation
{
    public sealed class WorldFeatureSpawnPlan
    {
        public WorldFeatureSpawnPlan(
            IReadOnlyList<SettlementSpawnInstruction> settlements,
            IReadOnlyList<PointOfInterestSpawnInstruction> pointsOfInterest)
        {
            Settlements = settlements ?? Array.Empty<SettlementSpawnInstruction>();
            PointsOfInterest = pointsOfInterest ?? Array.Empty<PointOfInterestSpawnInstruction>();
        }

        public IReadOnlyList<SettlementSpawnInstruction> Settlements { get; }
        public IReadOnlyList<PointOfInterestSpawnInstruction> PointsOfInterest { get; }
    }

    public sealed class SettlementSpawnInstruction
    {
        public SettlementSpawnInstruction(string id, string name, SettlementTier tier, Vector2 worldPosition, IReadOnlyList<SettlementBuildingSpawnInstruction> buildings)
        {
            Id = id ?? string.Empty;
            Name = string.IsNullOrWhiteSpace(name) ? tier.ToString() : name;
            Tier = tier;
            WorldPosition = worldPosition;
            Buildings = buildings ?? Array.Empty<SettlementBuildingSpawnInstruction>();
        }

        public string Id { get; }
        public string Name { get; }
        public SettlementTier Tier { get; }
        public Vector2 WorldPosition { get; }
        public IReadOnlyList<SettlementBuildingSpawnInstruction> Buildings { get; }
    }

    public sealed class SettlementBuildingSpawnInstruction
    {
        public SettlementBuildingSpawnInstruction(
            string id,
            BuildingType type,
            int level,
            Vector2 worldPosition,
            float rotationDegrees,
            Vector2 footprintSize,
            string prefabKey)
        {
            Id = id ?? string.Empty;
            Type = type;
            Level = Mathf.Max(1, level);
            WorldPosition = worldPosition;
            RotationDegrees = rotationDegrees;
            FootprintSize = new Vector2(Mathf.Max(0.5f, footprintSize.x), Mathf.Max(0.5f, footprintSize.y));
            PrefabKey = prefabKey ?? string.Empty;
        }

        public string Id { get; }
        public BuildingType Type { get; }
        public int Level { get; }
        public Vector2 WorldPosition { get; }
        public float RotationDegrees { get; }
        public Vector2 FootprintSize { get; }
        public string PrefabKey { get; }
    }

    public sealed class PointOfInterestSpawnInstruction
    {
        public PointOfInterestSpawnInstruction(
            string id,
            PointOfInterestType type,
            int dangerLevel,
            float radius,
            Vector2 worldPosition,
            string prefabKey,
            IReadOnlyList<string> tags)
        {
            Id = id ?? string.Empty;
            Type = type;
            DangerLevel = Mathf.Clamp(dangerLevel, 0, 10);
            Radius = Mathf.Max(1f, radius);
            WorldPosition = worldPosition;
            PrefabKey = prefabKey ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
        }

        public string Id { get; }
        public PointOfInterestType Type { get; }
        public int DangerLevel { get; }
        public float Radius { get; }
        public Vector2 WorldPosition { get; }
        public string PrefabKey { get; }
        public IReadOnlyList<string> Tags { get; }
    }

    public static class WorldFeatureSpawnPlanBuilder
    {
        public static WorldFeatureSpawnPlan Build(WorldGenerationLayers layers)
        {
            if (layers == null)
            {
                return new WorldFeatureSpawnPlan(Array.Empty<SettlementSpawnInstruction>(), Array.Empty<PointOfInterestSpawnInstruction>());
            }

            return new WorldFeatureSpawnPlan(BuildSettlements(layers), BuildPointsOfInterest(layers));
        }

        private static IReadOnlyList<SettlementSpawnInstruction> BuildSettlements(WorldGenerationLayers layers)
        {
            var result = new List<SettlementSpawnInstruction>(layers.Settlements.Count);
            for (var i = 0; i < layers.Settlements.Count; i++)
            {
                var settlement = layers.Settlements[i];
                var buildings = new List<SettlementBuildingSpawnInstruction>(settlement.Buildings.Count);
                for (var buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    var building = settlement.Buildings[buildingIndex];
                    buildings.Add(new SettlementBuildingSpawnInstruction(
                        building.Id,
                        building.Definition.Type,
                        building.Definition.Level,
                        building.WorldPosition,
                        building.RotationDegrees,
                        building.FootprintSize,
                        building.Definition.PrefabKey));
                }

                result.Add(new SettlementSpawnInstruction(settlement.Id, settlement.Name, settlement.Tier, settlement.WorldPosition, buildings));
            }

            return result;
        }

        private static IReadOnlyList<PointOfInterestSpawnInstruction> BuildPointsOfInterest(WorldGenerationLayers layers)
        {
            var result = new List<PointOfInterestSpawnInstruction>(layers.PointsOfInterest.Count);
            for (var i = 0; i < layers.PointsOfInterest.Count; i++)
            {
                var poi = layers.PointsOfInterest[i];
                result.Add(new PointOfInterestSpawnInstruction(
                    poi.Id,
                    poi.Type,
                    poi.DangerLevel,
                    poi.Radius,
                    poi.WorldPosition,
                    poi.PrefabKey,
                    poi.Tags));
            }

            return result;
        }
    }
}
