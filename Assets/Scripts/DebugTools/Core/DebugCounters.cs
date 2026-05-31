using System.Collections.Generic;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class DebugCounters
    {
        private readonly Dictionary<string, double> values = new();

        public void Set(string name, double value)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                values[name] = value;
            }
        }

        public void Add(string name, double value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            values.TryGetValue(name, out var current);
            values[name] = current + value;
        }

        public IReadOnlyDictionary<string, double> Snapshot()
        {
            return new Dictionary<string, double>(values);
        }
    }
}
