using System.Collections.Generic;
using System.IO;
using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Unity;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Game.World.Presentation
{
    [DisallowMultipleComponent]
    public sealed class RuntimeMapHud : MonoBehaviour
    {
        private GameObject worldMapPanel;
        private GameObject locationMapPanel;
        private Image worldMapImage;
        private Image locationMapImage;
        private Transform markerRoot;
        private readonly List<Sprite> loadedSprites = new();
        private readonly LocationSceneLoader sceneLoader = new();

        public static RuntimeMapHud Ensure()
        {
            var existing = FindFirstObjectByType<RuntimeMapHud>();
            if (existing != null)
            {
                return existing;
            }

            var obj = new GameObject("Runtime Map HUD");
            var hud = obj.AddComponent<RuntimeMapHud>();
            hud.BuildUi();
            return hud;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
            {
                ToggleLocationMap();
            }

            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                ShowWorldMap();
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HidePanel(worldMapPanel);
                HidePanel(locationMapPanel);
            }
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Runtime Map HUD Canvas");
            canvas.sortingOrder = 5200;
            canvas.transform.SetParent(transform, false);

            var buttonRow = RuntimeUiFactory.CreateUiObject(canvas.transform, "Map Buttons");
            var rowRect = buttonRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -24f);
            rowRect.sizeDelta = new Vector2(540f, 64f);
            RuntimeUiFactory.AddHorizontalLayout(buttonRow, 14f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            RuntimeUiFactory.CreateButton(buttonRow.transform, "World Map Button", "Карта мира", ShowWorldMap, new Vector2(230f, 58f));
            RuntimeUiFactory.CreateButton(buttonRow.transform, "Location Map Button", "Карта локации", ToggleLocationMap, new Vector2(250f, 58f));

            worldMapPanel = BuildMapPanel(canvas.transform, "World Map Panel", out worldMapImage, out markerRoot);
            locationMapPanel = BuildMapPanel(canvas.transform, "Location Map Panel", out locationMapImage, out _);
            worldMapPanel.SetActive(false);
            locationMapPanel.SetActive(false);
        }

        private static GameObject BuildMapPanel(Transform parent, string name, out Image mapImage, out Transform overlayRoot)
        {
            var panel = RuntimeUiFactory.CreateUiObject(parent, name);
            RuntimeUiFactory.Stretch(panel, Vector2.zero, Vector2.zero);
            var background = panel.AddComponent<Image>();
            background.color = new Color(0.03f, 0.035f, 0.03f, 0.90f);

            var mapObject = RuntimeUiFactory.CreateUiObject(panel.transform, "Map Image");
            var mapRect = mapObject.GetComponent<RectTransform>();
            mapRect.anchorMin = new Vector2(0.5f, 0.5f);
            mapRect.anchorMax = new Vector2(0.5f, 0.5f);
            mapRect.pivot = new Vector2(0.5f, 0.5f);
            mapRect.sizeDelta = new Vector2(1320f, 860f);
            mapRect.anchoredPosition = new Vector2(0f, -10f);
            mapImage = mapObject.AddComponent<Image>();
            mapImage.color = Color.white;
            mapImage.preserveAspect = true;

            var overlay = RuntimeUiFactory.CreateUiObject(mapObject.transform, "Marker Overlay");
            RuntimeUiFactory.Stretch(overlay, Vector2.zero, Vector2.zero);
            overlayRoot = overlay.transform;

            var close = RuntimeUiFactory.CreateButton(panel.transform, "Close Button", "Закрыть", () => HideMapPanel(panel), new Vector2(210f, 60f));
            var closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-28f, -24f);
            return panel;
        }

        private void ShowWorldMap()
        {
            GeneratedWorldRuntimeState.TryRestoreSession();
            var world = GeneratedWorldSession.CurrentWorld;
            if (world == null)
            {
                return;
            }

            SetImage(worldMapImage, world.Map.ImagePath);
            ClearChildren(markerRoot);
            foreach (var location in world.Locations)
            {
                AddWorldMarker(location);
            }

            worldMapPanel.SetActive(true);
            RuntimeInputBlocker.SetBlocked(worldMapPanel, true);
        }

        private void ToggleLocationMap()
        {
            if (locationMapPanel.activeSelf)
            {
                HidePanel(locationMapPanel);
                return;
            }

            GeneratedWorldRuntimeState.TryRestoreSession();
            var location = GeneratedWorldSession.CurrentLocation;
            if (location == null)
            {
                return;
            }

            SetImage(locationMapImage, location.Map.ImagePath);
            locationMapPanel.SetActive(true);
            RuntimeInputBlocker.SetBlocked(locationMapPanel, true);
        }

        private static void HideMapPanel(GameObject panel)
        {
            if (panel == null)
            {
                return;
            }

            panel.SetActive(false);
            RuntimeInputBlocker.SetBlocked(panel, false);
        }

        private void HidePanel(GameObject panel)
        {
            HideMapPanel(panel);
        }

        private void AddWorldMarker(WorldLocation location)
        {
            var markerObject = RuntimeUiFactory.CreateUiObject(markerRoot, $"{location.Name.Value} Marker");
            var rect = markerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(location.WorldMapPosition.X, location.WorldMapPosition.Y);
            rect.anchorMax = new Vector2(location.WorldMapPosition.X, location.WorldMapPosition.Y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = location.IsStartLocation ? new Vector2(44f, 44f) : new Vector2(34f, 34f);
            rect.anchoredPosition = Vector2.zero;

            var image = markerObject.AddComponent<Image>();
            image.color = location.IsStartLocation
                ? new Color(0.95f, 0.74f, 0.22f, 0.98f)
                : new Color(0.16f, 0.09f, 0.04f, 0.95f);
            var button = markerObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                LocationTransitionPromptUI.Ensure().Show(
                    location.Name.Value,
                    () => sceneLoader.EnterLocation(location.Id, GeneratedWorldSession.CurrentLocation?.Id ?? location.Id, WorldDirection.South));
            });

            var label = RuntimeUiFactory.CreateText(markerObject.transform, "Label", location.Name.Value, 18, new Color(0.08f, 0.06f, 0.04f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(1f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(210f, 32f);
            labelRect.anchoredPosition = new Vector2(8f, 0f);
        }

        private void SetImage(Image target, string path)
        {
            if (target == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            var bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(bytes);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            loadedSprites.Add(sprite);
            target.sprite = sprite;
            target.color = Color.white;
        }

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
