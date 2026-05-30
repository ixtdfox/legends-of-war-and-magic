using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model
{
    public sealed class GeneratedPointOfInterestDiagnostics
    {
        public GeneratedPointOfInterestDiagnostics(int seed, float siteScore, IReadOnlyList<string> debugTags, IReadOnlyList<string> questHooks)
        {
            Seed = seed;
            SiteScore = siteScore;
            DebugTags = debugTags ?? Array.Empty<string>();
            QuestHooks = questHooks ?? Array.Empty<string>();
        }

        public int Seed { get; }
        public float SiteScore { get; }
        public IReadOnlyList<string> DebugTags { get; }
        public IReadOnlyList<string> QuestHooks { get; }
    }

    public sealed class GeneratedPointOfInterest
    {
        public GeneratedPointOfInterest(
            string id,
            string name,
            PointOfInterestType type,
            Vector2 worldPosition,
            float radius,
            int dangerLevel,
            FactionId? factionId,
            IReadOnlyList<string> tags,
            string prefabKey,
            PointOfInterestState state,
            GeneratedPointOfInterestDiagnostics diagnostics)
        {
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            Name = string.IsNullOrWhiteSpace(name) ? type.ToString() : name;
            Type = type;
            WorldPosition = worldPosition;
            Radius = Mathf.Max(1f, radius);
            DangerLevel = Mathf.Clamp(dangerLevel, 0, 10);
            FactionId = factionId;
            Tags = tags ?? Array.Empty<string>();
            PrefabKey = prefabKey ?? string.Empty;
            State = state;
            Diagnostics = diagnostics ?? new GeneratedPointOfInterestDiagnostics(0, 0f, Array.Empty<string>(), Array.Empty<string>());
        }

        public string Id { get; }
        public string Name { get; }
        public PointOfInterestType Type { get; }
        public Vector2 WorldPosition { get; }
        public float Radius { get; }
        public int DangerLevel { get; }
        public FactionId? FactionId { get; }
        public IReadOnlyList<string> Tags { get; }
        public string PrefabKey { get; }
        public PointOfInterestState State { get; }
        public GeneratedPointOfInterestDiagnostics Diagnostics { get; }
    }
}
