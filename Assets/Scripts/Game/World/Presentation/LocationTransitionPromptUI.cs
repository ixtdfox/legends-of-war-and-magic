using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Game.World.Presentation
{
    [DisallowMultipleComponent]
    public sealed class LocationTransitionPromptUI : MonoBehaviour
    {
        private static LocationTransitionPromptUI instance;

        private GameObject panel;
        private Text message;
        private UnityAction onCancel;

        public static LocationTransitionPromptUI Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var obj = new GameObject("Location Transition Prompt UI");
            instance = obj.AddComponent<LocationTransitionPromptUI>();
            DontDestroyOnLoad(obj);
            instance.BuildUi();
            return instance;
        }

        public void Show(string targetLocationName, UnityAction onConfirm, UnityAction onCancelAction = null)
        {
            onCancel = onCancelAction;
            message.text = $"Вы хотите перейти в «{targetLocationName}»?";
            panel.SetActive(true);
            RuntimeInputBlocker.SetBlocked(panel, true);

            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() =>
            {
                Hide();
                onConfirm?.Invoke();
            });
        }

        private Button yesButton;

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Location Transition Prompt Canvas");
            canvas.sortingOrder = 6200;
            canvas.transform.SetParent(transform, false);

            panel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Prompt Panel");
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 260f);

            RuntimeUiFactory.StyleMenuPanel(panel);
            RuntimeUiFactory.AddVerticalLayout(panel, 22f, new RectOffset(56, 56, 40, 40), TextAnchor.MiddleCenter);

            message = RuntimeUiFactory.CreateText(panel.transform, "Prompt Text", string.Empty, 34, new Color(0.32f, 0.15f, 0.07f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(message.gameObject, 0f, 92f);

            var row = RuntimeUiFactory.CreateUiObject(panel.transform, "Prompt Buttons");
            RuntimeUiFactory.AddLayoutElement(row, 0f, 72f);
            RuntimeUiFactory.AddHorizontalLayout(row, 24f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            yesButton = RuntimeUiFactory.CreateButton(row.transform, "Yes Button", "Да", null, new Vector2(220f, 64f));
            RuntimeUiFactory.CreateButton(row.transform, "No Button", "Нет", HideWithCancel, new Vector2(220f, 64f));
            panel.SetActive(false);
        }

        private void HideWithCancel()
        {
            Hide();
            onCancel?.Invoke();
        }

        private void Hide()
        {
            panel.SetActive(false);
            RuntimeInputBlocker.SetBlocked(panel, false);
        }
    }
}
