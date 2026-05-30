using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public sealed class SettlementSiteSelector
    {
        public IReadOnlyList<SettlementSite> SelectSites(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler terrainSampler,
            SettlementGenerationConfig config,
            WorldGenerationMaskSet masks,
            int seed)
        {
            if (settings == null || terrainSampler == null || config == null || !config.Enabled)
            {
                return Array.Empty<SettlementSite>();
            }

            var random = new System.Random(DeterministicRandom.Hash(seed, 9001));
            var sampleCache = new TerrainSampleCache(terrainSampler, 8f);
            var desiredSettlements = random.Range(config.MinSettlements, config.MaxSettlements);
            var desiredCamps = random.Range(config.MinCamps, config.MaxCamps);
            var targetCount = desiredSettlements + desiredCamps;
            var selected = new List<SettlementSite>(targetCount);
            var index = new SpatialHashGrid2D<SettlementSite>(Mathf.Max(48f, config.MinimumSettlementDistance));
            var candidates = BuildCandidates(settings, config, sampleCache, masks, random, desiredSettlements, desiredCamps);

            candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
            for (var i = 0; i < candidates.Count && selected.Count < targetCount; i++)
            {
                var candidate = candidates[i];
                if (index.QueryRadius(candidate.Position, config.MinimumSettlementDistance + candidate.Radius).Count > 0)
                {
                    continue;
                }

                selected.Add(candidate);
                index.Insert(Bounds2D.FromCircle(candidate.Position, candidate.Radius), candidate);
            }

            return selected;
        }

        private static List<SettlementSite> BuildCandidates(
            ProceduralLocationSettings settings,
            SettlementGenerationConfig config,
            TerrainSampleCache sampler,
            WorldGenerationMaskSet masks,
            System.Random random,
            int desiredSettlements,
            int desiredCamps)
        {
            var bounds = settings.GetWorldBounds();
            var min = bounds.min;
            var max = bounds.max;
            var spacing = config.CandidateSpacing;
            var candidates = new List<SettlementSite>();
            var tierQueue = BuildTierQueue(config, random, desiredSettlements, desiredCamps);

            for (var tierIndex = 0; tierIndex < tierQueue.Count; tierIndex++)
            {
                var tier = tierQueue[tierIndex];
                var tierConfig = ResolveTierConfig(config, tier);
                var attempts = Mathf.Max(30, Mathf.RoundToInt((bounds.size.x * bounds.size.z) / Mathf.Max(1f, spacing * spacing) * 0.5f));

                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    var x = random.Range(min.x + config.EdgePadding, max.x - config.EdgePadding);
                    var z = random.Range(min.z + config.EdgePadding, max.z - config.EdgePadding);
                    var position = new Vector2(x, z);
                    if (!sampler.TrySample(x, z, out var point, out var normal))
                    {
                        continue;
                    }

                    var slope = Vector3.Angle(normal, Vector3.up);
                    if (slope > config.MaxSiteSlope + ResolveTierSlopeTolerance(tier))
                    {
                        continue;
                    }

                    if (settings.WaterEnabled && point.y <= settings.WaterLevel + ResolveWaterClearance(tier))
                    {
                        continue;
                    }

                    if (masks != null && masks.IsNoSpawn(position))
                    {
                        continue;
                    }

                    var radiusRange = tierConfig.RadiusRange;
                    var radius = random.Range(radiusRange.x, radiusRange.y);
                    var scoreTags = new List<string>();
                    var score = ScoreSite(settings, config, tier, position, point.y, slope, bounds, scoreTags);
                    candidates.Add(new SettlementSite(tier, position, radius, score, scoreTags));
                }
            }

            return candidates;
        }

        private static List<SettlementTier> BuildTierQueue(
            SettlementGenerationConfig config,
            System.Random random,
            int desiredSettlements,
            int desiredCamps)
        {
            var tiers = new List<SettlementTier>();
            for (var i = 0; i < desiredCamps; i++)
            {
                tiers.Add(i == 0 && desiredSettlements == 0 ? SettlementTier.Hamlet : SettlementTier.Camp);
            }

            var hasLarge = desiredSettlements > 0 && random.Chance(config.LargeSettlementChance);
            for (var i = 0; i < desiredSettlements; i++)
            {
                if (i == 0 && hasLarge)
                {
                    tiers.Add(random.Chance(config.CapitalChance) ? SettlementTier.Capital : random.Chance(0.45f) ? SettlementTier.City : SettlementTier.Town);
                    continue;
                }

                tiers.Add(random.Chance(0.36f) ? SettlementTier.Village : SettlementTier.Hamlet);
            }

            return tiers;
        }

        private static SettlementTierConfig ResolveTierConfig(SettlementGenerationConfig config, SettlementTier tier)
        {
            return config.BuildingCatalog != null
                ? config.BuildingCatalog.ResolveTierConfig(tier)
                : DefaultSettlementBuildingCatalog.CreateTierConfig(tier);
        }

        private static float ScoreSite(
            ProceduralLocationSettings settings,
            SettlementGenerationConfig config,
            SettlementTier tier,
            Vector2 position,
            float height,
            float slope,
            Bounds bounds,
            List<string> scoreTags)
        {
            var flatness = 1f - Mathf.InverseLerp(2f, config.MaxSiteSlope + ResolveTierSlopeTolerance(tier), slope);
            var waterScore = 0f;
            if (settings.WaterEnabled)
            {
                var aboveWater = height - settings.WaterLevel;
                waterScore = Mathf.Clamp01(1f - Mathf.Abs(aboveWater - ResolvePreferredWaterOffset(tier)) / Mathf.Max(8f, config.WaterSearchRadius));
            }

            var normalized = new Vector2(
                Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x),
                Mathf.InverseLerp(bounds.min.z, bounds.max.z, position.y));
            var centerDistance = Vector2.Distance(normalized, new Vector2(0.5f, 0.5f));
            var centrality = 1f - Mathf.Clamp01(centerDistance / 0.707f);
            var strategic = tier >= SettlementTier.Town ? Mathf.Max(centrality, 1f - Mathf.Abs(centerDistance - 0.38f) * 2.2f) : 0.2f + centrality * 0.35f;
            var edgePenalty = 1f - Mathf.Clamp01(Mathf.Min(
                Mathf.Min(position.x - bounds.min.x, bounds.max.x - position.x),
                Mathf.Min(position.y - bounds.min.z, bounds.max.z - position.y)) / Mathf.Max(1f, config.EdgePadding));

            if (flatness > 0.72f)
            {
                scoreTags.Add("flat");
            }

            if (waterScore > 0.55f)
            {
                scoreTags.Add("water-access");
            }

            if (strategic > 0.64f)
            {
                scoreTags.Add("strategic");
            }

            return flatness * 4.2f +
                   waterScore * ResolveWaterWeight(tier) +
                   strategic * ResolveStrategicWeight(tier) -
                   slope * 0.045f -
                   edgePenalty * 2.8f;
        }

        private static float ResolveTierSlopeTolerance(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 8f,
                SettlementTier.Hamlet => 4f,
                SettlementTier.Village => 2f,
                _ => 0f
            };
        }

        private static float ResolveWaterClearance(SettlementTier tier)
        {
            return tier >= SettlementTier.Town ? 4f : 1.5f;
        }

        private static float ResolvePreferredWaterOffset(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 10f,
                SettlementTier.Hamlet => 14f,
                SettlementTier.Village => 18f,
                SettlementTier.Town => 22f,
                _ => 26f
            };
        }

        private static float ResolveWaterWeight(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 0.9f,
                SettlementTier.Hamlet => 1.4f,
                SettlementTier.Village => 1.9f,
                SettlementTier.Town => 2.2f,
                _ => 2.5f
            };
        }

        private static float ResolveStrategicWeight(SettlementTier tier)
        {
            return tier switch
            {
                SettlementTier.Camp => 0.5f,
                SettlementTier.Hamlet => 0.8f,
                SettlementTier.Village => 1.2f,
                SettlementTier.Town => 2.6f,
                SettlementTier.City => 3.2f,
                SettlementTier.Capital => 3.8f,
                _ => 1f
            };
        }
    }
}
