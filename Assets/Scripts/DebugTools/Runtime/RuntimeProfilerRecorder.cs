using LegendsOfWarAndMagic.DebugTools.Core;
using UnityEngine;

namespace LegendsOfWarAndMagic.DebugTools.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RuntimeProfilerRecorder : MonoBehaviour
    {
        private void Update()
        {
            var session = DebugSessionManager.Current;
            if (session == null || !session.IsRuntimeRecording)
            {
                return;
            }

            session.RecordFrame(Time.unscaledDeltaTime);
        }
    }
}
