using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendsOfWarAndMagic.DebugTools.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityProfiler = UnityEngine.Profiling.Profiler;

namespace LegendsOfWarAndMagic.DebugTools.Core
{
    public sealed class DebugSession : IDisposable
    {
        private const int MaxRecentLogEntries = 250;

        private readonly object gate = new();
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly List<int> activeStack = new();
        private readonly List<object> manifest = new();
        private readonly List<object> recentWarnings = new();
        private readonly List<object> recentErrors = new();
        private readonly DateTime startedUtc;
        private readonly string logsDirectory;
        private readonly string generationDirectory;
        private readonly string runtimeDirectory;
        private readonly string snapshotsDirectory;
        private readonly string debugLogPath;
        private readonly string warningsPath;
        private readonly string errorsPath;
        private readonly DebugSessionConfig config;

        private ProfileTrace activeTrace;
        private ProfileTrace generationTrace;
        private string activeTraceKind;
        private int nextEventId;
        private int snapshotIndex;
        private int runtimeRecordingIndex;
        private RuntimeRecordingState runtimeRecording;
        private bool disposed;

        public DebugSession(DebugSessionConfig config, string baseDirectory)
        {
            this.config = config ?? new DebugSessionConfig { Enabled = true };
            startedUtc = DateTime.UtcNow;
            SessionId = Guid.NewGuid().ToString("N");
            ShortId = SessionId[..8];
            IsEnabled = true;
            var localTime = DateTime.Now;
            RootDirectory = Path.Combine(
                string.IsNullOrWhiteSpace(baseDirectory) ? Application.persistentDataPath : baseDirectory,
                "DebugSessions",
                $"{DebugClock.TimestampForDirectory(localTime)}_session-{ShortId}");
            Writer = new DebugArtifactWriter(RootDirectory);
            Profiler = new DebugProfiler(this);
            Counters = new DebugCounters();

            generationDirectory = Writer.Combine("generation");
            runtimeDirectory = Writer.Combine("runtime");
            snapshotsDirectory = Writer.Combine("snapshots");
            logsDirectory = Writer.Combine("logs");
            debugLogPath = Path.Combine(logsDirectory, "debug.log");
            warningsPath = Path.Combine(logsDirectory, "warnings.jsonl");
            errorsPath = Path.Combine(logsDirectory, "errors.jsonl");
            Writer.CreateDirectory(generationDirectory);
            Writer.CreateDirectory(runtimeDirectory);
            Writer.CreateDirectory(Path.Combine(runtimeDirectory, "recordings"));
            Writer.CreateDirectory(snapshotsDirectory);
            Writer.CreateDirectory(logsDirectory);

            WriteReadme();
            WriteSessionJson();
            WriteLatestPointer();
            Application.logMessageReceived += OnUnityLog;
            Debug.Log($"Debug output: {RootDirectory}");
        }

        public bool IsEnabled { get; }
        public string SessionId { get; }
        public string ShortId { get; }
        public string RootDirectory { get; }
        public string LastSavedPath { get; private set; }
        public bool IsGenerationRecording => activeTraceKind == "generation" && generationTrace != null;
        public bool IsRuntimeRecording => runtimeRecording != null;
        public IDebugProfiler Profiler { get; }
        public DebugCounters Counters { get; }
        public DebugArtifactWriter Writer { get; }
        public int CurrentFrameIndex => Time.frameCount;

        public string NextSnapshotDirectory()
        {
            snapshotIndex++;
            var path = Path.Combine(snapshotsDirectory, $"terrain-snapshot-{snapshotIndex:D4}");
            Writer.CreateDirectory(path);
            return path;
        }

        public void BeginGeneration(string name, object metadata = null)
        {
            if (!IsEnabled)
            {
                return;
            }

            lock (gate)
            {
                if (IsGenerationRecording)
                {
                    return;
                }

                activeStack.Clear();
                nextEventId = 0;
                generationTrace = new ProfileTrace(string.IsNullOrWhiteSpace(name) ? "generation" : name, DateTime.UtcNow);
                activeTrace = generationTrace;
                activeTraceKind = "generation";
                Mark("Generation.Start", metadata);
            }
        }

        public void EndGeneration(object extra = null)
        {
            ProfileTrace traceToWrite;
            lock (gate)
            {
                if (generationTrace == null || generationTrace.EndedUtc != default)
                {
                    return;
                }

                Mark("Generation.End", extra);
                generationTrace.EndedUtc = DateTime.UtcNow;
                traceToWrite = generationTrace;
                if (activeTrace == generationTrace)
                {
                    activeTrace = null;
                    activeTraceKind = null;
                    activeStack.Clear();
                }
            }

            ExportTrace(
                traceToWrite,
                generationDirectory,
                "generation",
                new
                {
                    session = BuildSessionSummary(),
                    config = BuildConfigSnapshot(),
                    counters = Counters.Snapshot(),
                    warnings = recentWarnings,
                    errors = recentErrors,
                    extra
                });
            WriteSessionJson();
        }

