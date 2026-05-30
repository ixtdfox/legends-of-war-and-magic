using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public sealed class BuildingPlacementSolver
    {
        private readonly SpatialHashGrid2D<GeneratedSettlementBuilding> footprintIndex;
        private readonly List<GeneratedSettlementBuilding> buildings = new();
        private readonly List<RejectedPlacement> rejections = new();

        public BuildingPlacementSolver(float cellSize)
        {
            footprintIndex = new SpatialHashGrid2D<GeneratedSettlementBuilding>(cellSize);
        }

        public IReadOnlyList<GeneratedSettlementBuilding> Buildings => buildings;
        public IReadOnlyList<RejectedPlacement> Rejections => rejections;

        public bool TryPlace(
            GeneratedSettlementBuildingDefinition definition,
            string districtId,
            Vector2 position,
            float rotationDegrees,
            float padding,
            Func<Vector2, float> slopeSampler,
            float maxSlope,
            Func<Vector2, bool> forbiddenSampler,
            out GeneratedSettlementBuilding instance)
        {
            instance = null;
            if (definition == null)
            {
                rejections.Add(new RejectedPlacement(position, "MissingDefinition"));
                return false;
            }

            if (forbiddenSampler != null && forbiddenSampler(position))
            {
                rejections.Add(new RejectedPlacement(position, "ForbiddenZone"));
                return false;
            }

            var slope = slopeSampler != null ? slopeSampler(position) : 0f;
            if (slope > maxSlope)
            {
                rejections.Add(new RejectedPlacement(position, "Slope"));
                return false;
            }

            var bounds = ResolveBounds(position, definition.FootprintSize, rotationDegrees, padding);
            if (footprintIndex.Any(bounds))
            {
                rejections.Add(new RejectedPlacement(position, "FootprintOverlap"));
                return false;
            }

            instance = new GeneratedSettlementBuilding(
                $"{definition.Id}_{buildings.Count + 1:D3}",
                definition,
                districtId,
                position,
                rotationDegrees,
                definition.FootprintSize);
            buildings.Add(instance);
            footprintIndex.Insert(bounds, instance);
            return true;
        }

        public static Bounds2D ResolveBounds(Vector2 center, Vector2 size, float rotationDegrees, float padding)
        {
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Abs(Mathf.Sin(radians));
            var cos = Mathf.Abs(Mathf.Cos(radians));
            var rotatedWidth = size.x * cos + size.y * sin;
            var rotatedHeight = size.x * sin + size.y * cos;
            var expanded = new Vector2(rotatedWidth + padding * 2f, rotatedHeight + padding * 2f);
            return new Bounds2D(center, expanded);
        }
    }
}
