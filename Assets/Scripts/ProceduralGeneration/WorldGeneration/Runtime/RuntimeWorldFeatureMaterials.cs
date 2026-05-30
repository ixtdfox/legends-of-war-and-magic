using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime
{
    public static class RuntimeWorldFeatureMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new();

        public static Material BuildingMaterial(BuildingType type)
        {
            return Get($"Building_{type}", ResolveBuildingColor(type));
        }

        public static Material PoiMaterial(PointOfInterestType type)
        {
            return Get($"Poi_{type}", ResolvePoiColor(type));
        }

        public static Material RoadMaterial(RoadType type)
        {
            return Get($"Road_{type}", ResolveRoadColor(type));
        }

        public static Material PrototypeMaterial(string key, Color color)
        {
            return Get(key, color);
        }

        private static Material Get(string key, Color color)
        {
            if (Cache.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }

            var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader)
            {
                name = key
            };
            material.color = color;
            material.SetFloatSafe("_Metallic", 0f);
            material.SetFloatSafe("_Smoothness", key.StartsWith("Road_") ? 0.08f : 0.18f);
            material.SetFloatSafe("_Glossiness", key.StartsWith("Road_") ? 0.08f : 0.18f);
            material.SetColorSafe("_SpecColor", new Color(0.025f, 0.022f, 0.018f, 1f));
            Cache[key] = material;
            return material;
        }

        private static Color ResolveBuildingColor(BuildingType type)
        {
            return type switch
            {
                BuildingType.Tent => new Color(0.65f, 0.55f, 0.42f),
                BuildingType.Campfire => new Color(0.95f, 0.36f, 0.12f),
                BuildingType.Wall => new Color(0.42f, 0.28f, 0.16f),
                BuildingType.Gate => new Color(0.38f, 0.24f, 0.14f),
                BuildingType.Temple => new Color(0.72f, 0.70f, 0.62f),
                BuildingType.Barracks => new Color(0.46f, 0.42f, 0.36f),
                BuildingType.Blacksmith => new Color(0.32f, 0.30f, 0.28f),
                BuildingType.Market => new Color(0.62f, 0.42f, 0.28f),
                BuildingType.Tavern => new Color(0.54f, 0.32f, 0.18f),
                _ => new Color(0.46f, 0.34f, 0.22f)
            };
        }

        private static Color ResolvePoiColor(PointOfInterestType type)
        {
            return type switch
            {
                PointOfInterestType.BanditCamp => new Color(0.38f, 0.16f, 0.12f),
                PointOfInterestType.CaveEntrance => new Color(0.18f, 0.18f, 0.18f),
                PointOfInterestType.AncientTemple => new Color(0.62f, 0.58f, 0.48f),
                PointOfInterestType.Shrine => new Color(0.72f, 0.68f, 0.45f),
                PointOfInterestType.ResourceNode => new Color(0.37f, 0.36f, 0.34f),
                _ => new Color(0.44f, 0.40f, 0.34f)
            };
        }

        private static Color ResolveRoadColor(RoadType type)
        {
            return type switch
            {
                RoadType.Trail => new Color(0.34f, 0.27f, 0.16f),
                RoadType.DirtRoad => new Color(0.42f, 0.32f, 0.18f),
                RoadType.MainRoad => new Color(0.50f, 0.42f, 0.28f),
                RoadType.StoneRoad => new Color(0.46f, 0.46f, 0.42f),
                RoadType.HiddenPath => new Color(0.24f, 0.26f, 0.16f),
                _ => new Color(0.42f, 0.32f, 0.18f)
            };
        }
    }

    internal static class MaterialPropertyExtensions
    {
        public static void SetFloatSafe(this Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        public static void SetColorSafe(this Material material, string property, Color value)
        {
            if (material != null && material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }
    }
}