        public bool StartRuntimeRecording()
        {
            lock (gate)
            {
                if (runtimeRecording != null || activeTrace != null)
                {
                    return false;
                }

                runtimeRecordingIndex++;
                var directory = Path.Combine(runtimeDirectory, "recordings", $"recording-{runtimeRecordingIndex:D4}");
                Writer.CreateDirectory(directory);
                activeStack.Clear();
                nextEventId = 0;
                runtimeRecording = new RuntimeRecordingState(directory, DateTime.UtcNow, Time.frameCount);
                activeTrace = new ProfileTrace($"runtime-recording-{runtimeRecordingIndex:D4}", runtimeRecording.StartedUtc);
                activeTraceKind = "runtime";
                Mark("RuntimeRecording.Start", new { Time.frameCount, Time.realtimeSinceStartup });
                LastSavedPath = directory;
                Debug.Log($"Debug runtime recording started: {directory}");
                return true;
            }
        }

        public string StopRuntimeRecording()
        {
            RuntimeRecordingState recordingToWrite;
            ProfileTrace traceToWrite;
            lock (gate)
            {
                if (runtimeRecording == null || activeTrace == null)
                {
                    return null;
                }

                Mark("RuntimeRecording.End", new { Time.frameCount, Time.realtimeSinceStartup });
                activeTrace.EndedUtc = DateTime.UtcNow;
                recordingToWrite = runtimeRecording;
                recordingToWrite.EndedUtc = activeTrace.EndedUtc;
                recordingToWrite.EndFrame = Time.frameCount;
                traceToWrite = activeTrace;
                runtimeRecording = null;
                activeTrace = null;
                activeTraceKind = null;
                activeStack.Clear();
            }

            var frameStats = recordingToWrite.BuildFrameStats();
            ExportTrace(
                traceToWrite,
                recordingToWrite.Directory,
                "runtime",
                new
                {
                    recording = new
                    {
                        index = runtimeRecordingIndex,
                        startedUtc = recordingToWrite.StartedUtc,
                        endedUtc = recordingToWrite.EndedUtc,
                        durationSeconds = (recordingToWrite.EndedUtc - recordingToWrite.StartedUtc).TotalSeconds,
                        startFrame = recordingToWrite.StartFrame,
                        endFrame = recordingToWrite.EndFrame,
                        frameCount = recordingToWrite.EndFrame - recordingToWrite.StartFrame + 1
                    },
                    frameTiming = frameStats,
                    warnings = recordingToWrite.Warnings,
                    errors = recordingToWrite.Errors
                });
            LastSavedPath = recordingToWrite.Directory;
            WriteSessionJson();
            Debug.Log($"Debug runtime recording saved: {recordingToWrite.Directory}");
            return recordingToWrite.Directory;
        }

        public int BeginScope(string name, object metadata)
        {
            lock (gate)
            {
                if (activeTrace == null)
                {
                    return 0;
                }

                var id = ++nextEventId;
                var parentId = activeStack.Count > 0 ? activeStack[^1] : 0;
                activeTrace.Add(new ProfileEvent
                {
                    Id = id,
                    ParentId = parentId,
                    Kind = "scope",
                    Name = string.IsNullOrWhiteSpace(name) ? "Unnamed" : name,
                    Metadata = metadata,
                    StartMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                    EndMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                    FrameIndex = Time.frameCount
                });
                activeStack.Add(id);
                return id;
            }
        }

        public void EndScope(int id)
        {
            lock (gate)
            {
                if (activeTrace == null || id <= 0)
                {
                    return;
                }

                var profileEvent = activeTrace.Find(id);
                if (profileEvent != null)
                {
                    profileEvent.EndMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                }

                var stackIndex = activeStack.LastIndexOf(id);
                if (stackIndex >= 0)
                {
                    activeStack.RemoveRange(stackIndex, activeStack.Count - stackIndex);
                }
            }
        }

        public void Mark(string name, object metadata)
        {
            lock (gate)
            {
                if (activeTrace == null)
                {
                    return;
                }

                var now = stopwatch.Elapsed.TotalMilliseconds;
                activeTrace.Add(new ProfileEvent
                {
                    Id = ++nextEventId,
                    ParentId = activeStack.Count > 0 ? activeStack[^1] : 0,
                    Kind = "mark",
                    Name = string.IsNullOrWhiteSpace(name) ? "Mark" : name,
                    Metadata = metadata,
                    StartMilliseconds = now,
                    EndMilliseconds = now,
                    FrameIndex = Time.frameCount
                });
            }
        }

