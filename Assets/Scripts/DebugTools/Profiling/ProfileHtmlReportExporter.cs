using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    public static class ProfileHtmlReportExporter
    {
        private const int MaxTraceRows = 5000;

        public static string BuildSummaryHtml(ProfileTrace trace, string speedscopeFileName)
        {
            var scopes = Scopes(trace)
                .OrderByDescending(item => item.DurationMilliseconds)
                .ToArray();
            var totals = scopes
                .GroupBy(item => item.Name)
                .Select(group => new
                {
                    Name = group.Key,
                    Count = group.Count(),
                    Total = group.Sum(item => item.DurationMilliseconds),
                    Average = group.Average(item => item.DurationMilliseconds),
                    Max = group.Max(item => item.DurationMilliseconds)
                })
                .OrderByDescending(item => item.Total)
                .Take(80)
                .ToArray();

            var builder = BeginDocument($"{trace?.Name ?? "Trace"} summary");
            builder.Append("<h1>").Append(Escape(trace?.Name ?? "Trace")).AppendLine(" summary</h1>");
            AppendNav(builder, speedscopeFileName);
            builder.AppendLine("<section class=\"cards\">");
            AppendCard(builder, "Duration", FormatMs(scopes.Length == 0 ? 0d : scopes.Max(item => item.EndMilliseconds) - scopes.Min(item => item.StartMilliseconds)));
            AppendCard(builder, "Scopes", scopes.Length.ToString());
            AppendCard(builder, "Marks", (trace?.Events ?? Array.Empty<ProfileEvent>()).Count(item => item.Kind == "mark").ToString());
            AppendCard(builder, "Started UTC", Escape(trace?.StartedUtc.ToString("O") ?? string.Empty));
            builder.AppendLine("</section>");

            builder.AppendLine("<h2>Top slowest scopes</h2>");
            AppendScopeTable(builder, scopes.Take(80), includeMetadata: true);

            builder.AppendLine("<h2>Totals by name</h2>");
            builder.AppendLine("<table><thead><tr><th>Name</th><th>Count</th><th>Total</th><th>Average</th><th>Max</th></tr></thead><tbody>");
            foreach (var item in totals)
            {
                builder.Append("<tr><td>").Append(Escape(item.Name)).Append("</td><td>")
                    .Append(item.Count).Append("</td><td>")
                    .Append(FormatMs(item.Total)).Append("</td><td>")
                    .Append(FormatMs(item.Average)).Append("</td><td>")
                    .Append(FormatMs(item.Max)).AppendLine("</td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            return EndDocument(builder);
        }

        public static string BuildCallTreeHtml(ProfileTrace trace, string speedscopeFileName)
        {
            var roots = BuildTree(trace);
            var maxDuration = Math.Max(1d, roots.Count == 0 ? 1d : roots.Max(item => item.DurationMilliseconds));
            var builder = BeginDocument($"{trace?.Name ?? "Trace"} call tree");
            builder.Append("<h1>").Append(Escape(trace?.Name ?? "Trace")).AppendLine(" call tree</h1>");
            AppendNav(builder, speedscopeFileName);
            builder.AppendLine("<p>Expand nodes to inspect nested scopes. Bar width is relative to the slowest root scope.</p>");
            builder.AppendLine("<div class=\"tree\">");
            foreach (var root in roots)
            {
                AppendTreeNode(builder, root, maxDuration, 0);
            }

            builder.AppendLine("</div>");
            return EndDocument(builder);
        }

        public static string BuildTraceHtml(ProfileTrace trace, string speedscopeFileName)
        {
            var events = (trace?.Events ?? Array.Empty<ProfileEvent>())
                .OrderBy(item => item.StartMilliseconds)
                .ThenBy(item => item.Id)
                .Take(MaxTraceRows)
                .ToArray();
            var builder = BeginDocument($"{trace?.Name ?? "Trace"} raw trace");
            builder.Append("<h1>").Append(Escape(trace?.Name ?? "Trace")).AppendLine(" raw trace</h1>");
            AppendNav(builder, speedscopeFileName);
            builder.Append("<p>Showing ").Append(events.Length).Append(" events");
            if ((trace?.Events.Count ?? 0) > MaxTraceRows)
            {
                builder.Append(" of ").Append(trace.Events.Count).Append(" total");
            }

            builder.AppendLine(".</p>");
            builder.AppendLine("<input id=\"filter\" placeholder=\"Filter by name/type/frame\" oninput=\"filterRows()\">");
            builder.AppendLine("<table id=\"trace\"><thead><tr><th>Id</th><th>Parent</th><th>Kind</th><th>Name</th><th>Start</th><th>Duration</th><th>Frame</th><th>Thread</th><th>Metadata</th></tr></thead><tbody>");
            foreach (var item in events)
            {
                builder.Append("<tr><td>").Append(item.Id).Append("</td><td>")
                    .Append(item.ParentId).Append("</td><td>")
                    .Append(Escape(item.Kind)).Append("</td><td>")
                    .Append(Escape(item.Name)).Append("</td><td>")
                    .Append(FormatMs(item.StartMilliseconds)).Append("</td><td>")
                    .Append(FormatMs(item.DurationMilliseconds)).Append("</td><td>")
                    .Append(item.FrameIndex).Append("</td><td>")
                    .Append(item.ThreadId).Append("</td><td><code>")
                    .Append(Escape(CompactMetadata(item.Metadata))).AppendLine("</code></td></tr>");
            }

            builder.AppendLine("</tbody></table>");
            builder.AppendLine("<script>function filterRows(){const q=document.getElementById('filter').value.toLowerCase();for(const r of document.querySelectorAll('#trace tbody tr')){r.style.display=r.innerText.toLowerCase().includes(q)?'':'none';}}</script>");
            return EndDocument(builder);
        }

        private static StringBuilder BeginDocument(string title)
        {
            var builder = new StringBuilder(32 * 1024);
            builder.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
                .Append(Escape(title))
                .AppendLine("</title>");
            builder.AppendLine("<style>");
            builder.AppendLine("body{font-family:system-ui,Arial,sans-serif;margin:24px;background:#111;color:#eee}a{color:#8bd3ff}table{border-collapse:collapse;width:100%;font-size:13px}th,td{border-bottom:1px solid #333;padding:6px 8px;text-align:left;vertical-align:top}th{position:sticky;top:0;background:#1b1b1b}code{white-space:pre-wrap}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:10px;margin:18px 0}.card{background:#1b1b1b;border:1px solid #333;padding:12px}.label{color:#aaa;font-size:12px}.value{font-size:20px;font-weight:700}.bar{height:9px;background:#2d7dd2;margin-top:4px}.tree details{margin-left:18px}.node{padding:4px 0}.meta{color:#aaa;font-size:12px}input{box-sizing:border-box;width:100%;margin:12px 0;padding:8px;background:#1b1b1b;border:1px solid #555;color:#eee}");
            builder.AppendLine("</style></head><body>");
            return builder;
        }

        private static string EndDocument(StringBuilder builder)
        {
            builder.AppendLine("</body></html>");
            return builder.ToString();
        }

        private static void AppendNav(StringBuilder builder, string speedscopeFileName)
        {
            builder.Append("<p><a href=\"")
                .Append(Escape(speedscopeFileName))
                .Append("\">")
                .Append(Escape(speedscopeFileName))
                .Append("</a> can be opened at <a href=\"https://www.speedscope.app/\">speedscope.app</a>.</p>");
        }

        private static void AppendCard(StringBuilder builder, string label, string value)
        {
            builder.Append("<div class=\"card\"><div class=\"label\">")
                .Append(Escape(label))
                .Append("</div><div class=\"value\">")
                .Append(value)
                .AppendLine("</div></div>");
        }

        private static void AppendScopeTable(StringBuilder builder, IEnumerable<ProfileEvent> scopes, bool includeMetadata)
        {
            builder.AppendLine("<table><thead><tr><th>Name</th><th>Duration</th><th>Start</th><th>Frame</th><th>Thread</th>");
            if (includeMetadata)
            {
                builder.Append("<th>Metadata</th>");
            }

            builder.AppendLine("</tr></thead><tbody>");
            foreach (var item in scopes)
            {
                builder.Append("<tr><td>").Append(Escape(item.Name)).Append("</td><td>")
                    .Append(FormatMs(item.DurationMilliseconds)).Append("</td><td>")
                    .Append(FormatMs(item.StartMilliseconds)).Append("</td><td>")
                    .Append(item.FrameIndex).Append("</td><td>")
                    .Append(item.ThreadId).Append("</td>");
                if (includeMetadata)
                {
                    builder.Append("<td><code>").Append(Escape(CompactMetadata(item.Metadata))).Append("</code></td>");
                }

                builder.AppendLine("</tr>");
            }

            builder.AppendLine("</tbody></table>");
        }

        private static void AppendTreeNode(StringBuilder builder, HtmlNode node, double maxDuration, int depth)
        {
            var width = Math.Max(1d, node.DurationMilliseconds / maxDuration * 100d);
            builder.Append("<details open><summary class=\"node\"><strong>")
                .Append(Escape(node.Name))
                .Append("</strong> <span class=\"meta\">")
                .Append(FormatMs(node.DurationMilliseconds))
                .Append(" self ")
                .Append(FormatMs(node.SelfMilliseconds))
                .Append(" frame ")
                .Append(node.FrameIndex)
                .Append("</span><div class=\"bar\" style=\"width:")
                .Append(width.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("%\"></div></summary>");
            if (node.Metadata != null)
            {
                builder.Append("<div class=\"meta\"><code>").Append(Escape(CompactMetadata(node.Metadata))).AppendLine("</code></div>");
            }

            foreach (var child in node.Children)
            {
                AppendTreeNode(builder, child, maxDuration, depth + 1);
            }

            builder.AppendLine("</details>");
        }

        private static List<HtmlNode> BuildTree(ProfileTrace trace)
        {
            var nodesById = new Dictionary<int, HtmlNode>();
            var roots = new List<HtmlNode>();
            foreach (var item in Scopes(trace))
            {
                nodesById[item.Id] = new HtmlNode(item);
            }

            foreach (var pair in nodesById)
            {
                var node = pair.Value;
                if (node.ParentId > 0 && nodesById.TryGetValue(node.ParentId, out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            roots.Sort((left, right) => left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            foreach (var root in roots)
            {
                SortAndCompute(root);
            }

            return roots;
        }

        private static void SortAndCompute(HtmlNode node)
        {
            node.Children.Sort((left, right) => left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            var childDuration = 0d;
            foreach (var child in node.Children)
            {
                SortAndCompute(child);
                childDuration += child.DurationMilliseconds;
            }

            node.SelfMilliseconds = Math.Max(0d, node.DurationMilliseconds - childDuration);
        }

        private static IEnumerable<ProfileEvent> Scopes(ProfileTrace trace)
        {
            return (trace?.Events ?? Array.Empty<ProfileEvent>())
                .Where(item => item != null && item.Kind == "scope");
        }

        private static string CompactMetadata(object metadata)
        {
            if (metadata == null)
            {
                return string.Empty;
            }

            var value = metadata.ToString();
            return value != null && value.Length > 300 ? value.Substring(0, 300) + "..." : value;
        }

        private static string FormatMs(double value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " ms";
        }

        private static string Escape(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private sealed class HtmlNode
        {
            public HtmlNode(ProfileEvent profileEvent)
            {
                Id = profileEvent.Id;
                ParentId = profileEvent.ParentId;
                Name = profileEvent.Name;
                Metadata = profileEvent.Metadata;
                StartMilliseconds = profileEvent.StartMilliseconds;
                DurationMilliseconds = profileEvent.DurationMilliseconds;
                FrameIndex = profileEvent.FrameIndex;
            }

            public int Id { get; }
            public int ParentId { get; }
            public string Name { get; }
            public object Metadata { get; }
            public double StartMilliseconds { get; }
            public double DurationMilliseconds { get; }
            public double SelfMilliseconds { get; set; }
            public int FrameIndex { get; }
            public List<HtmlNode> Children { get; } = new();
        }
    }
}
