using LegendsOfWarAndMagic.DebugTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.DebugTools.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        private Canvas canvas;
        private Text label;
        private float nextRefreshTime;

        private void Awake()
        {
            BuildUi();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void BuildUi()
        {
            var canvasObject = new GameObject("Debug Overlay Canvas");
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9100;
            canvasObject.AddComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("Debug Overlay Panel");
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(18f, -18f);
            panelRect.sizeDelta = new Vector2(760f, 172f);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.62f);
            image.raycastTarget = false;

            var labelObject = new GameObject("Debug Overlay Text");
            labelObject.transform.SetParent(panel.transform, false);
            var labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(14f, 10f);
            labelRect.offsetMax = new Vector2(-14f, -10f);
            label = labelObject.AddComponent<Text>();
            label.font = ResolveFont();
            label.fontSize = 19;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 19;
            label.alignment = TextAnchor.UpperLeft;
            label.color = new Color(0.84f, 1f, 0.70f, 1f);
            label.raycastTarget = false;
            canvasObject.SetActive(false);
        }

        private void Refresh()
        {
            var session = DebugSessionManager.Current;
            if (session == null)
            {
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }

                return;
            }

            canvas.gameObject.SetActive(true);
            label.text =
                "DEBUG ENABLED\n" +
                $"Session: {session.ShortId}\n" +
                $"Output: {session.RootDirectory}\n" +
                "F2: start/stop profiling recording    F5: save terrain snapshot\n" +
                $"Recording: {(session.IsRuntimeRecording ? "ON" : "OFF")}\n" +
                $"Last saved: {(string.IsNullOrWhiteSpace(session.LastSavedPath) ? "-" : session.LastSavedPath)}";
        }

        private static Font ResolveFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
