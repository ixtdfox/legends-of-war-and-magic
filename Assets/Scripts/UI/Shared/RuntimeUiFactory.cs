using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.Shared
{
    public static class RuntimeUiFactory
    {
        public const string MenuBoxSprite = "GUI/PNG/Menu Panel/Menu_Box";
        public const string MenuButtonSprite = "GUI/PNG/Menu Panel/Button";
        public const string MenuButtonShadowSprite = "GUI/PNG/Menu Panel/ButtonShadow";
        public const string LoginInputSprite = "GUI/PNG/Login Panel/LoginPassword_Box";
        public const string InventoryBoxSprite = "GUI/PNG/Inventory/Inventory_Box";
        public const string InventoryTitleSprite = "GUI/PNG/Inventory/Title_Box";
        public const string InventorySlotSprite = "GUI/PNG/Inventory/Inventory_Slot";
        public const string MoneyBarSprite = "GUI/PNG/Inventory/Money_Bar";
        public const string CoinIconSprite = "GUI/PNG/Inventory/Coin_Icon";
        public const string CloseIconSprite = "GUI/PNG/Inventory/Close_Icon";
        public const string LoadingBarBackgroundSprite = "GUI/PNG/Loading Bar/Background_Bar";
        public const string LoadingBarFillSprite = "GUI/PNG/Loading Bar/Progress_Bar";
        public const string LoadingDecorationSprite = "GUI/PNG/Loading Bar/Decoration";
        public const string CharacterBarBoxSprite = "GUI/PNG/Character Bar/Character_Box";
        public const string CharacterHealthSprite = "GUI/PNG/Character Bar/Health";
        public const string CharacterManaSprite = "GUI/PNG/Character Bar/Mana";
        public const string CharacterBarCoverSprite = "GUI/PNG/Character Bar/HealthMana_Cover";
        public const string PlayerFrameSprite = "GUI/PNG/Character Bar/Player_Frame";
        public const string CharacterPanelSprite = "GUI/PNG/Character Panel/Character_Box";
        public const string CharacterStatsBoxSprite = "GUI/PNG/Character Panel/Stats_Box";
        public const string RedRibbonSprite = "GUI/PNG/Character Panel/Red_Ribbon";
        public const string BlueRibbonSprite = "GUI/PNG/Character Panel/Blue_Ribbon";
        public const string QuestBoxSprite = "GUI/PNG/Quest Panel/Quest_Box";
        public const string QuestProgressSlotSprite = "GUI/PNG/Quest Panel/Progress_Slot";
        public const string QuestRewardSlotSprite = "GUI/PNG/Quest Panel/Reward_Slot";
        public const string AcceptIconSprite = "GUI/PNG/Icons/Accept_Icon";
        public const string SwordIconSprite = "GUI/PNG/Icons/Sword_Icon";
        public const string ShieldIconSprite = "GUI/PNG/Icons/Shield_Icon";

        private static readonly Color ParchmentText = new(0.28f, 0.14f, 0.07f, 1f);
        private static readonly Color CreamText = new(1f, 0.91f, 0.69f, 1f);
        private static readonly Color HeaderText = new(1f, 0.89f, 0.56f, 1f);
        private static readonly Dictionary<string, Sprite> CachedSprites = new();
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
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                ConfigureInputModule(existing);
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            ConfigureInputModule(eventSystem.AddComponent<EventSystem>());
        }

        private static void ConfigureInputModule(EventSystem eventSystem)
        {
            if (eventSystem == null)
            {
                return;
            }

            var modules = eventSystem.GetComponents<BaseInputModule>();
            for (var i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null || modules[i] is InputSystemUIInputModule)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(modules[i]);
                }
                else
                {
                    Object.DestroyImmediate(modules[i]);
                }
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (inputModule.actionsAsset == null)
            {
                inputModule.AssignDefaultActions();
            }
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

        public static Image CreateSpriteImage(
            Transform parent,
            string name,
            string spritePath,
            Color color,
            Image.Type type = Image.Type.Simple,
            bool preserveAspect = false)
        {
            var image = CreateImage(parent, name, color);
            ApplySprite(image, spritePath, color, type, preserveAspect);
            return image;
        }

        public static Image ApplySprite(
            Image image,
            string spritePath,
            Color color,
            Image.Type type = Image.Type.Sliced,
            bool preserveAspect = false)
        {
            if (image == null)
            {
                return null;
            }

            image.sprite = LoadSprite(spritePath);
            image.type = image.sprite != null ? type : Image.Type.Simple;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = true;
            return image;
        }

        public static Image StyleMenuPanel(GameObject target)
        {
            return ApplySprite(EnsureImage(target), MenuBoxSprite, Color.white, Image.Type.Sliced);
        }

        public static Image StyleInventoryPanel(GameObject target)
        {
            return ApplySprite(EnsureImage(target), InventoryBoxSprite, Color.white, Image.Type.Sliced);
        }

        public static Image StyleQuestPanel(GameObject target)
        {
            return ApplySprite(EnsureImage(target), QuestBoxSprite, Color.white, Image.Type.Sliced);
        }

        public static Image StyleCharacterPanel(GameObject target)
        {
            return ApplySprite(EnsureImage(target), CharacterPanelSprite, Color.white, Image.Type.Sliced);
        }

        public static Image StyleSection(GameObject target)
        {
            return ApplySprite(EnsureImage(target), MenuBoxSprite, new Color(0.93f, 0.78f, 0.50f, 0.94f), Image.Type.Sliced);
        }

        public static Image StyleHeader(GameObject target)
        {
            return ApplySprite(EnsureImage(target), InventoryTitleSprite, Color.white, Image.Type.Sliced);
        }

        public static Image StyleInput(GameObject target)
        {
            return ApplySprite(EnsureImage(target), LoginInputSprite, Color.white, Image.Type.Sliced);
        }

        public static Image EnsureImage(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            var image = target.GetComponent<Image>();
            return image != null ? image : target.AddComponent<Image>();
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
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.55f));
            label.resizeTextMaxSize = size;
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string label, UnityAction onClick, Vector2 preferredSize)
        {
            var buttonObject = CreateUiObject(parent, name);
            var shadow = CreateSpriteImage(buttonObject.transform, "Shadow", MenuButtonShadowSprite, Color.white, Image.Type.Sliced);
            shadow.raycastTarget = false;
            Stretch(shadow.gameObject, new Vector2(0f, -7f), new Vector2(0f, -7f));

            var face = CreateSpriteImage(buttonObject.transform, "Face", MenuButtonSprite, Color.white, Image.Type.Sliced);
            Stretch(face.gameObject, Vector2.zero, Vector2.zero);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.colors = CreateButtonColors(false);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredSize.x;
            layout.preferredHeight = preferredSize.y;
            layout.minHeight = preferredSize.y;

            var text = CreateText(buttonObject.transform, "Label", label, 28, CreamText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.gameObject, new Vector2(24f, 6f), new Vector2(-24f, -8f));
            return button;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 preferredSize)
        {
            var inputObject = CreateUiObject(parent, name);
            StyleInput(inputObject);

            var input = inputObject.AddComponent<InputField>();
            input.textComponent = CreateText(inputObject.transform, "Text", string.Empty, 26, ParchmentText, TextAnchor.MiddleLeft);
            Stretch(input.textComponent.gameObject, new Vector2(24f, 7f), new Vector2(-24f, -7f));

            var placeholderText = CreateText(inputObject.transform, "Placeholder", placeholder, 24, new Color(0.48f, 0.29f, 0.15f, 0.70f), TextAnchor.MiddleLeft);
            Stretch(placeholderText.gameObject, new Vector2(24f, 7f), new Vector2(-24f, -7f));
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

            var background = CreateSpriteImage(sliderObject.transform, "Background", LoadingBarBackgroundSprite, Color.white, Image.Type.Sliced);
            var backgroundRect = Stretch(background.gameObject, Vector2.zero, Vector2.zero);
            backgroundRect.anchorMin = new Vector2(0f, 0.22f);
            backgroundRect.anchorMax = new Vector2(1f, 0.78f);

            var fillArea = CreateUiObject(sliderObject.transform, "Fill Area");
            var fillAreaRect = Stretch(fillArea, new Vector2(27f, 0f), new Vector2(-27f, 0f));
            fillAreaRect.anchorMin = new Vector2(0f, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.65f);

            var fill = CreateSpriteImage(fillArea.transform, "Fill", LoadingBarFillSprite, Color.white, Image.Type.Filled);
            Stretch(fill.gameObject, Vector2.zero, Vector2.zero);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = Mathf.Clamp01(value);

            var handleArea = CreateUiObject(sliderObject.transform, "Handle Slide Area");
            var handleAreaRect = Stretch(handleArea, new Vector2(24f, 0f), new Vector2(-24f, 0f));
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;

            var handle = CreateSpriteImage(handleArea.transform, "Handle", QuestRewardSlotSprite, Color.white, Image.Type.Simple, true);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(42f, 42f);

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
                    ? new Color(1f, 0.92f, 0.62f, 1f)
                    : Color.white;
            }
        }

        public static Text CreateHeader(Transform parent, string name, string text, int size = 38)
        {
            var header = CreateUiObject(parent, name);
            StyleHeader(header);
            var label = CreateText(header.transform, "Label", text, size, HeaderText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.gameObject, new Vector2(36f, 4f), new Vector2(-36f, -4f));
            return label;
        }

        public static GameObject CreateInventorySlot(Transform parent, string name, Sprite itemSprite = null)
        {
            var slot = CreateUiObject(parent, name);
            ApplySprite(EnsureImage(slot), InventorySlotSprite, Color.white, Image.Type.Sliced);

            if (itemSprite != null)
            {
                var icon = CreateImage(slot.transform, "Item Icon", Color.white);
                icon.sprite = itemSprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Stretch(icon.gameObject, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            }

            return slot;
        }

        public static Button CreateCloseButton(Transform parent, string name, UnityAction onClick)
        {
            var closeObject = CreateUiObject(parent, name);
            var image = closeObject.AddComponent<Image>();
            ApplySprite(image, CloseIconSprite, Color.white, Image.Type.Simple, true);
            var button = closeObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = CreateIconButtonColors();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        public static GameObject CreateLoadingProgressBar(
            Transform parent,
            string name,
            out Image fillImage,
            out RectTransform fillRect,
            float preferredHeight = 66f)
        {
            var bar = CreateUiObject(parent, name);
            AddLayoutElement(bar, 0f, preferredHeight);

            var background = CreateSpriteImage(bar.transform, "Background", LoadingBarBackgroundSprite, Color.white, Image.Type.Sliced);
            Stretch(background.gameObject, Vector2.zero, Vector2.zero);

            var fill = CreateSpriteImage(bar.transform, "Fill", LoadingBarFillSprite, Color.white, Image.Type.Filled);
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            fillRect = Stretch(fill.gameObject, new Vector2(28f, 20f), new Vector2(-28f, -20f));
            fillImage = fill;

            return bar;
        }

        public static void SetProgressFill(Image fillImage, RectTransform fillRect, float normalized)
        {
            normalized = Mathf.Clamp01(normalized);
            if (fillImage != null)
            {
                fillImage.enabled = normalized > 0.001f;
                fillImage.fillAmount = normalized;
            }

            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0f, fillRect.anchorMin.y);
                fillRect.anchorMax = new Vector2(1f, fillRect.anchorMax.y);
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
            var normal = selected ? new Color(1f, 0.92f, 0.62f, 1f) : Color.white;

            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = selected ? new Color(1f, 0.97f, 0.76f, 1f) : new Color(1f, 0.91f, 0.78f, 1f),
                pressedColor = new Color(0.82f, 0.64f, 0.43f, 1f),
                selectedColor = new Color(1f, 0.91f, 0.60f, 1f),
                disabledColor = new Color(0.52f, 0.43f, 0.38f, 0.70f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static ColorBlock CreateIconButtonColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.90f, 0.70f, 1f),
                pressedColor = new Color(0.78f, 0.56f, 0.42f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        public static Sprite LoadSprite(string resourcesPath)
        {
            if (string.IsNullOrWhiteSpace(resourcesPath))
            {
                return null;
            }

            if (CachedSprites.TryGetValue(resourcesPath, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = Resources.Load<Sprite>(resourcesPath);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(resourcesPath);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 1, SpriteMeshType.FullRect);
                }
            }

            CachedSprites[resourcesPath] = sprite;
            return sprite;
        }

        private static Font ResolveFont()
        {
            if (cachedFont != null)
            {
                return cachedFont;
            }

            cachedFont = Resources.Load<Font>("GUI/Font/MedievalSharp");
            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (cachedFont == null)
            {
                cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return cachedFont;
        }
    }
}
