using System;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class NullDebugProfiler : IDebugProfiler
    {
        public static readonly NullDebugProfiler Instance = new();

        private NullDebugProfiler()
        {
        }

        public bool IsEnabled => false;

        public IDisposable Scope(string name)
        {
            return NoopDisposable.Instance;
        }

        public IDisposable Scope(string name, object metadata)
        {
            return NoopDisposable.Instance;
        }

        public void Mark(string name)
        {
        }

        public void Mark(string name, object metadata)
        {
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static readonly NoopDisposable Instance = new();

            private NoopDisposable()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
