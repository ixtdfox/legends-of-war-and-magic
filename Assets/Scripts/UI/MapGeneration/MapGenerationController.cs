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
        private float selectedTreeDensity = 0.72f;

        private Button[] sizeButtons;
        private Button[] landButtons;
        private Button[] waterButtons;
        private Button[] reliefButtons;
        private Button[] propButtons;
        private InputField seedInput;
        private Text treeDensityValue;

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
            panelRect.sizeDelta = new Vector2(1240f, 1000f);
            RuntimeUiFactory.AddVerticalLayout(panel, 12f, new RectOffset(44, 44, 28, 28), TextAnchor.UpperCenter);

            var title = RuntimeUiFactory.CreateText(panel.transform, "Title", "Новая карта", 54, new Color(0.98f, 0.86f, 0.55f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(title.gameObject, 0f, 62f);

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

            CreateTreeDensityRow(panel.transform);
            CreateSeedRow(panel.transform);
            CreateNavigationRow(panel.transform);

            UpdateSelected(sizeButtons, (int)selectedSize);
            UpdateSelected(landButtons, (int)selectedLandType);
            UpdateSelected(waterButtons, (int)selectedWaterAmount);
            UpdateSelected(reliefButtons, (int)selectedRelief);
            UpdateSelected(propButtons, (int)selectedPropDensity);
            UpdateTreeDensityLabel(selectedTreeDensity);
        }

        private Button[] CreateOptionRow(Transform parent, string title, string[] labels, Action<int> onSelected)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, $"{title} Section");
            var image = section.AddComponent<Image>();
            image.color = new Color(0.095f, 0.105f, 0.09f, 0.88f);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 86f);
            RuntimeUiFactory.AddHorizontalLayout(section, 16f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, $"{title} Label", title, 28, new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 300f, 58f);

            var buttons = new Button[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                var capturedIndex = i;
                buttons[i] = RuntimeUiFactory.CreateButton(section.transform, $"{labels[i]} Option", labels[i], () => onSelected(capturedIndex), new Vector2(220f, 56f));
            }

            return buttons;
        }

        private void CreateTreeDensityRow(Transform parent)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, "Tree Density Section");
            var image = section.AddComponent<Image>();
            image.color = new Color(0.095f, 0.105f, 0.09f, 0.88f);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 88f);
            RuntimeUiFactory.AddHorizontalLayout(section, 18f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, "Tree Density Label", "Густота леса", 28, new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 300f, 58f);

            RuntimeUiFactory.CreateSlider(section.transform, "Tree Density Slider", selectedTreeDensity, OnTreeDensityChanged, new Vector2(570f, 58f));

            treeDensityValue = RuntimeUiFactory.CreateText(section.transform, "Tree Density Value", string.Empty, 26, new Color(0.95f, 0.90f, 0.72f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(treeDensityValue.gameObject, 220f, 58f);
        }

        private void CreateSeedRow(Transform parent)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, "Seed Section");
            var image = section.AddComponent<Image>();
            image.color = new Color(0.095f, 0.105f, 0.09f, 0.88f);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 86f);
            RuntimeUiFactory.AddHorizontalLayout(section, 18f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, "Seed Label", "Seed", 28, new Color(0.92f, 0.86f, 0.68f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 300f, 58f);

            seedInput = RuntimeUiFactory.CreateInputField(section.transform, "Seed Input", "пусто = случайный", new Vector2(360f, 56f));
            RuntimeUiFactory.CreateButton(section.transform, "Random Seed Button", "Случайный seed", RandomizeSeed, new Vector2(290f, 56f));
        }

        private void CreateNavigationRow(Transform parent)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, "Navigation Row");
            RuntimeUiFactory.AddLayoutElement(row, 0f, 78f);
            RuntimeUiFactory.AddHorizontalLayout(row, 24f, new RectOffset(0, 0, 4, 0), TextAnchor.MiddleCenter);

            RuntimeUiFactory.CreateButton(row.transform, "Back Button", "Назад", BackToMainMenu, new Vector2(300f, 64f));
            RuntimeUiFactory.CreateButton(row.transform, "Start Game Button", "Начать игру", StartGame, new Vector2(360f, 64f));
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

        private void OnTreeDensityChanged(float value)
        {
            selectedTreeDensity = Mathf.Clamp01(value);
            UpdateTreeDensityLabel(selectedTreeDensity);
        }

        private void UpdateTreeDensityLabel(float value)
        {
            if (treeDensityValue == null)
            {
                return;
            }

            treeDensityValue.text = value switch
            {
                < 0.25f => "Редколесье",
                < 0.55f => "Лес",
                < 0.85f => "Густо",
                _ => "Чаща"
            };
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
                TreeDensity = selectedTreeDensity,
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
