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
        private const float GrassHighLodMin = 10f;
        private const float GrassHighLodMax = 18f;
        private const float GrassDrawDistanceMin = 60f;
        private const float GrassDrawDistanceMax = 140f;

        private readonly System.Random seedRandom = new();

        private MapSizeOption selectedSize = MapSizeOption.Medium;
        private LandTypeOption selectedLandType = LandTypeOption.Mainland;
        private WaterAmountOption selectedWaterAmount = WaterAmountOption.Normal;
        private ReliefOption selectedRelief = ReliefOption.Hills;
        private PropDensityOption selectedPropDensity = PropDensityOption.Normal;
        private float selectedTreeDensity = 0.72f;
        private float selectedGrassSaturation = 0.95f;
        private float selectedGrassHighDetailDistance = 18f;
        private float selectedGrassDrawDistance = 120f;

        private Button[] sizeButtons;
        private Button[] landButtons;
        private Button[] waterButtons;
        private Button[] reliefButtons;
        private Button[] propButtons;
        private InputField seedInput;
        private Text treeDensityValue;
        private Text grassSaturationValue;
        private Text grassHighLodValue;
        private Text grassDrawDistanceValue;

        private void Awake()
        {
            RuntimeLoadingOverlay.Hide();
            RuntimeInputBlocker.ReleaseAll();
            RuntimeInputBlocker.ShowCursorForUi();
            BuildUi();
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Map Generation Canvas");
            var background = RuntimeUiFactory.CreateImage(canvas.transform, "Background", new Color(0.048f, 0.062f, 0.067f, 1f));
            RuntimeUiFactory.Stretch(background.gameObject, Vector2.zero, Vector2.zero);

            var decoration = RuntimeUiFactory.CreateSpriteImage(
                canvas.transform,
                "Fantasy Backdrop",
                RuntimeUiFactory.LoadingDecorationSprite,
                new Color(1f, 0.82f, 0.54f, 0.18f),
                Image.Type.Simple,
                true);
            var decorationRect = decoration.GetComponent<RectTransform>();
            decorationRect.anchorMin = new Vector2(0.5f, 0.5f);
            decorationRect.anchorMax = new Vector2(0.5f, 0.5f);
            decorationRect.pivot = new Vector2(0.5f, 0.5f);
            decorationRect.anchoredPosition = Vector2.zero;
            decorationRect.sizeDelta = new Vector2(1440f, 1080f);
            decoration.raycastTarget = false;

            var panel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Generator Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1240f, 1010f);
            RuntimeUiFactory.StyleMenuPanel(panel);
            RuntimeUiFactory.AddVerticalLayout(panel, 12f, new RectOffset(64, 64, 44, 48), TextAnchor.UpperCenter);

            var title = RuntimeUiFactory.CreateHeader(panel.transform, "Header", "Новая карта", 42);
            RuntimeUiFactory.AddLayoutElement(title.transform.parent.gameObject, 0f, 68f);

            var scrollContent = CreateScrollableContent(panel.transform);

            sizeButtons = CreateOptionRow(scrollContent, "Размер карты", new[] { "Маленькая", "Средняя", "Большая" }, (index) =>
            {
                selectedSize = (MapSizeOption)index;
                UpdateSelected(sizeButtons, index);
            });

            landButtons = CreateOptionRow(scrollContent, "Тип суши", new[] { "Материк", "Острова", "Архипелаг" }, (index) =>
            {
                selectedLandType = (LandTypeOption)index;
                UpdateSelected(landButtons, index);
            });

            waterButtons = CreateOptionRow(scrollContent, "Количество воды", new[] { "Мало воды", "Нормально", "Много воды" }, (index) =>
            {
                selectedWaterAmount = (WaterAmountOption)index;
                UpdateSelected(waterButtons, index);
            });

            reliefButtons = CreateOptionRow(scrollContent, "Рельеф", new[] { "Равнинный", "Холмистый", "Горный" }, (index) =>
            {
                selectedRelief = (ReliefOption)index;
                UpdateSelected(reliefButtons, index);
            });

            propButtons = CreateOptionRow(scrollContent, "Леса и пропсы", new[] { "Мало", "Нормально", "Много" }, (index) =>
            {
                selectedPropDensity = (PropDensityOption)index;
                UpdateSelected(propButtons, index);
            });

            CreateTreeDensityRow(scrollContent);
            CreateGrassSettingsSection(scrollContent);
            CreateSeedRow(scrollContent);
            CreateNavigationRow(panel.transform);

            UpdateSelected(sizeButtons, (int)selectedSize);
            UpdateSelected(landButtons, (int)selectedLandType);
            UpdateSelected(waterButtons, (int)selectedWaterAmount);
            UpdateSelected(reliefButtons, (int)selectedRelief);
            UpdateSelected(propButtons, (int)selectedPropDensity);
            UpdateTreeDensityLabel(selectedTreeDensity);
            UpdateGrassSaturationLabel(selectedGrassSaturation);
            UpdateGrassHighLodLabel(selectedGrassHighDetailDistance);
            UpdateGrassDrawDistanceLabel(selectedGrassDrawDistance);
        }

        private Transform CreateScrollableContent(Transform parent)
        {
            var scrollRoot = RuntimeUiFactory.CreateUiObject(parent, "Generator Scroll Root");
            var scrollRootRect = scrollRoot.GetComponent<RectTransform>();
            scrollRootRect.sizeDelta = new Vector2(0f, 742f);
            RuntimeUiFactory.AddLayoutElement(scrollRoot, 0f, 742f);

            var viewport = RuntimeUiFactory.CreateUiObject(scrollRoot.transform, "Generator Scroll Viewport");
            RuntimeUiFactory.Stretch(viewport.gameObject, new Vector2(0f, 0f), new Vector2(-24f, 0f));
            viewport.AddComponent<RectMask2D>();

            var content = RuntimeUiFactory.CreateUiObject(viewport.transform, "Generator Scroll Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            RuntimeUiFactory.AddVerticalLayout(content, 12f, new RectOffset(0, 8, 0, 0), TextAnchor.UpperCenter);
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = CreateScrollbar(scrollRoot.transform);
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 48f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 4f;
            return content.transform;
        }

        private Scrollbar CreateScrollbar(Transform parent)
        {
            var scrollbarImage = RuntimeUiFactory.CreateSpriteImage(parent, "Generator Scrollbar", RuntimeUiFactory.LoadingBarBackgroundSprite, Color.white, Image.Type.Sliced);
            var scrollbarRect = scrollbarImage.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-16f, 0f);
            scrollbarRect.offsetMax = Vector2.zero;

            var handle = RuntimeUiFactory.CreateSpriteImage(scrollbarImage.transform, "Handle", RuntimeUiFactory.LoadingBarFillSprite, Color.white, Image.Type.Sliced);
            RuntimeUiFactory.Stretch(handle.gameObject, Vector2.zero, Vector2.zero);

            var scrollbar = scrollbarImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            return scrollbar;
        }

        private Button[] CreateOptionRow(Transform parent, string title, string[] labels, Action<int> onSelected)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, $"{title} Section");
            RuntimeUiFactory.StyleSection(section);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 86f);
            RuntimeUiFactory.AddHorizontalLayout(section, 16f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, $"{title} Label", title, 28, new Color(0.30f, 0.15f, 0.07f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
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
            RuntimeUiFactory.StyleSection(section);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 88f);
            RuntimeUiFactory.AddHorizontalLayout(section, 18f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, "Tree Density Label", "Густота леса", 28, new Color(0.30f, 0.15f, 0.07f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 300f, 58f);

            RuntimeUiFactory.CreateSlider(section.transform, "Tree Density Slider", selectedTreeDensity, OnTreeDensityChanged, new Vector2(570f, 58f));

            treeDensityValue = RuntimeUiFactory.CreateText(section.transform, "Tree Density Value", string.Empty, 26, new Color(0.47f, 0.10f, 0.05f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(treeDensityValue.gameObject, 220f, 58f);
        }

        private void CreateGrassSettingsSection(Transform parent)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, "Grass Settings Section");
            RuntimeUiFactory.StyleSection(section);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 176f);
            RuntimeUiFactory.AddVerticalLayout(section, 8f, new RectOffset(24, 24, 8, 8), TextAnchor.MiddleCenter);

            grassSaturationValue = CreateGrassSliderRow(
                section.transform,
                "Насыщенность травы",
                selectedGrassSaturation,
                OnGrassSaturationChanged);
            grassHighLodValue = CreateGrassSliderRow(
                section.transform,
                "LOD0 травы",
                Mathf.InverseLerp(GrassHighLodMin, GrassHighLodMax, selectedGrassHighDetailDistance),
                OnGrassHighLodChanged);
            grassDrawDistanceValue = CreateGrassSliderRow(
                section.transform,
                "Дальность травы",
                Mathf.InverseLerp(GrassDrawDistanceMin, GrassDrawDistanceMax, selectedGrassDrawDistance),
                OnGrassDrawDistanceChanged);
        }

        private Text CreateGrassSliderRow(Transform parent, string labelText, float sliderValue, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, $"{labelText} Row");
            RuntimeUiFactory.AddLayoutElement(row, 0f, 48f);
            RuntimeUiFactory.AddHorizontalLayout(row, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(row.transform, $"{labelText} Label", labelText, 24, new Color(0.30f, 0.15f, 0.07f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 300f, 46f);

            RuntimeUiFactory.CreateSlider(row.transform, $"{labelText} Slider", sliderValue, onChanged, new Vector2(560f, 46f));

            var value = RuntimeUiFactory.CreateText(row.transform, $"{labelText} Value", string.Empty, 22, new Color(0.47f, 0.10f, 0.05f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(value.gameObject, 230f, 46f);
            return value;
        }

        private void CreateSeedRow(Transform parent)
        {
            var section = RuntimeUiFactory.CreateUiObject(parent, "Seed Section");
            RuntimeUiFactory.StyleSection(section);
            RuntimeUiFactory.AddLayoutElement(section, 0f, 86f);
            RuntimeUiFactory.AddHorizontalLayout(section, 18f, new RectOffset(24, 24, 10, 10), TextAnchor.MiddleCenter);

            var label = RuntimeUiFactory.CreateText(section.transform, "Seed Label", "Seed", 28, new Color(0.30f, 0.15f, 0.07f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
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

        private void OnGrassSaturationChanged(float value)
        {
            selectedGrassSaturation = Mathf.Clamp01(value);
            UpdateGrassSaturationLabel(selectedGrassSaturation);
        }

        private void OnGrassHighLodChanged(float value)
        {
            selectedGrassHighDetailDistance = Mathf.Lerp(GrassHighLodMin, GrassHighLodMax, Mathf.Clamp01(value));
            UpdateGrassHighLodLabel(selectedGrassHighDetailDistance);
        }

        private void OnGrassDrawDistanceChanged(float value)
        {
            selectedGrassDrawDistance = Mathf.Lerp(GrassDrawDistanceMin, GrassDrawDistanceMax, Mathf.Clamp01(value));
            UpdateGrassDrawDistanceLabel(selectedGrassDrawDistance);
        }

        private void UpdateGrassSaturationLabel(float value)
        {
            if (grassSaturationValue == null)
            {
                return;
            }

            grassSaturationValue.text = value switch
            {
                < 0.25f => "Редкая",
                < 0.55f => "Нормальная",
                < 0.85f => "Густая",
                _ => "Сплошная"
            };
        }

        private void UpdateGrassHighLodLabel(float value)
        {
            if (grassHighLodValue == null)
            {
                return;
            }

            grassHighLodValue.text = $"Near {value:0} м";
        }

        private void UpdateGrassDrawDistanceLabel(float value)
        {
            if (grassDrawDistanceValue == null)
            {
                return;
            }

            grassDrawDistanceValue.text = $"Far tint {value:0} м";
        }

        private void BackToMainMenu()
        {
            SceneManager.LoadScene(SceneNames.MainMenuScene);
        }

        private void StartGame()
        {
            var grassDrawDistance = Mathf.Max(selectedGrassDrawDistance, selectedGrassHighDetailDistance + 12f);
            var request = new MapGenerationRequest
            {
                MapSize = selectedSize,
                LandType = selectedLandType,
                WaterAmount = selectedWaterAmount,
                Relief = selectedRelief,
                PropDensity = selectedPropDensity,
                TreeDensity = selectedTreeDensity,
                GrassSaturation = selectedGrassSaturation,
                GrassHighDetailDistance = selectedGrassHighDetailDistance,
                GrassDrawDistance = grassDrawDistance,
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
