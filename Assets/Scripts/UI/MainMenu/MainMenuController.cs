using LegendsOfWarAndMagic.SceneManagement;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private GameObject mainPanel;
        private GameObject settingsPanel;

        private void Awake()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Main Menu Canvas");
            var background = RuntimeUiFactory.CreateImage(canvas.transform, "Background", new Color(0.055f, 0.072f, 0.078f, 1f));
            RuntimeUiFactory.Stretch(background.gameObject, Vector2.zero, Vector2.zero);

            var glow = RuntimeUiFactory.CreateImage(canvas.transform, "Warm Backdrop", new Color(0.32f, 0.22f, 0.10f, 0.26f));
            var glowRect = RuntimeUiFactory.Stretch(glow.gameObject, new Vector2(0f, 0f), new Vector2(0f, 0f));
            glowRect.anchorMin = new Vector2(0f, 0f);
            glowRect.anchorMax = new Vector2(1f, 1f);

            mainPanel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Main Menu Panel");
            var panelRect = mainPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(720f, 650f);
            RuntimeUiFactory.AddVerticalLayout(mainPanel, 28f, new RectOffset(70, 70, 46, 46));

            var title = RuntimeUiFactory.CreateText(mainPanel.transform, "Title", "Legends of War and Magic", 58, new Color(0.97f, 0.86f, 0.56f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(title.gameObject, 0f, 145f);

            AddMenuButton("Новая игра", StartNewGame);
            AddMenuButton("Настройки", OpenSettings);
            AddMenuButton("Выйти", QuitGame);

            BuildSettingsPanel(canvas.transform);
            settingsPanel.SetActive(false);
        }

        private void BuildSettingsPanel(Transform canvasTransform)
        {
            settingsPanel = RuntimeUiFactory.CreateUiObject(canvasTransform, "Settings Placeholder Panel");
            var rect = settingsPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(760f, 470f);

            var image = settingsPanel.AddComponent<Image>();
            image.color = new Color(0.10f, 0.12f, 0.12f, 0.94f);

            RuntimeUiFactory.AddVerticalLayout(settingsPanel, 30f, new RectOffset(70, 70, 50, 50));

            var title = RuntimeUiFactory.CreateText(settingsPanel.transform, "Settings Title", "Настройки", 46, new Color(0.97f, 0.86f, 0.56f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(title.gameObject, 0f, 90f);

            var message = RuntimeUiFactory.CreateText(settingsPanel.transform, "Settings Message", "Настройки появятся позже", 32, new Color(0.90f, 0.86f, 0.74f, 1f));
            RuntimeUiFactory.AddLayoutElement(message.gameObject, 0f, 130f);

            RuntimeUiFactory.CreateButton(settingsPanel.transform, "Back Button", "Назад", CloseSettings, new Vector2(340f, 72f));
        }

        private void AddMenuButton(string label, UnityEngine.Events.UnityAction action)
        {
            RuntimeUiFactory.CreateButton(mainPanel.transform, $"{label} Button", label, action, new Vector2(420f, 78f));
        }

        private void StartNewGame()
        {
            SceneManager.LoadScene(SceneNames.MapGenerationScene);
        }

        private void OpenSettings()
        {
            mainPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void CloseSettings()
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested from main menu.");
#endif
            Application.Quit();
        }
    }
}
