using System;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class DebugProfiler : IDebugProfiler
    {
        private readonly DebugSession session;

        public DebugProfiler(DebugSession session)
        {
            this.session = session;
        }

        public bool IsEnabled => session != null && session.IsEnabled;

        public IDisposable Scope(string name)
        {
            return Scope(name, null);
        }

        public IDisposable Scope(string name, object metadata)
        {
            if (!IsEnabled)
            {
                return NullDebugProfiler.Instance.Scope(name, metadata);
            }

            var id = session.BeginScope(name, metadata);
            return id > 0 ? new DebugScope(session, id) : NullDebugProfiler.Instance.Scope(name, metadata);
        }

        public void Mark(string name)
        {
            Mark(name, null);
        }

        public void Mark(string name, object metadata)
        {
            if (IsEnabled)
            {
                session.Mark(name, metadata);
            }
        }
    }
}
