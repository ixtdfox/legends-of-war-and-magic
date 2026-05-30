using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Generation
{
    public sealed class PointOfInterestGenerator
    {
        public IReadOnlyList<GeneratedPointOfInterest> Generate(
            ProceduralLocationSettings settings,
            IProceduralTerrainSampler terrainSampler,
            PointOfInterestGenerationConfig config,
            IReadOnlyList<GeneratedSettlement> settlements,
            WorldGenerationMaskSet masks,
            int seed)
        {
            if (settings == null || terrainSampler == null || config == null || !config.Enabled)
            {
                return Array.Empty<GeneratedPointOfInterest>();
            }

            var entries = config.Catalog != null
                ? config.Catalog.ResolveEntries()
                : DefaultPointOfInterestCatalog.CreateEntries();
            var random = new System.Random(DeterministicRandom.Hash(seed, 14011));
            var count = random.Range(config.MinPointsOfInterest, config.MaxPointsOfInterest);
            var selected = new List<GeneratedPointOfInterest>(count);
            var index = new SpatialHashGrid2D<GeneratedPointOfInterest>(Mathf.Max(32f, config.MinimumDistanceBetweenPoi));
            var sampleCache = new TerrainSampleCache(terrainSampler, 8f);
            var candidates = BuildCandidates(settings, config, entries, settlements, masks, sampleCache, random, seed);
            candidates.Sort((left, right) => right.Score.CompareTo(left.Score));

            for (var i = 0; i < candidates.Count && selected.Count < count; i++)
            {
                var candidate = candidates[i];
                if (index.QueryRadius(candidate.Position, config.MinimumDistanceBetweenPoi + candidate.Radius).Count > 0)
                {
                    continue;
                }

                var poi = CreatePointOfInterest(candidate, seed, selected.Count, random);
                selected.Add(poi);
                index.Insert(Bounds2D.FromCircle(poi.WorldPosition, poi.Radius), poi);
                AddMasks(masks, poi);
            }

            return selected;
        }

        private static List<PointOfInterestCandidate> BuildCandidates(
            ProceduralLocationSettings settings,
            PointOfInterestGenerationConfig config,
            IReadOnlyList<PointOfInterestConfigEntry> entries,
            IReadOnlyList<GeneratedSettlement> settlements,
            WorldGenerationMaskSet masks,
            TerrainSampleCache sampler,
            System.Random random,
            int seed)
        {
            var bounds = settings.GetWorldBounds();
            var min = bounds.min;
            var max = bounds.max;
            var candidates = new List<PointOfInterestCandidate>();
            var attempts = Mathf.Max(config.CandidateAttempts, config.MaxPointsOfInterest * 48);

            for (var attempt = 0; attempt < attempts; attempt++)
            {
                var entry = PickEntry(entries, random);
                if (entry == null || !entry.Enabled || entry.Weight <= 0f)
                {
                    continue;
                }

                var x = random.Range(min.x + 80f, max.x - 80f);
                var z = random.Range(min.z + 80f, max.z - 80f);
                var position = new Vector2(x, z);
                if (!sampler.TrySample(x, z, out var point, out var normal))
                {
                    continue;
                }

                var slope = Vector3.Angle(normal, Vector3.up);
                if (slope > ResolveMaxSlope(entry.Type, config.MaxSiteSlope))
                {
                    continue;
                }

                if (settings.WaterEnabled && point.y <= settings.WaterLevel + ResolveWaterClearance(entry.Type))
                {
                    continue;
                }

                if (masks != null && masks.IsNoSpawn(position))
                {
                    continue;
                }

                var settlementDistance = DistanceToNearestSettlement(position, settlements);
                if (settlementDistance < config.MinimumDistanceFromSettlements && RequiresIsolation(entry.Type))
                {
                    continue;
                }

                var tags = new List<string>(entry.Tags);
                var score = ScoreCandidate(entry, position, slope, point.y, settlementDistance, bounds, seed, tags);
                var radiusRange = entry.RadiusRange;
                candidates.Add(new PointOfInterestCandidate(
                    entry,
                    position,
                    random.Range(radiusRange.x, radiusRange.y),
                    random.Range(entry.DangerRange.x, entry.DangerRange.y),
                    score,
                    tags));
            }

            return candidates;
        }

        private static PointOfInterestConfigEntry PickEntry(IReadOnlyList<PointOfInterestConfigEntry> entries, System.Random random)
        {
            var total = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Enabled)
                {
                    total += entries[i].Weight;
                }
            }

            if (total <= 0f)
            {
                return entries.Count > 0 ? entries[0] : null;
            }

            var pick = random.Range(0f, total);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.Enabled)
                {
                    continue;
                }

                pick -= entry.Weight;
                if (pick <= 0f)
                {
                    return entry;
                }
            }

            return entries[entries.Count - 1];
        }

        private static GeneratedPointOfInterest CreatePointOfInterest(PointOfInterestCandidate candidate, int seed, int index, System.Random random)
        {
            return new GeneratedPointOfInterest(
                $"poi_{index:D2}_{candidate.Entry.Type}",
                GenerateName(candidate.Entry.Type, seed, index),
                candidate.Entry.Type,
                candidate.Position,
                candidate.Radius,
                candidate.DangerLevel,
                null,
                candidate.Tags,
                candidate.Entry.PrefabKey,
                candidate.Entry.Type == PointOfInterestType.BanditCamp || candidate.Entry.Type == PointOfInterestType.MonsterNest
                    ? PointOfInterestState.Occupied
                    : PointOfInterestState.Undiscovered,
                new GeneratedPointOfInterestDiagnostics(
                    seed,
                    candidate.Score,
                    candidate.Tags,
                    BuildQuestHooks(candidate.Entry.Type, random)));
        }

        private static void AddMasks(WorldGenerationMaskSet masks, GeneratedPointOfInterest poi)
        {
            if (masks == null || poi == null)
            {
                return;
            }

            masks.AddZone(new GenerationMaskZone(
                $"{poi.Id}_footprint",
                GenerationZoneKind.PointOfInterestFootprint,
                poi.WorldPosition,
                poi.Radius,
                1f,
                poi.Id));
            masks.AddZone(new GenerationMaskZone(
                $"{poi.Id}_no_spawn",
                GenerationZoneKind.NoSpawn,
                poi.WorldPosition,
                poi.Radius + 6f,
                1f,
                poi.Id));
        }

        private static float ScoreCandidate(
            PointOfInterestConfigEntry entry,
            Vector2 position,
            float slope,
            float height,
            float settlementDistance,
            Bounds bounds,
            int seed,
            List<string> tags)
        {
            var nx = Mathf.InverseLerp(bounds.min.x, bounds.max.x, position.x);
            var nz = Mathf.InverseLerp(bounds.min.z, bounds.max.z, position.y);
            var noise = Mathf.PerlinNoise(nx * 9.7f + seed * 0.00017f, nz * 9.7f + seed * 0.00023f);
            var isolation = Mathf.Clamp01(settlementDistance / Mathf.Max(1f, bounds.extents.x * 0.42f));
            var highGround = Mathf.Clamp01(height / Mathf.Max(1f, bounds.size.y + 220f));
            var rugged = Mathf.InverseLerp(10f, 36f, slope);

            var typeScore = entry.Type switch
            {
                PointOfInterestType.BanditCamp => Mathf.Clamp01(isolation * 0.55f + (1f - Mathf.Abs(isolation - 0.52f) * 1.6f) * 0.45f),
                PointOfInterestType.CaveEntrance => Mathf.Clamp01(rugged * 0.85f + highGround * 0.22f),
                PointOfInterestType.AncientTemple => Mathf.Clamp01(isolation * 0.55f + highGround * 0.38f),
                PointOfInterestType.Shrine => Mathf.Clamp01(highGround * 0.45f + noise * 0.45f),
                PointOfInterestType.ResourceNode => Mathf.Clamp01(rugged * 0.48f + highGround * 0.35f + noise * 0.25f),
                PointOfInterestType.HiddenCache => Mathf.Clamp01(isolation * 0.75f + noise * 0.25f),
                _ => Mathf.Clamp01(noise * 0.55f + isolation * 0.28f + (1f - rugged) * 0.18f)
            };

            if (typeScore > 0.65f)
            {
                tags.Add("good-site");
            }

            return typeScore * 5f + entry.Weight - slope * 0.025f;
        }

        private static float DistanceToNearestSettlement(Vector2 position, IReadOnlyList<GeneratedSettlement> settlements)
        {
            if (settlements == null || settlements.Count == 0)
            {
                return float.MaxValue;
            }

            var best = float.MaxValue;
            for (var i = 0; i < settlements.Count; i++)
            {
                best = Mathf.Min(best, Vector2.Distance(position, settlements[i].WorldPosition) - settlements[i].Radius);
            }

            return best;
        }

        private static bool RequiresIsolation(PointOfInterestType type)
        {
            return type != PointOfInterestType.AbandonedHouse &&
                   type != PointOfInterestType.Shrine &&
                   type != PointOfInterestType.ResourceNode;
        }

        private static float ResolveMaxSlope(PointOfInterestType type, float defaultMaxSlope)
        {
            return type switch
            {
                PointOfInterestType.CaveEntrance => Mathf.Max(defaultMaxSlope, 42f),
                PointOfInterestType.ResourceNode => Mathf.Max(defaultMaxSlope, 38f),
                PointOfInterestType.StoneCircle => Mathf.Min(defaultMaxSlope, 20f),
                PointOfInterestType.Graveyard => Mathf.Min(defaultMaxSlope, 18f),
                _ => defaultMaxSlope
            };
        }

        private static float ResolveWaterClearance(PointOfInterestType type)
        {
            return type == PointOfInterestType.CaveEntrance || type == PointOfInterestType.ResourceNode ? 1.2f : 2.4f;
        }

        private static string GenerateName(PointOfInterestType type, int seed, int index)
        {
            var random = new System.Random(DeterministicRandom.Hash(seed, 5153 + index * 67));
            var prefix = new[] { "Old", "Silent", "Broken", "Moonlit", "Black", "Silver", "Hollow", "Forgotten" };
            var suffix = new[] { "Hollow", "Rise", "Stones", "Vault", "Watch", "Sanctum", "Crossing", "Den" };
            return $"{prefix[random.Next(0, prefix.Length)]} {type} {suffix[random.Next(0, suffix.Length)]}";
        }

        private static IReadOnlyList<string> BuildQuestHooks(PointOfInterestType type, System.Random random)
        {
            var hooks = new List<string> { type.ToString().ToLowerInvariant() };
            if (random.Chance(0.45f))
            {
                hooks.Add("rumor");
            }

            if (type == PointOfInterestType.BanditCamp || type == PointOfInterestType.MonsterNest)
            {
                hooks.Add("bounty");
            }

            if (type == PointOfInterestType.AncientTemple || type == PointOfInterestType.Portal || type == PointOfInterestType.Obelisk)
            {
                hooks.Add("main_story");
            }

            return hooks;
        }

        private sealed class PointOfInterestCandidate
        {
            public PointOfInterestCandidate(
                PointOfInterestConfigEntry entry,
                Vector2 position,
                float radius,
                int dangerLevel,
                float score,
                IReadOnlyList<string> tags)
            {
                Entry = entry;
                Position = position;
                Radius = Mathf.Max(1f, radius);
                DangerLevel = Mathf.Clamp(dangerLevel, 0, 10);
                Score = score;
                Tags = tags ?? Array.Empty<string>();
            }

            public PointOfInterestConfigEntry Entry { get; }
            public Vector2 Position { get; }
            public float Radius { get; }
            public int DangerLevel { get; }
            public float Score { get; }
            public IReadOnlyList<string> Tags { get; }
        }
    }
}
