using System;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public interface IDebugProfiler
    {
        bool IsEnabled { get; }
        IDisposable Scope(string name);
        IDisposable Scope(string name, object metadata);
        void Mark(string name);
        void Mark(string name, object metadata);
    }
}
