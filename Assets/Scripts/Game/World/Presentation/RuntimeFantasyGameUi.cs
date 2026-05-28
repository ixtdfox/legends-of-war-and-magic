using LegendsOfWarAndMagic.SceneManagement;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Game.World.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RuntimeFantasyGameUi : MonoBehaviour
    {
        private GameObject pauseOverlay;
        private GameObject inventoryOverlay;
        private Text optionsMessage;

        public static RuntimeFantasyGameUi Ensure()
        {
            var existing = FindFirstObjectByType<RuntimeFantasyGameUi>();
            if (existing != null)
            {
                return existing;
            }

            var obj = new GameObject("Runtime Fantasy Game UI");
            var ui = obj.AddComponent<RuntimeFantasyGameUi>();
            ui.BuildUi();
            return ui;
        }

        private void OnDestroy()
        {
            RuntimeInputBlocker.SetBlocked(pauseOverlay, false);
            RuntimeInputBlocker.SetBlocked(inventoryOverlay, false);
            Time.timeScale = 1f;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (inventoryOverlay != null && inventoryOverlay.activeSelf)
                {
                    HideInventory();
                    return;
                }

                if (pauseOverlay != null && pauseOverlay.activeSelf)
                {
                    Resume();
                    return;
                }

                if (!RuntimeInputBlocker.IsBlocked)
                {
                    ShowPause();
                }
            }

            if (keyboard.iKey.wasPressedThisFrame)
            {
                if (inventoryOverlay != null && inventoryOverlay.activeSelf)
                {
                    HideInventory();
                    return;
                }

                if (!RuntimeInputBlocker.IsBlocked)
                {
                    ShowInventory();
                }
            }
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Runtime Fantasy Game UI Canvas");
            canvas.sortingOrder = 5400;
            canvas.transform.SetParent(transform, false);

            BuildPlayerHud(canvas.transform);
            BuildInventory(canvas.transform);
            BuildPauseMenu(canvas.transform);

            inventoryOverlay.SetActive(false);
            pauseOverlay.SetActive(false);
        }

        private static void BuildPlayerHud(Transform parent)
        {
            var root = RuntimeUiFactory.CreateUiObject(parent, "Player Status Bar");
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.anchoredPosition = new Vector2(24f, -24f);
            rootRect.sizeDelta = new Vector2(430f, 190f);

            RuntimeUiFactory.ApplySprite(
                RuntimeUiFactory.EnsureImage(root),
                RuntimeUiFactory.CharacterBarBoxSprite,
                Color.white,
                Image.Type.Sliced);

            var portrait = RuntimeUiFactory.CreateSpriteImage(
                root.transform,
                "Player Frame",
                RuntimeUiFactory.PlayerFrameSprite,
                Color.white,
                Image.Type.Simple,
                true);
            var portraitRect = portrait.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(24f, 0f);
            portraitRect.sizeDelta = new Vector2(118f, 118f);
            portrait.raycastTarget = false;

            var name = RuntimeUiFactory.CreateText(root.transform, "Player Name", "Hero", 24, new Color(0.36f, 0.12f, 0.06f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var nameRect = name.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.offsetMin = new Vector2(154f, -58f);
            nameRect.offsetMax = new Vector2(-34f, -24f);

            CreateStatusFill(root.transform, "Health", RuntimeUiFactory.CharacterHealthSprite, "Health", new Vector2(154f, -84f));
            CreateStatusFill(root.transform, "Mana", RuntimeUiFactory.CharacterManaSprite, "Mana", new Vector2(154f, -122f));
        }

        private static void CreateStatusFill(Transform parent, string name, string fillSprite, string label, Vector2 anchoredPosition)
        {
            var bar = RuntimeUiFactory.CreateUiObject(parent, $"{name} Bar");
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(245f, 30f);

            var fill = RuntimeUiFactory.CreateSpriteImage(bar.transform, "Fill", fillSprite, Color.white, Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            RuntimeUiFactory.Stretch(fill.gameObject, Vector2.zero, Vector2.zero);

            var cover = RuntimeUiFactory.CreateSpriteImage(bar.transform, "Cover", RuntimeUiFactory.CharacterBarCoverSprite, Color.white, Image.Type.Sliced);
            RuntimeUiFactory.Stretch(cover.gameObject, Vector2.zero, Vector2.zero);
            cover.raycastTarget = false;

            var text = RuntimeUiFactory.CreateText(bar.transform, "Label", label, 16, new Color(1f, 0.92f, 0.74f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.Stretch(text.gameObject, new Vector2(8f, 2f), new Vector2(-8f, -2f));
        }

        private void BuildPauseMenu(Transform parent)
        {
            pauseOverlay = RuntimeUiFactory.CreateUiObject(parent, "Pause Overlay");
            RuntimeUiFactory.Stretch(pauseOverlay, Vector2.zero, Vector2.zero);
            var backdrop = pauseOverlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.015f, 0.012f, 0.68f);

            var window = RuntimeUiFactory.CreateUiObject(pauseOverlay.transform, "Pause Window");
            var rect = window.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 540f);
            RuntimeUiFactory.StyleMenuPanel(window);
            RuntimeUiFactory.AddVerticalLayout(window, 20f, new RectOffset(80, 80, 58, 62), TextAnchor.UpperCenter);

            var header = RuntimeUiFactory.CreateHeader(window.transform, "Pause Header", "Paused", 42);
            RuntimeUiFactory.AddLayoutElement(header.transform.parent.gameObject, 0f, 78f);

            RuntimeUiFactory.CreateButton(window.transform, "Resume Button", "Resume", Resume, new Vector2(360f, 68f));
            RuntimeUiFactory.CreateButton(window.transform, "Options Button", "Options", ToggleOptionsMessage, new Vector2(360f, 68f));
            RuntimeUiFactory.CreateButton(window.transform, "Main Menu Button", "Main Menu", ReturnToMainMenu, new Vector2(360f, 68f));
            RuntimeUiFactory.CreateButton(window.transform, "Exit Button", "Exit", QuitGame, new Vector2(360f, 68f));

            optionsMessage = RuntimeUiFactory.CreateText(window.transform, "Options Message", "Options are not available yet", 22, new Color(0.34f, 0.15f, 0.07f, 1f), TextAnchor.MiddleCenter);
            RuntimeUiFactory.AddLayoutElement(optionsMessage.gameObject, 0f, 44f);
            optionsMessage.gameObject.SetActive(false);
        }

        private void BuildInventory(Transform parent)
        {
            inventoryOverlay = RuntimeUiFactory.CreateUiObject(parent, "Inventory Overlay");
            RuntimeUiFactory.Stretch(inventoryOverlay, Vector2.zero, Vector2.zero);
            var backdrop = inventoryOverlay.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.015f, 0.012f, 0.62f);

            var window = RuntimeUiFactory.CreateUiObject(inventoryOverlay.transform, "Inventory Window");
            var rect = window.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(980f, 740f);
            RuntimeUiFactory.StyleInventoryPanel(window);

            var header = RuntimeUiFactory.CreateHeader(window.transform, "Inventory Header", "Inventory", 40);
            var headerRect = header.transform.parent.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -42f);
            headerRect.sizeDelta = new Vector2(680f, 58f);

            var close = RuntimeUiFactory.CreateCloseButton(window.transform, "Inventory Close Button", HideInventory);
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-68f, -44f);
            closeRect.sizeDelta = new Vector2(42f, 42f);

            var gridRoot = RuntimeUiFactory.CreateUiObject(window.transform, "Inventory Slot Grid");
            var gridRect = gridRoot.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.5f, 0.5f);
            gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 0.5f);
            gridRect.anchoredPosition = new Vector2(0f, -8f);
            gridRect.sizeDelta = new Vector2(590f, 384f);
            var grid = gridRoot.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(82f, 82f);
            grid.spacing = new Vector2(16f, 16f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 6;
            grid.childAlignment = TextAnchor.MiddleCenter;

            for (var i = 0; i < 24; i++)
            {
                RuntimeUiFactory.CreateInventorySlot(gridRoot.transform, $"Inventory Slot {i + 1:00}");
            }

            var money = RuntimeUiFactory.CreateSpriteImage(window.transform, "Money Bar", RuntimeUiFactory.MoneyBarSprite, Color.white, Image.Type.Sliced);
            var moneyRect = money.GetComponent<RectTransform>();
            moneyRect.anchorMin = new Vector2(0.5f, 0f);
            moneyRect.anchorMax = new Vector2(0.5f, 0f);
            moneyRect.pivot = new Vector2(0.5f, 0f);
            moneyRect.anchoredPosition = new Vector2(0f, 58f);
            moneyRect.sizeDelta = new Vector2(260f, 52f);

            var coin = RuntimeUiFactory.CreateSpriteImage(money.transform, "Coin Icon", RuntimeUiFactory.CoinIconSprite, Color.white, Image.Type.Simple, true);
            var coinRect = coin.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0f, 0.5f);
            coinRect.anchorMax = new Vector2(0f, 0.5f);
            coinRect.pivot = new Vector2(0f, 0.5f);
            coinRect.anchoredPosition = new Vector2(18f, 0f);
            coinRect.sizeDelta = new Vector2(36f, 36f);

            var amount = RuntimeUiFactory.CreateText(money.transform, "Coin Amount", "0", 24, new Color(0.36f, 0.15f, 0.06f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.Stretch(amount.gameObject, new Vector2(66f, 4f), new Vector2(-18f, -4f));
        }

        private void ShowPause()
        {
            pauseOverlay.SetActive(true);
            RuntimeInputBlocker.SetBlocked(pauseOverlay, true);
            Time.timeScale = 0f;
        }

        private void Resume()
        {
            pauseOverlay.SetActive(false);
            RuntimeInputBlocker.SetBlocked(pauseOverlay, false);
            Time.timeScale = 1f;
        }

        private void ToggleOptionsMessage()
        {
            if (optionsMessage != null)
            {
                optionsMessage.gameObject.SetActive(!optionsMessage.gameObject.activeSelf);
            }
        }

        private void ShowInventory()
        {
            inventoryOverlay.SetActive(true);
            RuntimeInputBlocker.SetBlocked(inventoryOverlay, true);
        }

        private void HideInventory()
        {
            inventoryOverlay.SetActive(false);
            RuntimeInputBlocker.SetBlocked(inventoryOverlay, false);
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            RuntimeInputBlocker.ReleaseAll();
            SceneManager.LoadScene(SceneNames.MainMenuScene);
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested from pause menu.");
#endif
            UnityEngine.Application.Quit();
        }
    }
}
