using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    public static class SpeedscopeExporter
    {
        public static object Build(ProfileTrace trace)
        {
            var scopes = (trace?.Events ?? Array.Empty<ProfileEvent>())
                .Where(profileEvent => profileEvent != null && profileEvent.Kind == "scope")
                .OrderBy(profileEvent => profileEvent.StartMilliseconds)
                .ToArray();

            var frameByName = new Dictionary<string, int>();
            var frames = new List<object>();
            var samples = new List<int[]>();
            var weights = new List<double>();
            var start = scopes.Length == 0 ? 0d : scopes.Min(item => item.StartMilliseconds);
            var end = scopes.Length == 0 ? 0d : scopes.Max(item => item.EndMilliseconds);
            var points = scopes
                .SelectMany(item => new[] { item.StartMilliseconds, item.EndMilliseconds })
                .Distinct()
                .OrderBy(item => item)
                .ToArray();

            for (var i = 0; i < points.Length - 1; i++)
            {
                var left = points[i];
                var right = points[i + 1];
                var weight = right - left;
                if (weight <= 0.0001d)
                {
                    continue;
                }

                var mid = (left + right) * 0.5d;
                var active = scopes
                    .Where(item => item.StartMilliseconds <= mid && item.EndMilliseconds > mid)
                    .OrderBy(item => item.StartMilliseconds)
                    .ThenByDescending(item => item.EndMilliseconds)
                    .Select(item => ResolveFrame(item.Name, frameByName, frames))
                    .ToArray();
                samples.Add(active);
                weights.Add(weight);
            }

            return new Dictionary<string, object>
            {
                ["$schema"] = "https://www.speedscope.app/file-format-schema.json",
                ["exporter"] = "LegendsOfWarAndMagic.DebugTools",
                ["name"] = trace?.Name ?? "Debug trace",
                ["version"] = "0.0.1",
                ["shared"] = new
                {
                    frames
                },
                ["profiles"] = new object[]
                {
                    new
                    {
                        type = "sampled",
                        name = trace?.Name ?? "Debug trace",
                        unit = "milliseconds",
                        startValue = 0d,
                        endValue = Math.Max(0d, end - start),
                        samples,
                        weights
                    }
                },
                ["activeProfileIndex"] = 0
            };
        }

        private static int ResolveFrame(string name, Dictionary<string, int> frameByName, List<object> frames)
        {
            var safeName = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name;
            if (frameByName.TryGetValue(safeName, out var index))
            {
                return index;
            }

            index = frames.Count;
            frameByName[safeName] = index;
            frames.Add(new { name = safeName });
            return index;
        }

    }
}