        public void RecordFrame(float deltaTimeSeconds)
        {
            lock (gate)
            {
                runtimeRecording?.FrameTimesMilliseconds.Add(Mathf.Max(0f, deltaTimeSeconds) * 1000f);
            }
        }

        public void SetLastSavedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            LastSavedPath = path;
            RegisterArtifact(path, "manual");
            WriteSessionJson();
        }

        public IReadOnlyList<object> GetRecentWarnings()
        {
            lock (gate)
            {
                return recentWarnings.ToArray();
            }
        }

        public IReadOnlyList<object> GetRecentErrors()
        {
            lock (gate)
            {
                return recentErrors.ToArray();
            }
        }

        public object BuildSessionMetadata()
        {
            return new
            {
                sessionId = SessionId,
                shortId = ShortId,
                rootDirectory = RootDirectory,
                startedUtc,
                lastSavedPath = LastSavedPath,
                source = config.Source,
                label = config.Label,
                environment = BuildEnvironmentSnapshot(),
                config = BuildConfigSnapshot(),
                counters = Counters.Snapshot()
            };
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Application.logMessageReceived -= OnUnityLog;
            if (runtimeRecording != null)
            {
                StopRuntimeRecording();
            }

            if (generationTrace != null && generationTrace.EndedUtc == default)
            {
                EndGeneration(new { reason = "session disposed" });
            }

            WriteSessionJson();
        }

        private void ExportTrace(ProfileTrace trace, string directory, string filePrefix, object summaryExtra)
        {
            if (trace == null || string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            Writer.CreateDirectory(directory);
            var rawPath = Path.Combine(directory, $"{filePrefix}-trace.json");
            var callTreePath = Path.Combine(directory, $"{filePrefix}-calltree.json");
            var speedscopePath = Path.Combine(directory, $"{filePrefix}-speedscope.json");
            var summaryPath = Path.Combine(directory, $"{filePrefix}-summary.json");
            var htmlPath = Path.Combine(directory, $"{filePrefix}-flamegraph.html");
            var summaryHtmlPath = Path.Combine(directory, $"{filePrefix}-summary.html");
            var callTreeHtmlPath = Path.Combine(directory, $"{filePrefix}-calltree.html");
            var traceHtmlPath = Path.Combine(directory, $"{filePrefix}-trace.html");

            Writer.WriteJson(rawPath, new
            {
                session = BuildSessionSummary(),
                trace = new
                {
                    trace.Name,
                    trace.StartedUtc,
                    trace.EndedUtc,
                    events = trace.Events
                }
            });
            Writer.WriteJson(callTreePath, CallTreeBuilder.Build(trace));
            Writer.WriteJson(speedscopePath, SpeedscopeExporter.Build(trace), false);
            Writer.WriteJson(summaryPath, ProfileSummaryBuilder.Build(trace, summaryExtra));
            Writer.WriteText(htmlPath, FlameGraphExporter.BuildHtml(trace, Path.GetFileName(speedscopePath)));
            Writer.WriteText(summaryHtmlPath, ProfileHtmlReportExporter.BuildSummaryHtml(trace, Path.GetFileName(speedscopePath)));
            Writer.WriteText(callTreeHtmlPath, ProfileHtmlReportExporter.BuildCallTreeHtml(trace, Path.GetFileName(speedscopePath)));
            Writer.WriteText(traceHtmlPath, ProfileHtmlReportExporter.BuildTraceHtml(trace, Path.GetFileName(speedscopePath)));
            RegisterArtifact(rawPath, "trace");
            RegisterArtifact(callTreePath, "calltree");
            RegisterArtifact(speedscopePath, "speedscope");
            RegisterArtifact(summaryPath, "summary");
            RegisterArtifact(htmlPath, "flamegraph-html");
            RegisterArtifact(summaryHtmlPath, "summary-html");
            RegisterArtifact(callTreeHtmlPath, "calltree-html");
            RegisterArtifact(traceHtmlPath, "trace-html");
            LastSavedPath = directory;
            Writer.WriteJson(Path.Combine(RootDirectory, "manifest.json"), new { artifacts = manifest });
        }

        private void RegisterArtifact(string path, string kind)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            manifest.Add(new
            {
                kind,
                path,
                relativePath = path.StartsWith(RootDirectory, StringComparison.OrdinalIgnoreCase)
                    ? path.Substring(RootDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : path,
                timestampUtc = DateTime.UtcNow
            });
        }

        private void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            var entry = new
            {
                timestampUtc = DateTime.UtcNow,
                type = type.ToString(),
                message = condition,
                stackTrace,
                frame = Time.frameCount,
                scene = SceneManager.GetActiveScene().name
            };

            Writer.AppendLine(debugLogPath, $"{DebugClock.ToIsoUtc(DateTime.UtcNow)} [{type}] {condition}");
            if (!string.IsNullOrWhiteSpace(stackTrace) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            {
                Writer.AppendLine(debugLogPath, stackTrace);
            }

            lock (gate)
            {
                if (type == LogType.Warning)
                {
                    recentWarnings.Add(entry);
                    TrimLogList(recentWarnings);
                    runtimeRecording?.Warnings.Add(entry);
                    Writer.AppendLine(warningsPath, DebugJson.ToJson(entry, false));
                }
                else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    recentErrors.Add(entry);
                    TrimLogList(recentErrors);
                    runtimeRecording?.Errors.Add(entry);
                    Writer.AppendLine(errorsPath, DebugJson.ToJson(entry, false));
                }
            }
        }

