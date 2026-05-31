using System;
using System.Threading;

namespace LegendsOfWarAndMagic.DebugTools.Profiling
{
    [Serializable]
    public sealed class ProfileEvent
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public object Metadata { get; set; }
        public double StartMilliseconds { get; set; }
        public double EndMilliseconds { get; set; }
        public double DurationMilliseconds => Math.Max(0d, EndMilliseconds - StartMilliseconds);
        public int FrameIndex { get; set; }
        public int ThreadId { get; set; } = Thread.CurrentThread.ManagedThreadId;
    }
}
