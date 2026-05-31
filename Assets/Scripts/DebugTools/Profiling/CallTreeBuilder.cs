using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    public static class CallTreeBuilder
    {
        public static object Build(ProfileTrace trace)
        {
            var nodesById = new Dictionary<int, CallTreeNode>();
            var roots = new List<CallTreeNode>();
            var events = trace?.Events ?? Array.Empty<ProfileEvent>();

            for (var i = 0; i < events.Count; i++)
            {
                var profileEvent = events[i];
                if (profileEvent == null || profileEvent.Kind != "scope")
                {
                    continue;
                }

                var node = new CallTreeNode(profileEvent);
                nodesById[profileEvent.Id] = node;
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

            return new
            {
                trace = trace?.Name,
                startedUtc = trace?.StartedUtc,
                endedUtc = trace?.EndedUtc,
                roots
            };
        }

        private static void SortAndCompute(CallTreeNode node)
        {
            node.Children.Sort((left, right) => left.StartMilliseconds.CompareTo(right.StartMilliseconds));
            var childDuration = 0d;
            for (var i = 0; i < node.Children.Count; i++)
            {
                SortAndCompute(node.Children[i]);
                childDuration += node.Children[i].DurationMilliseconds;
            }

            node.SelfMilliseconds = Math.Max(0d, node.DurationMilliseconds - childDuration);
        }

        public sealed class CallTreeNode
        {
            public CallTreeNode(ProfileEvent profileEvent)
            {
                Id = profileEvent.Id;
                ParentId = profileEvent.ParentId;
                Name = profileEvent.Name;
                Metadata = profileEvent.Metadata;
                StartMilliseconds = profileEvent.StartMilliseconds;
                EndMilliseconds = profileEvent.EndMilliseconds;
                DurationMilliseconds = profileEvent.DurationMilliseconds;
                FrameIndex = profileEvent.FrameIndex;
                ThreadId = profileEvent.ThreadId;
            }

            public int Id { get; }
            public int ParentId { get; }
            public string Name { get; }
            public object Metadata { get; }
            public double StartMilliseconds { get; }
            public double EndMilliseconds { get; }
            public double DurationMilliseconds { get; }
            public double SelfMilliseconds { get; set; }
            public int FrameIndex { get; }
            public int ThreadId { get; }
            public List<CallTreeNode> Children { get; } = new();
        }
    }
}
