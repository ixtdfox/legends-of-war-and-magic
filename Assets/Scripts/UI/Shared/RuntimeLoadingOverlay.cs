using System.Collections;
using LegendsOfWarAndMagic.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.Shared
{
    [DisallowMultipleComponent]
    public sealed class RuntimeLoadingOverlay : MonoBehaviour
    {
        private static RuntimeLoadingOverlay instance;

        private GameObject panel;
        private Text messageText;
        private Image progressFill;
        private RectTransform progressFillRect;
        private Text percentText;

        public static RuntimeLoadingOverlay Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var obj = new GameObject("Runtime Loading Overlay");
            DontDestroyOnLoad(obj);
            instance = obj.AddComponent<RuntimeLoadingOverlay>();
            instance.BuildUi();
            return instance;
        }

        public static void Show(string message, float progress)
        {
            Ensure().ShowOverlay(message, progress);
        }

        public static void SetProgress(string message, float progress)
        {
            Ensure().SetOverlayProgress(message, progress);
        }

        public static void Hide()
        {
            if (instance == null || instance.panel == null)
            {
                return;
            }

            RuntimeInputBlocker.SetBlocked(instance.panel, false);
            instance.panel.SetActive(false);
        }

        public static void LoadGameScene(string message, bool hideWhenLoaded)
        {
            LoadScene(SceneNames.GameScene, message, hideWhenLoaded);
        }

        public static void LoadScene(string sceneName, string message, bool hideWhenLoaded = true)
        {
            var overlay = Ensure();
            overlay.StopAllCoroutines();
            overlay.StartCoroutine(overlay.LoadSceneRoutine(sceneName, message, hideWhenLoaded));
        }

        private void OnDestroy()
        {
            if (panel != null)
            {
                RuntimeInputBlocker.SetBlocked(panel, false);
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Runtime Loading Overlay Canvas");
            canvas.sortingOrder = 7000;
            canvas.transform.SetParent(transform, false);

            panel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Loading Panel");
            RuntimeUiFactory.Stretch(panel, Vector2.zero, Vector2.zero);
            var backdrop = panel.AddComponent<Image>();
            backdrop.color = new Color(0.025f, 0.03f, 0.028f, 0.86f);

            var box = RuntimeUiFactory.CreateUiObject(panel.transform, "Loading Box");
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(820f, 260f);

            var boxImage = box.AddComponent<Image>();
            boxImage.color = new Color(0.08f, 0.095f, 0.085f, 0.97f);
            RuntimeUiFactory.AddVerticalLayout(box, 22f, new RectOffset(52, 52, 36, 36), TextAnchor.MiddleCenter);

            messageText = RuntimeUiFactory.CreateText(
                box.transform,
                "Message",
                "Загрузка...",
                34,
                new Color(0.96f, 0.90f, 0.74f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(messageText.gameObject, 0f, 78f);

            var bar = RuntimeUiFactory.CreateUiObject(box.transform, "Progress Bar");
            RuntimeUiFactory.AddLayoutElement(bar, 0f, 34f);
            var barImage = bar.AddComponent<Image>();
            barImage.color = new Color(0.04f, 0.05f, 0.045f, 1f);

            var fill = RuntimeUiFactory.CreateUiObject(bar.transform, "Fill");
            progressFillRect = RuntimeUiFactory.Stretch(fill, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            progressFillRect.anchorMin = new Vector2(0f, 0f);
            progressFillRect.anchorMax = new Vector2(0f, 1f);
            progressFill = fill.AddComponent<Image>();
            progressFill.color = new Color(0.86f, 0.62f, 0.22f, 1f);

            percentText = RuntimeUiFactory.CreateText(
                box.transform,
                "Percent",
                "0%",
                22,
                new Color(0.78f, 0.73f, 0.62f, 1f),
                TextAnchor.MiddleCenter,
                FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(percentText.gameObject, 0f, 38f);

            panel.SetActive(false);
        }

        private void ShowOverlay(string message, float progress)
        {
            panel.SetActive(true);
            RuntimeInputBlocker.SetBlocked(panel, true);
            SetOverlayProgress(message, progress);
        }

        private void SetOverlayProgress(string message, float progress)
        {
            if (messageText != null && !string.IsNullOrWhiteSpace(message))
            {
                messageText.text = message;
            }

            var normalized = Mathf.Clamp01(progress);
            if (progressFill != null)
            {
                progressFill.enabled = normalized > 0.001f;
            }

            if (progressFillRect != null)
            {
                progressFillRect.anchorMax = new Vector2(normalized, 1f);
            }

            if (percentText != null)
            {
                percentText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }

            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator LoadSceneRoutine(string sceneName, string message, bool hideWhenLoaded)
        {
            ShowOverlay(message, 0.02f);
            yield return null;

            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                Hide();
                yield break;
            }

            while (!operation.isDone)
            {
                var progress = Mathf.Clamp01(operation.progress / 0.9f);
                SetOverlayProgress(message, Mathf.Lerp(0.05f, 0.82f, progress));
                yield return null;
            }

            SetOverlayProgress("Подготавливаем локацию...", hideWhenLoaded ? 1f : 0.86f);
            yield return null;

            if (hideWhenLoaded)
            {
                Hide();
            }
        }
    }
}
