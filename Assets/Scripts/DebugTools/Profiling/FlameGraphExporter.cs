using System;
using System.Linq;
using System.Net;
using System.Text;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    public static class FlameGraphExporter
    {
        public static string BuildHtml(ProfileTrace trace, string speedscopeFileName)
        {
            var scopes = (trace?.Events ?? Array.Empty<ProfileEvent>())
                .Where(profileEvent => profileEvent != null && profileEvent.Kind == "scope")
                .OrderByDescending(profileEvent => profileEvent.DurationMilliseconds)
                .Take(80)
                .ToArray();

            var max = scopes.Length == 0 ? 1d : Math.Max(1d, scopes[0].DurationMilliseconds);
            var builder = new StringBuilder();
            builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Debug Flame Graph</title>");
            builder.AppendLine("<style>body{font-family:system-ui,Arial,sans-serif;margin:24px;background:#111;color:#eee}a{color:#8bd3ff}.bar{height:22px;background:#2d7dd2;margin:4px 0}.row{margin:8px 0}.name{font-size:13px}</style>");
            builder.AppendLine("</head><body>");
            builder.Append("<h1>").Append(WebUtility.HtmlEncode(trace?.Name ?? "Debug trace")).AppendLine("</h1>");
            builder.Append("<p>Open <code>").Append(WebUtility.HtmlEncode(speedscopeFileName)).AppendLine("</code> at <a href=\"https://www.speedscope.app/\">speedscope.app</a> for the interactive flame graph.</p>");
            builder.AppendLine("<h2>Slowest scopes</h2>");
            for (var i = 0; i < scopes.Length; i++)
            {
                var scope = scopes[i];
                var width = Math.Max(1d, scope.DurationMilliseconds / max * 100d);
                builder.Append("<div class=\"row\"><div class=\"name\">")
                    .Append(WebUtility.HtmlEncode(scope.Name))
                    .Append(" - ")
                    .Append(scope.DurationMilliseconds.ToString("0.###"))
                    .Append(" ms</div><div class=\"bar\" style=\"width:")
                    .Append(width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                    .AppendLine("%\"></div></div>");
            }

            builder.AppendLine("</body></html>");
            return builder.ToString();
        }
    }
}
