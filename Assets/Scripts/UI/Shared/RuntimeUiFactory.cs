using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.Shared
{
    public static class RuntimeUiFactory
    {
        private static Font cachedFont;

        public static Canvas CreateCanvas(string name)
        {
            EnsureEventSystem();

            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        public static RectTransform Stretch(GameObject target, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = target.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            var imageObject = CreateUiObject(parent, name);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string text,
            int size,
            Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            FontStyle fontStyle = FontStyle.Normal)
        {
            var textObject = CreateUiObject(parent, name);
            var label = textObject.AddComponent<Text>();
            label.text = text;
            label.font = ResolveFont();
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = fontStyle;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.55f));
            label.resizeTextMaxSize = size;
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string label, UnityAction onClick, Vector2 preferredSize)
        {
            var buttonObject = CreateUiObject(parent, name);
            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.24f, 0.20f, 0.13f, 0.96f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateButtonColors(false);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredSize.x;
            layout.preferredHeight = preferredSize.y;
            layout.minHeight = preferredSize.y;

            var text = CreateText(buttonObject.transform, "Label", label, 28, new Color(0.96f, 0.89f, 0.72f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.gameObject, new Vector2(16f, 4f), new Vector2(-16f, -4f));
            return button;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 preferredSize)
        {
            var inputObject = CreateUiObject(parent, name);
            var image = inputObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.10f, 0.11f, 0.92f);

            var input = inputObject.AddComponent<InputField>();
            input.textComponent = CreateText(inputObject.transform, "Text", string.Empty, 26, new Color(0.95f, 0.93f, 0.84f, 1f), TextAnchor.MiddleLeft);
            Stretch(input.textComponent.gameObject, new Vector2(16f, 6f), new Vector2(-16f, -6f));

            var placeholderText = CreateText(inputObject.transform, "Placeholder", placeholder, 24, new Color(0.65f, 0.62f, 0.52f, 0.88f), TextAnchor.MiddleLeft);
            Stretch(placeholderText.gameObject, new Vector2(16f, 6f), new Vector2(-16f, -6f));
            input.placeholder = placeholderText;

            var layout = inputObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredSize.x;
            layout.preferredHeight = preferredSize.y;
            layout.minHeight = preferredSize.y;

            return input;
        }

        public static Slider CreateSlider(
            Transform parent,
            string name,
            float value,
            UnityAction<float> onValueChanged,
            Vector2 preferredSize)
        {
            var sliderObject = CreateUiObject(parent, name);

            var background = CreateImage(sliderObject.transform, "Background", new Color(0.08f, 0.10f, 0.09f, 0.95f));
            var backgroundRect = Stretch(background.gameObject, Vector2.zero, Vector2.zero);
            backgroundRect.anchorMin = new Vector2(0f, 0.35f);
            backgroundRect.anchorMax = new Vector2(1f, 0.65f);

            var fillArea = CreateUiObject(sliderObject.transform, "Fill Area");
            var fillAreaRect = Stretch(fillArea, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.65f);

            var fill = CreateImage(fillArea.transform, "Fill", new Color(0.55f, 0.42f, 0.18f, 1f));
            Stretch(fill.gameObject, Vector2.zero, Vector2.zero);

            var handleArea = CreateUiObject(sliderObject.transform, "Handle Slide Area");
            var handleAreaRect = Stretch(handleArea, new Vector2(10f, 0f), new Vector2(-10f, 0f));
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;

            var handle = CreateImage(handleArea.transform, "Handle", new Color(0.97f, 0.86f, 0.55f, 1f));
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(28f, 46f);

            var slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handle;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handleRect;
            slider.value = Mathf.Clamp01(value);
            if (onValueChanged != null)
            {
                slider.onValueChanged.AddListener(onValueChanged);
            }

            var layout = sliderObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredSize.x;
            layout.preferredHeight = preferredSize.y;
            layout.minHeight = preferredSize.y;

            return slider;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, RectOffset padding, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var layout = target.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, RectOffset padding, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var layout = target.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static LayoutElement AddLayoutElement(GameObject target, float preferredWidth, float preferredHeight)
        {
            var layout = target.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
            if (preferredHeight > 0f)
            {
                layout.minHeight = preferredHeight;
            }

            return layout;
        }

        public static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.colors = CreateButtonColors(selected);
            if (button.targetGraphic is Image image)
            {
                image.color = selected
                    ? new Color(0.53f, 0.40f, 0.18f, 0.98f)
                    : new Color(0.20f, 0.17f, 0.12f, 0.96f);
            }
        }

        public static GameObject CreateUiObject(Transform parent, string name)
        {
            var uiObject = new GameObject(name);
            uiObject.transform.SetParent(parent, false);
            uiObject.AddComponent<RectTransform>();
            return uiObject;
        }

        private static ColorBlock CreateButtonColors(bool selected)
        {
            var normal = selected
                ? new Color(0.53f, 0.40f, 0.18f, 0.98f)
                : new Color(0.20f, 0.17f, 0.12f, 0.96f);

            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = selected ? new Color(0.63f, 0.50f, 0.24f, 1f) : new Color(0.32f, 0.26f, 0.16f, 1f),
                pressedColor = new Color(0.69f, 0.49f, 0.18f, 1f),
                selectedColor = new Color(0.55f, 0.42f, 0.19f, 1f),
                disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static Font ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return cachedFont;
        }
    }
}
