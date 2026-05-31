using LegendsOfWarAndMagic.DebugTools.Runtime;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public static class DebugSessionManager
    {
        private static GameObject runtimeHost;

        public static DebugSession Current { get; private set; }
        public static IDebugProfiler Profiler => Current?.Profiler ?? NullDebugProfiler.Instance;
        public static bool IsEnabled => Current != null && Current.IsEnabled;

        public static DebugSession StartSession(DebugSessionConfig config)
        {
            if (config == null || !config.Enabled)
            {
                EndSession();
                return null;
            }

            EndSession();
            Current = new DebugSession(config, Application.persistentDataPath);
            EnsureRuntimeHost();
            return Current;
        }

        public static DebugSession StartSessionIfNeeded(DebugSessionConfig config)
        {
            if (Current != null && Current.IsEnabled)
            {
                return Current;
            }

            return StartSession(config);
        }

        public static void EndSession()
        {
            if (Current != null)
            {
                Current.Dispose();
                Current = null;
            }

            if (runtimeHost != null)
            {
                Object.Destroy(runtimeHost);
                runtimeHost = null;
            }
        }

        public static void BeginGeneration(string name, object metadata = null)
        {
            Current?.BeginGeneration(name, metadata);
        }

        public static void EndGeneration(object extra = null)
        {
            Current?.EndGeneration(extra);
        }

        public static bool ToggleRuntimeRecording()
        {
            if (Current == null)
            {
                return false;
            }

            if (Current.IsRuntimeRecording)
            {
                Current.StopRuntimeRecording();
                return false;
            }

            Current.StartRuntimeRecording();
            return Current.IsRuntimeRecording;
        }

        public static void SetLastSavedPath(string path)
        {
            Current?.SetLastSavedPath(path);
        }

        private static void EnsureRuntimeHost()
        {
            if (runtimeHost != null)
            {
                return;
            }

            runtimeHost = new GameObject("Debug Tools Runtime");
            Object.DontDestroyOnLoad(runtimeHost);
            runtimeHost.AddComponent<DebugOverlay>();
            runtimeHost.AddComponent<DebugHotkeys>();
            runtimeHost.AddComponent<RuntimeProfilerRecorder>();
        }
    }
}
