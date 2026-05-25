using System;
using LegendsOfWarAndMagic.ProceduralGeneration.Runtime;
using LegendsOfWarAndMagic.SceneManagement;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.MapGeneration
{
    [DisallowMultipleComponent]
    public sealed class MapGenerationController : MonoBehaviour
    {
        private readonly System.Random seedRandom = new();

        private MapSizeOption selectedSize = MapSizeOption.Medium;
        private LandTypeOption selectedLandType = LandTypeOption.Mainland;
        private WaterAmountOption selectedWaterAmount = WaterAmountOption.Normal;
        private ReliefOption selectedRelief = ReliefOption.Hills;
        private PropDensityOption selectedPropDensity = PropDensityOption.Normal;

        private Button[] sizeButtons;
        private Button[] landButtons;
        private Button[] waterButtons;
        private Button[] reliefButtons;
        private Button[] propButtons;
        private InputField seedInput;

        private void Awake()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Map Generation Canvas");
            var background = RuntimeUiFactory.CreateImage(canvas.transform, "Background", new Color(0.048f, 0.062f, 0.067f, 1f));
            RuntimeUiFactory.Stretch(background.gameObject, Vector2.zero, Vector2.zero);

            var panel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Generator Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1240f, 920f);
            RuntimeUiFactory.AddVerticalLayout(panel, 20f, new RectOffset(54, 54, 36, 36), TextAnchor.UpperCenter);

            var title = RuntimeUiFactory.CreateText(panel.transform, "Title", "Новая карта", 54, new Color(0.98f, 0.86f, 0.55f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(title.gameObject, 0f, 72f);

            sizeButtons = CreateOptionRow(panel.transform, "Размер карты", new[] { "Маленькая", "Средняя", "Большая" }, (index) =>
            {
                selectedSize = (MapSizeOption)index;
                UpdateSelected(sizeButtons, index);
            });

            landButtons = CreateOptionRow(panel.transform, "Тип суши", new[] { "Материк", "Острова", "Архипелаг" }, (index) =>
            {
                selectedLandType = (LandTypeOption)index;
                UpdateSelected(landButtons, index);
            });

            waterButtons = CreateOptionRow(panel.transform, "Количество воды", new[] { "Мало воды", "Нормально", "Много воды" }, (index) =>
            {
                selectedWaterAmount = (WaterAmountOption)index;
                UpdateSelected(waterButtons, index);
            });

            reliefButtons = CreateOptionRow(panel.transform, "Рельеф", new[] { "Равнинный", "Холмистый", "Горный" }, (index) =>
            {
                selectedRelief = (ReliefOption)index;
                UpdateSelected(reliefButtons, index);
            });

            propButtons = CreateOptionRow(panel.transform, "Леса и пропсы", new[] { "Мало", "Нормально", "Много" }, (index) =>
            {
                selectedPropDensity = (PropDensityOption)index;
                UpdateSelected(propButtons, index);
            });

            CreateSeedRow(panel.transform);
            CreateNavigationRow(panel.transform);

            UpdateSelected(sizeButtons, (int)selectedSize);
            UpdateSelected(landButtons, (int)selectedLandType);
            UpdateSelected(waterButtons, (int)selectedWaterAmount);
            UpdateSelected(reliefButtons, (int)selectedRelief);
            UpdateSelected(propButtons, (int)selectedPropDensity);
        }

        private Button[] CreateOptionRow(Transform parent, string title, string[] labels, Action<int> onSelected)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, $"{title} Section");
            var image = section.AddComponent<Image>();
            image.color = new Color(0.095f, 0.105f, 0.09f, 0.88f);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 104f);
            RuntimeUiFactory.AddHorizontalLayout(section, 20f, new RectOffset(28, 28, 14, 14), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, $"{title} Label", title, 28, new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 310f, 70f);

            var buttons = new Button[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                var capturedIndex = i;
                buttons[i] = RuntimeUiFactory.CreateButton(section.transform, $"{labels[i]} Option", labels[i], () => onSelected(capturedIndex), new Vector2(225f, 64f));
            }

            return buttons;
        }

        private void CreateSeedRow(Transform parent)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, "Seed Section");
            var image = section.AddComponent<Image>();
            image.color = new Color(0.095f, 0.105f, 0.09f, 0.88f);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 104f);
            RuntimeUiFactory.AddHorizontalLayout(section, 18f, new RectOffset(28, 28, 14, 14), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, "Seed Label", "Seed", 28, new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 310f, 70f);

            seedInput = RuntimeUiFactory.CreateInputField(section.transform, "Seed Input", "пусто = случайный", new Vector2(360f, 64f));
            RuntimeUiFactory.CreateButton(section.transform, "Random Seed Button", "Случайный seed", RandomizeSeed, new Vector2(290f, 64f));
        }

        private void CreateNavigationRow(Transform parent)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, "Navigation Row");
            RuntimeUiFactory.AddLayoutElement(row, 0f, 92f);
            RuntimeUiFactory.AddHorizontalLayout(row, 24f, new RectOffset(0, 0, 8, 0), TextAnchor.MiddleCenter);

            RuntimeUiFactory.CreateButton(row.transform, "Back Button", "Назад", BackToMainMenu, new Vector2(300f, 74f));
            RuntimeUiFactory.CreateButton(row.transform, "Start Game Button", "Начать игру", StartGame, new Vector2(360f, 74f));
        }

        private void UpdateSelected(Button[] buttons, int selectedIndex)
        {
            if (buttons == null)
            {
                return;
            }

            for (var i = 0; i < buttons.Length; i++)
            {
                RuntimeUiFactory.SetButtonSelected(buttons[i], i == selectedIndex);
            }
        }

        private void RandomizeSeed()
        {
            seedInput.text = seedRandom.Next(1, int.MaxValue).ToString();
        }

        private void BackToMainMenu()
        {
            SceneManager.LoadScene(SceneNames.MainMenuScene);
        }

        private void StartGame()
        {
            var request = new MapGenerationRequest
            {
                MapSize = selectedSize,
                LandType = selectedLandType,
                WaterAmount = selectedWaterAmount,
                Relief = selectedRelief,
                PropDensity = selectedPropDensity,
                SeedText = seedInput != null ? seedInput.text : string.Empty
            };

            var resolvedSeed = request.ResolveSeed();
            request.SeedText = resolvedSeed.ToString();
            MapGenerationSession.SetRequest(request);
            Debug.Log($"Map generation request selected: {request.BuildSummary(resolvedSeed)}");
            SceneManager.LoadScene(SceneNames.GameScene);
        }
    }
}
