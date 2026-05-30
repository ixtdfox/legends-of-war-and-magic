using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public sealed class SettlementGenerator
    {
        private readonly ISettlementLayoutStrategy[] strategies =
        {
            new CampLayoutStrategy(),
            new HamletLayoutStrategy(),
            new VillageOrganicLayoutStrategy(),
            new TownDistrictLayoutStrategy(),
            new CityWalledLayoutStrategy()
        };

        public IReadOnlyList<GeneratedSettlement> Generate(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler terrainSampler,
            SettlementGenerationConfig config,
            WorldGenerationMaskSet masks,
            int seed)
        {
            if (settings == null || terrainSampler == null || config == null || !config.Enabled)
            {
                return Array.Empty<GeneratedSettlement>();
            }

            var siteSelector = new SettlementSiteSelector();
            var sites = siteSelector.SelectSites(settings, terrainSampler, config, masks, seed);
            var definitions = config.BuildingCatalog != null
                ? config.BuildingCatalog.BuildDefinitions()
                : DefaultSettlementBuildingCatalog.CreateDefinitions();
            var sampleCache = new TerrainSampleCache(terrainSampler, 6f);
            var settlements = new List<GeneratedSettlement>(sites.Count);

            for (var i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                var tierConfig = ResolveTierConfig(config, site.Tier);
                var strategy = ResolveStrategy(site.Tier);
                var random = new System.Random(DeterministicRandom.Hash(seed, i * 977 + (int)site.Tier * 131));
                var buildingCount = random.Range(tierConfig.BuildingCountRange.x, tierConfig.BuildingCountRange.y);
                var layoutContext = new SettlementLayoutContext(
                    DeterministicRandom.Hash(seed, i * 2381 + 47),
                    site,
                    definitions,
                    tierConfig.RequiredBuildings,
                    tierConfig.OptionalBuildings,
                    buildingCount,
                    tierConfig.RoadWidth,
                    config.PlotPadding,
                    config.MaxBuildingSlope,
                    position => sampleCache.SampleSlope(position.x, position.y),
                    position => masks != null && masks.IsNoSpawn(position));
                var layout = strategy.Generate(layoutContext);
                var settlement = BuildSettlement(site, layout, strategy.Name, seed, i, random);
                settlements.Add(settlement);
                AddMasks(masks, settlement, config);
            }

            return settlements;
        }

        private ISettlementLayoutStrategy ResolveStrategy(SettlementTier tier)
        {
            for (var i = 0; i < strategies.Length; i++)
            {
                if (strategies[i].Supports(tier))
                {
                    return strategies[i];
                }
            }

            return strategies[0];
        }

        private static SettlementTierConfig ResolveTierConfig(SettlementGenerationConfig config, SettlementTier tier)
        {
            return config.BuildingCatalog != null
                ? config.BuildingCatalog.ResolveTierConfig(tier)
                : DefaultSettlementBuildingCatalog.CreateTierConfig(tier);
        }

        private static GeneratedSettlement BuildSettlement(
            SettlementSite site,
            SettlementLayout layout,
            string strategyName,
            int seed,
            int index,
            System.Random random)
        {
            var tier = site.Tier;
            var services = ResolveServices(layout.Buildings);
            var stats = new SettlementStats(
                ResolvePopulation(tier, layout.Buildings.Count, random),
                ResolveWealth(tier, random),
                ResolveSecurity(tier, layout.Buildings),
                ResolveFood(tier, layout.Buildings),
                ResolveProduction(layout.Buildings),
                services,
                ResolveQuestHooks(tier, random));
            var progression = new SettlementProgression(
                Mathf.Max(1, (int)tier + 1),
                Mathf.Clamp(stats.Wealth + random.Range(-8, 8), 0, 100),
                stats.Safety,
                random.Range(-8, 16),
                ResolveUpgradeRequirements(tier));
            var economy = new SettlementEconomy(
                new[]
                {
                    new ResourceAmount("food", Mathf.Max(12, stats.Food * 3)),
                    new ResourceAmount("wood", 20 + layout.Buildings.Count * 3),
                    new ResourceAmount("iron", stats.Production)
                },
                new[]
                {
                    new ResourceAmount("food", Mathf.Max(1, stats.Food / 8)),
                    new ResourceAmount("crafted_goods", Mathf.Max(0, stats.Production / 10))
                },
                new[]
                {
                    new ResourceAmount("tools", Mathf.Max(1, layout.Buildings.Count / 5)),
                    new ResourceAmount("luxury_goods", tier >= SettlementTier.Town ? 3 : 0)
                },
                tier >= SettlementTier.Town ? 0.08f : 0.03f);
            var rejected = new List<string>();
            for (var i = 0; i < layout.RejectedPlacements.Count; i++)
            {
                rejected.Add(layout.RejectedPlacements[i].Reason);
            }

            return new GeneratedSettlement(
                $"settlement_{index:D2}_{tier}",
                SettlementNameGenerator.Generate(tier, seed, index),
                tier,
                null,
                site.Position,
                site.Radius,
                layout.Districts,
                layout.Buildings,
                layout.Roads,
                stats,
                progression,
                economy,
                new GeneratedSettlementDiagnostics(seed, site.Score, strategyName, site.ScoreTags, rejected));
        }

        private static void AddMasks(WorldGenerationMaskSet masks, GeneratedSettlement settlement, SettlementGenerationConfig config)
        {
            if (masks == null || settlement == null)
            {
                return;
            }

            masks.AddZone(new GenerationMaskZone(
                $"{settlement.Id}_footprint",
                GenerationZoneKind.SettlementFootprint,
                settlement.WorldPosition,
                settlement.Radius,
                1f,
                settlement.Id));
            masks.AddZone(new GenerationMaskZone(
                $"{settlement.Id}_clearing",
                GenerationZoneKind.ReducedVegetation,
                settlement.WorldPosition,
                settlement.Radius + config.ClearingRadiusPadding,
                0.7f,
                settlement.Id));
            for (var i = 0; i < settlement.Buildings.Count; i++)
            {
                var building = settlement.Buildings[i];
                var radius = Mathf.Max(building.FootprintSize.x, building.FootprintSize.y) * 0.72f + config.PlotPadding;
                masks.AddZone(new GenerationMaskZone(
                    $"{building.Id}_no_spawn",
                    GenerationZoneKind.NoSpawn,
                    building.WorldPosition,
                    radius,
                    1f,
                    settlement.Id));
            }

            for (var i = 0; i < settlement.InternalRoads.Count; i++)
            {
                var road = settlement.InternalRoads[i];
                masks.AddPath(new GenerationPathMask(
                    road.Id,
                    GenerationZoneKind.Road,
                    road.Points,
                    road.Width * 0.5f + 1.2f,
                    1f,
                    settlement.Id));
            }
        }

        private static IReadOnlyList<SettlementService> ResolveServices(IReadOnlyList<GeneratedSettlementBuilding> buildings)
        {
            var services = new List<SettlementService>();
            for (var i = 0; i < buildings.Count; i++)
            {
                switch (buildings[i].Definition.Type)
                {
                    case BuildingType.Tavern:
                        AddUnique(services, SettlementService.Rest);
                        AddUnique(services, SettlementService.Rumors);
                        break;
                    case BuildingType.Market:
                        AddUnique(services, SettlementService.Trade);
                        break;
                    case BuildingType.Blacksmith:
                        AddUnique(services, SettlementService.Repair);
                        AddUnique(services, SettlementService.Crafting);
                        break;
                    case BuildingType.Temple:
                    case BuildingType.Shrine:
                        AddUnique(services, SettlementService.Healing);
                        break;
                    case BuildingType.Barracks:
                    case BuildingType.Watchtower:
                        AddUnique(services, SettlementService.Training);
                        break;
                    case BuildingType.Storage:
                        AddUnique(services, SettlementService.Storage);
                        break;
                    case BuildingType.TownHall:
                        AddUnique(services, SettlementService.Governance);
                        break;
                }
            }

            return services;
        }

        private static void AddUnique<T>(ICollection<T> values, T value)
        {
            if (!values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static int ResolvePopulation(SettlementTier tier, int buildings, System.Random random)
        {
            var basePopulation = tier switch
            {
                SettlementTier.Camp => random.Range(5, 18),
                SettlementTier.Hamlet => random.Range(24, 70),
                SettlementTier.Village => random.Range(80, 220),
                SettlementTier.Town => random.Range(320, 900),
                SettlementTier.City => random.Range(1400, 4200),
                SettlementTier.Capital => random.Range(4800, 12000),
                _ => 30
            };

            return basePopulation + buildings * (tier >= SettlementTier.Town ? 12 : 4);
        }

        private static int ResolveWealth(SettlementTier tier, System.Random random)
        {
            return Mathf.Clamp((int)tier * 12 + 20 + random.Range(-8, 16), 0, 100);
        }

        private static int ResolveSecurity(SettlementTier tier, IReadOnlyList<GeneratedSettlementBuilding> buildings)
        {
            var security = 20 + (int)tier * 8;
            for (var i = 0; i < buildings.Count; i++)
            {
                var type = buildings[i].Definition.Type;
                if (type == BuildingType.Wall || type == BuildingType.Gate || type == BuildingType.Watchtower || type == BuildingType.Barracks)
                {
                    security += 6;
                }
            }

            return Mathf.Clamp(security, 0, 100);
        }

        private static int ResolveFood(SettlementTier tier, IReadOnlyList<GeneratedSettlementBuilding> buildings)
        {
            var food = 35 + (tier <= SettlementTier.Village ? 22 : 5);
            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].Definition.Type == BuildingType.Farm || buildings[i].Definition.Type == BuildingType.Storage)
                {
                    food += 8;
                }
            }

            return Mathf.Clamp(food, 0, 100);
        }

        private static int ResolveProduction(IReadOnlyList<GeneratedSettlementBuilding> buildings)
        {
            var production = 10;
            for (var i = 0; i < buildings.Count; i++)
            {
                var type = buildings[i].Definition.Type;
                if (type == BuildingType.Blacksmith || type == BuildingType.Workshop || type == BuildingType.Market || type == BuildingType.Storage)
                {
                    production += 8 + buildings[i].Definition.Level * 4;
                }
            }

            return Mathf.Clamp(production, 0, 100);
        }

        private static IReadOnlyList<string> ResolveQuestHooks(SettlementTier tier, System.Random random)
        {
            var hooks = new List<string>();
            if (tier <= SettlementTier.Hamlet)
            {
                hooks.Add("local_trouble");
            }
            else
            {
                hooks.Add("regional_contract");
            }

            if (tier >= SettlementTier.Town)
            {
                hooks.Add("faction_politics");
            }

            if (random.Chance(0.25f))
            {
                hooks.Add("resource_shortage");
            }

            return hooks;
        }

        private static IReadOnlyList<SettlementUpgradeRequirement> ResolveUpgradeRequirements(SettlementTier tier)
        {
            if (tier >= SettlementTier.Capital)
            {
                return Array.Empty<SettlementUpgradeRequirement>();
            }

            var nextTier = (SettlementTier)((int)tier + 1);
            return new[]
            {
                new SettlementUpgradeRequirement(
                    nextTier,
                    25 + (int)nextTier * 10,
                    20 + (int)nextTier * 8,
                    new[]
                    {
                        new ResourceAmount("wood", 50 + (int)nextTier * 30),
                        new ResourceAmount("stone", nextTier >= SettlementTier.Town ? 80 : 0),
                        new ResourceAmount("iron", nextTier >= SettlementTier.City ? 45 : 10)
                    },
                    nextTier >= SettlementTier.Town
                        ? new[] { BuildingType.Market, BuildingType.Blacksmith }
                        : new[] { BuildingType.Storage, BuildingType.Well })
            };
        }
    }
}
