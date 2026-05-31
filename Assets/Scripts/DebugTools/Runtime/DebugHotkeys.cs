using LegendsOfWarAndMagic.DebugTools.Core;
using LegendsOfWarAndMagic.DebugTools.Snapshots;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LegendsOfWarAndMagic.DebugTools.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DebugHotkeys : MonoBehaviour
    {
        private void Update()
        {
            if (!DebugSessionManager.IsEnabled)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                DebugSessionManager.ToggleRuntimeRecording();
            }

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                var path = TerrainSnapshotService.Capture(DebugSessionManager.Current);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    DebugSessionManager.SetLastSavedPath(path);
                    Debug.Log($"Debug terrain snapshot saved: {path}");
                }
            }
        }
    }
}
