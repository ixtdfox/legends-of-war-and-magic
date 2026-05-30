using LegendsOfWarAndMagic.ProceduralGeneration;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Game.World.Presentation
{
    internal sealed class RuntimeLocationMapOverlay
    {
        private const int PlayerMarkerSpriteWidth = 32;
        private const int PlayerMarkerSpriteHeight = 52;

        private static Sprite playerMarkerSprite;

        private readonly Image mapImage;
        private readonly Transform markerRoot;
        private readonly RectTransform overlayRect;

        private RectTransform playerMarker;
        private Bounds currentBounds;
        private Rect currentContentRect;

        public RuntimeLocationMapOverlay(Image mapImage, Transform markerRoot)
        {
            this.mapImage = mapImage;
            this.markerRoot = markerRoot;
            overlayRect = markerRoot != null ? markerRoot.GetComponent<RectTransform>() : null;
        }

        public void Refresh(ProceduralLocationGenerator generator)
        {
            Clear();

            var layers = generator != null ? generator.GeneratedWorldLayers : null;
            var settings = generator != null ? generator.CurrentSettings : null;
            if (settings == null || mapImage == null || markerRoot == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            currentBounds = settings.GetWorldBounds();
            currentContentRect = ResolveImageContentRect(mapImage);

            if (layers != null)
            {
                AddRoadMarkers(layers, currentBounds, currentContentRect);
                AddSettlementBuildingMarkers(layers, currentBounds, currentContentRect);
            }

            AddPlayerMarker();
            UpdatePlayerMarker();
        }

        public void UpdatePlayerMarker()
        {
            if (playerMarker == null || overlayRect == null)
            {
                return;
            }

            var player = ResolvePlayerTransform();
            if (player == null)
            {
                playerMarker.gameObject.SetActive(false);
                return;
            }

            playerMarker.gameObject.SetActive(true);
            var worldPosition = new Vector2(player.position.x, player.position.z);
            playerMarker.anchoredPosition = WorldToMapPosition(worldPosition, currentBounds, currentContentRect);
            playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
            playerMarker.SetAsLastSibling();
        }

        private void Clear()
        {
            if (markerRoot == null)
            {
                return;
            }

            for (var i = markerRoot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(markerRoot.GetChild(i).gameObject);
            }

            playerMarker = null;
        }

        private void AddRoadMarkers(WorldGenerationLayers layers, Bounds bounds, Rect contentRect)
        {
            if (layers?.RoadNetwork?.Segments != null)
            {
                for (var i = 0; i < layers.RoadNetwork.Segments.Count; i++)
                {
                    AddRoadPolyline(layers.RoadNetwork.Segments[i], bounds, contentRect);
                }
            }

            if (layers?.Settlements == null)
            {
                return;
            }

            for (var settlementIndex = 0; settlementIndex < layers.Settlements.Count; settlementIndex++)
            {
                var settlement = layers.Settlements[settlementIndex];
                for (var roadIndex = 0; roadIndex < settlement.InternalRoads.Count; roadIndex++)
                {
                    AddInternalRoadPolyline(settlement.InternalRoads[roadIndex], bounds, contentRect);
                }
            }
        }

        private void AddRoadPolyline(GeneratedRoadSegment segment, Bounds bounds, Rect contentRect)
        {
            if (segment == null || segment.Points.Count < 2)
            {
                return;
            }

            var color = RoadMapColor(segment.Type);
            var thickness = Mathf.Clamp(segment.Width * PixelsPerMeter(bounds, contentRect) * 1.35f, 2f, 8f);
            for (var i = 1; i < segment.Points.Count; i++)
            {
                AddLine(
                    $"Road {segment.Id} {i}",
                    WorldToMapPosition(segment.Points[i - 1], bounds, contentRect),
                    WorldToMapPosition(segment.Points[i], bounds, contentRect),
                    thickness,
                    color);
            }
        }

        private void AddInternalRoadPolyline(GeneratedSettlementRoadSegment segment, Bounds bounds, Rect contentRect)
        {
            if (segment == null || segment.Points.Count < 2)
            {
                return;
            }

            var thickness = Mathf.Clamp(segment.Width * PixelsPerMeter(bounds, contentRect), 1.5f, 5f);
            var color = segment.Primary
                ? new Color(0.72f, 0.46f, 0.18f, 0.86f)
                : new Color(0.60f, 0.38f, 0.16f, 0.68f);
            for (var i = 1; i < segment.Points.Count; i++)
            {
                AddLine(
                    $"GeneratedSettlement Road {segment.Id} {i}",
                    WorldToMapPosition(segment.Points[i - 1], bounds, contentRect),
                    WorldToMapPosition(segment.Points[i], bounds, contentRect),
                    thickness,
                    color);
            }
        }

        private void AddSettlementBuildingMarkers(WorldGenerationLayers layers, Bounds bounds, Rect contentRect)
        {
            if (layers?.Settlements == null)
            {
                return;
            }

            var pixelsPerMeter = PixelsPerMeter(bounds, contentRect);
            for (var settlementIndex = 0; settlementIndex < layers.Settlements.Count; settlementIndex++)
            {
                var settlement = layers.Settlements[settlementIndex];
                for (var buildingIndex = 0; buildingIndex < settlement.Buildings.Count; buildingIndex++)
                {
                    AddBuildingMarker(settlement.Buildings[buildingIndex], bounds, contentRect, pixelsPerMeter);
                }
            }
        }

        private void AddBuildingMarker(GeneratedSettlementBuilding building, Bounds bounds, Rect contentRect, float pixelsPerMeter)
        {
            var position = WorldToMapPosition(building.WorldPosition, bounds, contentRect);
            var markerObject = RuntimeUiFactory.CreateUiObject(markerRoot, $"Building {building.Id}");
            var rect = markerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(
                Mathf.Clamp(building.FootprintSize.x * pixelsPerMeter, 3f, 18f),
                Mathf.Clamp(building.FootprintSize.y * pixelsPerMeter, 3f, 18f));
            rect.localRotation = Quaternion.Euler(0f, 0f, -building.RotationDegrees);

            var image = markerObject.AddComponent<Image>();
            image.color = BuildingMapColor(building.Definition.Type);
            image.raycastTarget = false;
        }

        private void AddPlayerMarker()
        {
            if (markerRoot == null)
            {
                return;
            }

            var markerObject = RuntimeUiFactory.CreateUiObject(markerRoot, "Current Player Marker");
            playerMarker = markerObject.GetComponent<RectTransform>();
            playerMarker.anchorMin = new Vector2(0.5f, 0.5f);
            playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
            playerMarker.pivot = new Vector2(0.5f, 0.5f);
            playerMarker.sizeDelta = new Vector2(22f, 36f);

            var image = markerObject.AddComponent<Image>();
            image.sprite = ResolvePlayerMarkerSprite();
            image.color = new Color(0.95f, 0.12f, 0.07f, 1f);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var outlineObject = RuntimeUiFactory.CreateUiObject(markerObject.transform, "Outline");
            var outlineRect = outlineObject.GetComponent<RectTransform>();
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = new Vector2(-2f, -2f);
            outlineRect.offsetMax = new Vector2(2f, 2f);
            outlineRect.SetAsFirstSibling();
            var outline = outlineObject.AddComponent<Image>();
            outline.sprite = ResolvePlayerMarkerSprite();
            outline.color = new Color(1f, 1f, 0.86f, 0.92f);
            outline.type = Image.Type.Simple;
            outline.preserveAspect = true;
            outline.raycastTarget = false;
        }

        private void AddLine(string name, Vector2 from, Vector2 to, float thickness, Color color)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length <= 0.1f)
            {
                return;
            }

            var lineObject = RuntimeUiFactory.CreateUiObject(markerRoot, name);
            var rect = lineObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(length, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            var image = lineObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static Transform ResolvePlayerTransform()
        {
            GameObject taggedPlayer = null;
            try
            {
                taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            }
            catch (UnityException)
            {
                taggedPlayer = null;
            }

            if (taggedPlayer != null)
            {
                return taggedPlayer.transform;
            }

            return UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform.root : null;
        }

        private static Vector2 WorldToMapPosition(Vector2 worldPosition, Bounds bounds, Rect contentRect)
        {
            var normalized = new Vector2(
                Mathf.InverseLerp(bounds.min.x, bounds.max.x, worldPosition.x),
                Mathf.InverseLerp(bounds.min.z, bounds.max.z, worldPosition.y));
            normalized.x = Mathf.Clamp01(normalized.x);
            normalized.y = Mathf.Clamp01(normalized.y);
            return new Vector2(
                contentRect.xMin + normalized.x * contentRect.width,
                contentRect.yMin + normalized.y * contentRect.height);
        }

        private static Rect ResolveImageContentRect(Image image)
        {
            var rectTransform = image.GetComponent<RectTransform>();
            var rect = rectTransform.rect;
            if (!image.preserveAspect || image.sprite == null)
            {
                return rect;
            }

            var spriteRect = image.sprite.rect;
            if (spriteRect.height <= 0f || rect.height <= 0f)
            {
                return rect;
            }

            var spriteAspect = spriteRect.width / spriteRect.height;
            var rectAspect = rect.width / rect.height;
            if (rectAspect > spriteAspect)
            {
                var contentWidth = rect.height * spriteAspect;
                return new Rect(
                    rect.xMin + (rect.width - contentWidth) * 0.5f,
                    rect.yMin,
                    contentWidth,
                    rect.height);
            }

            var contentHeight = rect.width / spriteAspect;
            return new Rect(
                rect.xMin,
                rect.yMin + (rect.height - contentHeight) * 0.5f,
                rect.width,
                contentHeight);
        }

        private static float PixelsPerMeter(Bounds bounds, Rect contentRect)
        {
            var widthScale = contentRect.width / Mathf.Max(1f, bounds.size.x);
            var heightScale = contentRect.height / Mathf.Max(1f, bounds.size.z);
            return Mathf.Max(0.01f, (widthScale + heightScale) * 0.5f);
        }

        private static Color RoadMapColor(RoadType type)
        {
            return type switch
            {
                RoadType.Trail => new Color(0.34f, 0.22f, 0.09f, 0.76f),
                RoadType.HiddenPath => new Color(0.18f, 0.23f, 0.12f, 0.54f),
                RoadType.MainRoad => new Color(0.54f, 0.30f, 0.10f, 0.92f),
                RoadType.StoneRoad => new Color(0.38f, 0.36f, 0.31f, 0.92f),
                _ => new Color(0.43f, 0.25f, 0.10f, 0.84f)
            };
        }

        private static Color BuildingMapColor(BuildingType type)
        {
            return type switch
            {
                BuildingType.TownHall => new Color(0.88f, 0.62f, 0.18f, 0.95f),
                BuildingType.Market => new Color(0.76f, 0.40f, 0.14f, 0.92f),
                BuildingType.Temple or BuildingType.Shrine => new Color(0.78f, 0.72f, 0.38f, 0.94f),
                BuildingType.Wall or BuildingType.Gate or BuildingType.Watchtower => new Color(0.26f, 0.18f, 0.10f, 0.82f),
                BuildingType.Blacksmith or BuildingType.Workshop => new Color(0.24f, 0.24f, 0.22f, 0.88f),
                _ => new Color(0.58f, 0.30f, 0.14f, 0.82f)
            };
        }

        private static Sprite ResolvePlayerMarkerSprite()
        {
            if (playerMarkerSprite != null)
            {
                return playerMarkerSprite;
            }

            var texture = new Texture2D(PlayerMarkerSpriteWidth, PlayerMarkerSpriteHeight, TextureFormat.RGBA32, false)
            {
                name = "Runtime Player Direction Marker",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };

            var clear = new Color(1f, 1f, 1f, 0f);
            var fill = Color.white;
            var centerX = (PlayerMarkerSpriteWidth - 1) * 0.5f;
            var halfBase = (PlayerMarkerSpriteWidth - 1) * 0.45f;
            for (var y = 0; y < PlayerMarkerSpriteHeight; y++)
            {
                var t = y / (float)(PlayerMarkerSpriteHeight - 1);
                var halfWidth = Mathf.Lerp(halfBase, 1.2f, t);
                for (var x = 0; x < PlayerMarkerSpriteWidth; x++)
                {
                    var edge = Mathf.Abs(x - centerX);
                    var alpha = Mathf.Clamp01(halfWidth + 0.65f - edge);
                    texture.SetPixel(x, y, alpha > 0f ? new Color(fill.r, fill.g, fill.b, alpha) : clear);
                }
            }

            texture.Apply(false, true);
            playerMarkerSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, PlayerMarkerSpriteWidth, PlayerMarkerSpriteHeight),
                new Vector2(0.5f, 0.5f),
                100f);
            playerMarkerSprite.name = "Runtime Player Direction Marker Sprite";
            playerMarkerSprite.hideFlags = HideFlags.HideAndDontSave;
            return playerMarkerSprite;
        }
    }
}
