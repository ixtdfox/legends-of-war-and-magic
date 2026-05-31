using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    public static class ProfileSummaryBuilder
    {
        public static object Build(ProfileTrace trace, object extra = null)
        {
            var scopes = (trace?.Events ?? Array.Empty<ProfileEvent>())
                .Where(profileEvent => profileEvent != null && profileEvent.Kind == "scope")
                .OrderByDescending(profileEvent => profileEvent.DurationMilliseconds)
                .ToArray();

            var byName = scopes
                .GroupBy(profileEvent => profileEvent.Name)
                .Select(group => new
                {
                    name = group.Key,
                    count = group.Count(),
                    totalMs = group.Sum(item => item.DurationMilliseconds),
                    averageMs = group.Average(item => item.DurationMilliseconds),
                    maxMs = group.Max(item => item.DurationMilliseconds)
                })
                .OrderByDescending(item => item.totalMs)
                .Take(25)
                .ToArray();

            return new
            {
                trace = trace?.Name,
                startedUtc = trace?.StartedUtc,
                endedUtc = trace?.EndedUtc,
                durationMs = scopes.Length == 0 ? 0d : scopes.Max(item => item.EndMilliseconds) - scopes.Min(item => item.StartMilliseconds),
                scopeCount = scopes.Length,
                markCount = (trace?.Events ?? Array.Empty<ProfileEvent>()).Count(item => item != null && item.Kind == "mark"),
                topSlowestScopes = scopes.Take(25).Select(item => new
                {
                    item.Name,
                    item.DurationMilliseconds,
                    item.StartMilliseconds,
                    item.EndMilliseconds,
                    item.FrameIndex,
                    item.Metadata
                }).ToArray(),
                totalsByName = byName,
                extra
            };
        }
    }
}