        private static void TrimLogList(List<object> entries)
        {
            while (entries.Count > MaxRecentLogEntries)
            {
                entries.RemoveAt(0);
            }
        }

        private void WriteReadme()
        {
            var text =
                "# Debug Session\n\n" +
                $"Session: `{ShortId}`\n\n" +
                $"Output: `{RootDirectory}`\n\n" +
                "## Files\n\n" +
                "- `session.json`: session metadata and environment.\n" +
                "- `generation/generation-*.json`: generation trace, summary, call tree, and Speedscope profile.\n" +
                "- `runtime/recordings/recording-####/`: F2 runtime recordings.\n" +
                "- `snapshots/terrain-snapshot-####/`: F5 terrain snapshots with JSON and OBJ exports.\n" +
                "- `logs/debug.log`, `logs/warnings.jsonl`, `logs/errors.jsonl`: Unity log capture.\n\n" +
                "Open `*-speedscope.json` at https://www.speedscope.app/ for an interactive flame graph.\n";
            Writer.WriteText(Path.Combine(RootDirectory, "README.md"), text);
        }

        private void WriteSessionJson()
        {
            Writer.WriteJson(Path.Combine(RootDirectory, "session.json"), BuildSessionMetadata());
        }

        private void WriteLatestPointer()
        {
            var parent = Directory.GetParent(RootDirectory)?.FullName;
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Writer.WriteText(Path.Combine(parent, "latest.txt"), RootDirectory);
            }
        }

        private object BuildSessionSummary()
        {
            return new
            {
                sessionId = SessionId,
                shortId = ShortId,
                rootDirectory = RootDirectory,
                source = config.Source,
                label = config.Label
            };
        }

        private object BuildConfigSnapshot()
        {
            return new
            {
                config.Source,
                config.Label,
                config.Seed,
                config.Summary,
                settings = config.Settings
            };
        }

        private static object BuildEnvironmentSnapshot()
        {
            var graphicsType = SystemInfo.graphicsDeviceType;
            return new
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                processorCount = SystemInfo.processorCount,
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceType = graphicsType.ToString(),
                graphicsMemoryMb = SystemInfo.graphicsMemorySize,
                graphicsShaderLevel = SystemInfo.graphicsShaderLevel,
                screen = new
                {
                    Screen.width,
                    Screen.height,
                    refreshRate = Screen.currentResolution.refreshRateRatio.value
                },
                qualityLevel = QualitySettings.GetQualityLevel(),
                qualityName = QualitySettings.names.Length > QualitySettings.GetQualityLevel() ? QualitySettings.names[QualitySettings.GetQualityLevel()] : string.Empty,
                targetFrameRate = Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                scene = SceneManager.GetActiveScene().name,
                isEditor = Application.isEditor,
                isDebugBuild = Debug.isDebugBuild,
                renderPipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in",
                allocatedMemoryBytes = UnityProfiler.GetTotalAllocatedMemoryLong(),
                reservedMemoryBytes = UnityProfiler.GetTotalReservedMemoryLong()
            };
        }

        private sealed class RuntimeRecordingState
        {
            public RuntimeRecordingState(string directory, DateTime startedUtc, int startFrame)
            {
                Directory = directory;
                StartedUtc = startedUtc;
                StartFrame = startFrame;
            }

            public string Directory { get; }
            public DateTime StartedUtc { get; }
            public DateTime EndedUtc { get; set; }
            public int StartFrame { get; }
            public int EndFrame { get; set; }
            public List<float> FrameTimesMilliseconds { get; } = new();
            public List<object> Warnings { get; } = new();
            public List<object> Errors { get; } = new();

            public object BuildFrameStats()
            {
                if (FrameTimesMilliseconds.Count == 0)
                {
                    return new
                    {
                        frameSamples = 0,
                        averageMs = 0f,
                        minMs = 0f,
                        maxMs = 0f
                    };
                }

                return new
                {
                    frameSamples = FrameTimesMilliseconds.Count,
                    averageMs = FrameTimesMilliseconds.Average(),
                    minMs = FrameTimesMilliseconds.Min(),
                    maxMs = FrameTimesMilliseconds.Max()
                };
            }
        }
    }
}
