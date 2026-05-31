using System;
using System.Collections.Generic;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    [Serializable]
    public sealed class ProfileTrace
    {
        private readonly List<ProfileEvent> events = new();

        public ProfileTrace(string name, DateTime startedUtc)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "debug-trace" : name;
            StartedUtc = startedUtc;
        }

        public string Name { get; }
        public DateTime StartedUtc { get; }
        public DateTime EndedUtc { get; set; }
        public IReadOnlyList<ProfileEvent> Events => events;

        public void Add(ProfileEvent profileEvent)
        {
            if (profileEvent != null)
            {
                events.Add(profileEvent);
            }
        }

        public ProfileEvent Find(int id)
        {
            for (var i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Id == id)
                {
                    return events[i];
                }
            }

            return null;
        }
    }
}
