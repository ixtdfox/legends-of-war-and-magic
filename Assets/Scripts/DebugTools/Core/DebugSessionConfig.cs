using System;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    [Serializable]
    public sealed class DebugSessionConfig
    {
        public bool Enabled { get; set; }
        public string Source { get; set; }
        public string Label { get; set; }
        public int Seed { get; set; }
        public string Summary { get; set; }
        public object Settings { get; set; }

        public static DebugSessionConfig Disabled => new DebugSessionConfig { Enabled = false };
    }
}
