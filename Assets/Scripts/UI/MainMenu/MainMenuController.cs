using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Unity;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.Generator.Location;
using LegendsOfWarAndMagic.Generator.World;
using LegendsOfWarAndMagic.SceneManagement;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.UI.MainMenu
{
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        private GameObject mainPanel;
        private GameObject settingsPanel;
        private GameObject loadPanel;
        private GameObject loadListRoot;
        private GameObject progressPanel;
        private Text progressText;
        private Image progressFill;
        private RectTransform progressFillRect;
        private Text progressPercentText;
        private Button newGameButton;
        private bool generationInProgress;
        private bool wasMousePressed;

        private void Awake()
        {
            BuildUi();
        }

        private void BuildUi()
        {
            var canvas = RuntimeUiFactory.CreateCanvas("Main Menu Canvas");
            var background = RuntimeUiFactory.CreateImage(canvas.transform, "Background", new Color(0.055f, 0.072f, 0.078f, 1f));
            RuntimeUiFactory.Stretch(background.gameObject, Vector2.zero, Vector2.zero);

            var decoration = RuntimeUiFactory.CreateSpriteImage(
                canvas.transform,
                "Fantasy Backdrop",
                RuntimeUiFactory.LoadingDecorationSprite,
                new Color(1f, 0.82f, 0.54f, 0.24f),
                Image.Type.Simple,
                true);
            var decorationRect = decoration.GetComponent<RectTransform>();
            decorationRect.anchorMin = new Vector2(0.5f, 0.5f);
            decorationRect.anchorMax = new Vector2(0.5f, 0.5f);
            decorationRect.pivot = new Vector2(0.5f, 0.5f);
            decorationRect.anchoredPosition = new Vector2(0f, 0f);
            decorationRect.sizeDelta = new Vector2(1420f, 1060f);
            decoration.raycastTarget = false;

            mainPanel = RuntimeUiFactory.CreateUiObject(canvas.transform, "Main Menu Panel");
            var panelRect = mainPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(720f, 720f);
            RuntimeUiFactory.StyleMenuPanel(mainPanel);
            RuntimeUiFactory.AddVerticalLayout(mainPanel, 24f, new RectOffset(82, 82, 56, 56));

            var title = RuntimeUiFactory.CreateText(mainPanel.transform, "Title", "Legends of War and Magic", 54, new Color(0.44f, 0.10f, 0.04f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(title.gameObject, 0f, 138f);

            AddMenuButton("Новая игра", StartNewGame);
            AddMenuButton("Загрузить мир", OpenLoadWorlds);
            AddMenuButton("Настройки", OpenSettings);
            AddMenuButton("Выйти", QuitGame);
            AddPinnedLoadButton(canvas.transform);

            BuildSettingsPanel(canvas.transform);
            BuildLoadPanel(canvas.transform);
            BuildProgressPanel(canvas.transform);
            settingsPanel.SetActive(false);
            loadPanel.SetActive(false);
            progressPanel.SetActive(false);
        }

        private void Update()
        {
            if (generationInProgress || newGameButton == null || !newGameButton.gameObject.activeInHierarchy)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var isMousePressed = mouse.leftButton.isPressed;
            var pressedNow = isMousePressed && !wasMousePressed;
            wasMousePressed = isMousePressed;
            if (!pressedNow)
            {
                return;
            }

            var rect = newGameButton.GetComponent<RectTransform>();
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue()))
            {
                StartNewGame();
            }
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

            RuntimeUiFactory.StyleMenuPanel(settingsPanel);

            RuntimeUiFactory.AddVerticalLayout(settingsPanel, 26f, new RectOffset(76, 76, 54, 54));

            var title = RuntimeUiFactory.CreateHeader(settingsPanel.transform, "Settings Header", "Настройки", 40);
            RuntimeUiFactory.AddLayoutElement(title.transform.parent.gameObject, 0f, 72f);

            var message = RuntimeUiFactory.CreateText(settingsPanel.transform, "Settings Message", "Настройки появятся позже", 32, new Color(0.32f, 0.17f, 0.08f, 1f));
            RuntimeUiFactory.AddLayoutElement(message.gameObject, 0f, 130f);

            RuntimeUiFactory.CreateButton(settingsPanel.transform, "Back Button", "Назад", CloseSettings, new Vector2(340f, 72f));
        }

        private void AddMenuButton(string label, UnityEngine.Events.UnityAction action)
        {
            var button = RuntimeUiFactory.CreateButton(mainPanel.transform, $"{label} Button", label, action, new Vector2(420f, 78f));
            if (label == "Новая игра")
            {
                newGameButton = button;
            }
        }

        private void AddPinnedLoadButton(Transform canvasTransform)
        {
            var button = RuntimeUiFactory.CreateButton(canvasTransform, "Pinned Load World Button", "Загрузить мир", OpenLoadWorlds, new Vector2(250f, 58f));
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
        }

        private void StartNewGame()
        {
            if (generationInProgress)
            {
                return;
            }

            Debug.Log("Main menu: New Game requested.");
            StartCoroutine(GenerateNewWorldRoutine());
        }

        private IEnumerator GenerateNewWorldRoutine()
        {
            generationInProgress = true;
            mainPanel.SetActive(false);
            settingsPanel.SetActive(false);
            loadPanel.SetActive(false);
            progressPanel.SetActive(true);
            SetProgress("Создаём форму мира...", 0.02f);
            yield return null;

            var config = new WorldGenerationConfig
            {
                Seed = Environment.TickCount,
                MinRegions = 8,
                MaxRegions = 20,
                PlayableLocationCount = 15
            };

            var repository = WorldRuntimeServices.CreateRepository();
            var worldGenerator = new TopDownWorldGenerator();
            var worldMapRenderer = new TextureWorldMapRenderer();
            var locationTerrainGenerator = new LegacyLocationTerrainGenerator();
            var locationMapRenderer = new TextureLocationMapRenderer();

            SetProgress("Генерируем структуру мира...", 0.06f);
            yield return null;
            var world = worldGenerator.Generate(config, message => SetProgress(TranslateProgress(message), 0.10f));
            var worldDirectory = repository.GetWorldDirectory(world.Id);

            SetProgress("Рисуем карту мира...", 0.24f);
            yield return null;
            var worldMapPath = Path.Combine(worldDirectory, "Maps", "world_map.png");
            var worldMap = worldMapRenderer.Render(world, worldMapPath, new WorldMapRenderSettings());
            world = world.WithMap(new WorldMap(worldMap.ImagePath, worldMap.Markers));

            var generatedLocations = new List<WorldLocation>();
            var locationIndex = 0;
            var locationStageCount = Mathf.Max(1, world.Locations.Count * 3);
            foreach (var location in world.Locations)
            {
                var stageIndex = locationIndex * 3;
                SetProgress(
                    $"Создаём terrain: {locationIndex + 1} / {world.Locations.Count}",
                    LocationGenerationProgress(stageIndex, locationStageCount));
                yield return null;

                var locationDirectory = Path.Combine(worldDirectory, "Locations", location.Id.Value);
                var terrain = locationTerrainGenerator.Generate(world, location, LocationGenerationConfig.CreateDefault());
                SetProgress(
                    $"Сохраняем terrain: {locationIndex + 1} / {world.Locations.Count}",
                    LocationGenerationProgress(stageIndex + 1, locationStageCount));
                yield return null;

                var terrainPath = terrain.Save(locationDirectory);
                var locationMapPath = Path.Combine(locationDirectory, "location_map.png");
                var locationWithTerrain = location.WithRuntimeAssets(terrainPath, locationMapPath);
                SetProgress(
                    $"Рисуем карту локации: {locationIndex + 1} / {world.Locations.Count}",
                    LocationGenerationProgress(stageIndex + 2, locationStageCount));
                yield return null;

                var locationMap = locationMapRenderer.Render(world, locationWithTerrain, locationMapPath, new LocationMapRenderSettings());
                generatedLocations.Add(location.WithRuntimeAssets(terrainPath, locationMap.ImagePath));
                locationIndex++;
                SetProgress(
                    $"Генерируем локации: {locationIndex} / {world.Locations.Count}",
                    LocationGenerationProgress(locationIndex * 3, locationStageCount));
                yield return null;
            }

            SetProgress("Сохраняем мир...", 0.93f);
            yield return null;
            world = world.WithLocations(generatedLocations);
            repository.Save(world);

            GeneratedWorldSession.Start(world);
            GeneratedWorldRuntimeState.SaveCurrentSession();
            yield return LoadGameSceneWithProgress("Загружаем стартовую локацию...", 0.94f, 1f);
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

        private void BuildLoadPanel(Transform canvasTransform)
        {
            loadPanel = RuntimeUiFactory.CreateUiObject(canvasTransform, "Load World Panel");
            var rect = loadPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 20f);
            rect.sizeDelta = new Vector2(980f, 720f);

            RuntimeUiFactory.StyleMenuPanel(loadPanel);
            RuntimeUiFactory.AddVerticalLayout(loadPanel, 18f, new RectOffset(72, 72, 54, 58));

            var title = RuntimeUiFactory.CreateHeader(loadPanel.transform, "Load Header", "Загрузить мир", 40);
            RuntimeUiFactory.AddLayoutElement(title.transform.parent.gameObject, 0f, 76f);

            loadListRoot = RuntimeUiFactory.CreateUiObject(loadPanel.transform, "Load List");
            RuntimeUiFactory.AddLayoutElement(loadListRoot, 0f, 480f);
            RuntimeUiFactory.AddVerticalLayout(loadListRoot, 12f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

            RuntimeUiFactory.CreateButton(loadPanel.transform, "Load Back Button", "Назад", CloseLoadWorlds, new Vector2(300f, 64f));
        }

        private void BuildProgressPanel(Transform canvasTransform)
        {
            progressPanel = RuntimeUiFactory.CreateUiObject(canvasTransform, "World Generation Progress Panel");
            var rect = progressPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(880f, 360f);
            RuntimeUiFactory.StyleMenuPanel(progressPanel);
            RuntimeUiFactory.AddVerticalLayout(progressPanel, 20f, new RectOffset(72, 72, 48, 48), TextAnchor.MiddleCenter);

            var title = RuntimeUiFactory.CreateHeader(progressPanel.transform, "Progress Header", "Новая игра", 38);
            RuntimeUiFactory.AddLayoutElement(title.transform.parent.gameObject, 0f, 68f);
            progressText = RuntimeUiFactory.CreateText(progressPanel.transform, "Progress Text", string.Empty, 30, new Color(0.30f, 0.16f, 0.08f, 1f), TextAnchor.MiddleCenter);
            RuntimeUiFactory.AddLayoutElement(progressText.gameObject, 0f, 82f);

            RuntimeUiFactory.CreateLoadingProgressBar(progressPanel.transform, "Progress Bar", out progressFill, out progressFillRect, 52f);

            progressPercentText = RuntimeUiFactory.CreateText(progressPanel.transform, "Progress Percent", "0%", 24, new Color(0.48f, 0.25f, 0.10f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(progressPercentText.gameObject, 0f, 42f);
        }

        private void OpenLoadWorlds()
        {
            mainPanel.SetActive(false);
            loadPanel.SetActive(true);
            RefreshLoadWorlds();
        }

        private void CloseLoadWorlds()
        {
            loadPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void RefreshLoadWorlds()
        {
            for (var i = loadListRoot.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(loadListRoot.transform.GetChild(i).gameObject);
            }

            var worlds = WorldRuntimeServices.CreateWorldService().ListSavedWorlds();
            if (worlds.Count == 0)
            {
                var empty = RuntimeUiFactory.CreateText(loadListRoot.transform, "No Worlds", "Сохранённых миров пока нет", 30, new Color(0.30f, 0.16f, 0.08f, 1f));
                RuntimeUiFactory.AddLayoutElement(empty.gameObject, 0f, 90f);
                return;
            }

            foreach (var summary in worlds)
            {
                AddWorldLoadRow(summary);
            }
        }

        private void AddWorldLoadRow(WorldSaveSummary summary)
        {
            var row = RuntimeUiFactory.CreateUiObject(loadListRoot.transform, $"{summary.WorldName} Row");
            RuntimeUiFactory.StyleSection(row);
            RuntimeUiFactory.AddLayoutElement(row, 0f, 92f);
            RuntimeUiFactory.AddHorizontalLayout(row, 18f, new RectOffset(18, 18, 10, 10), TextAnchor.MiddleCenter);

            var labelText = $"{summary.WorldName}\nSeed {summary.Seed} · {summary.ShapeType} · {summary.CreatedUtc:yyyy-MM-dd HH:mm}";
            var label = RuntimeUiFactory.CreateText(row.transform, "World Summary", labelText, 24, new Color(0.29f, 0.15f, 0.07f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(label.gameObject, 600f, 72f);
            RuntimeUiFactory.CreateButton(row.transform, "Load Button", "Загрузить", () => LoadWorld(summary), new Vector2(240f, 62f));
        }

        private void LoadWorld(WorldSaveSummary summary)
        {
            StartCoroutine(LoadWorldRoutine(summary));
        }

        private IEnumerator LoadWorldRoutine(WorldSaveSummary summary)
        {
            mainPanel.SetActive(false);
            loadPanel.SetActive(false);
            settingsPanel.SetActive(false);
            progressPanel.SetActive(true);
            SetProgress("Загружаем мир...", 0.05f);
            yield return null;

            var world = WorldRuntimeServices.CreateLoadWorldUseCase().Execute(new WorldId(summary.WorldId));
            SetProgress("Восстанавливаем карту и локации...", 0.34f);
            yield return null;

            GeneratedWorldSession.Start(world);
            GeneratedWorldRuntimeState.SaveCurrentSession();
            var startLocation = world.StartLocation?.Name.Value ?? "стартовую локацию";
            yield return LoadGameSceneWithProgress($"Загружаем «{startLocation}»...", 0.55f, 1f);
        }

        private IEnumerator LoadGameSceneWithProgress(string message, float startProgress, float endProgress)
        {
            SetProgress(message, startProgress);
            yield return null;

            var operation = SceneManager.LoadSceneAsync(SceneNames.GameScene);
            if (operation == null)
            {
                yield break;
            }

            while (!operation.isDone)
            {
                var sceneProgress = Mathf.Clamp01(operation.progress / 0.9f);
                SetProgress(message, Mathf.Lerp(startProgress, endProgress, sceneProgress));
                yield return null;
            }

            SetProgress(message, endProgress);
            yield return null;
        }

        private static float LocationGenerationProgress(int stageIndex, int stageCount)
        {
            return 0.32f + Mathf.Clamp01(stageIndex / (float)Mathf.Max(1, stageCount)) * 0.56f;
        }

        private void SetProgress(string message, float progress = -1f)
        {
            if (progressText != null)
            {
                progressText.text = message;
            }

            if (progress < 0f)
            {
                return;
            }

            var normalized = Mathf.Clamp01(progress);
            RuntimeUiFactory.SetProgressFill(progressFill, progressFillRect, normalized);

            if (progressPercentText != null)
            {
                progressPercentText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            }

            Canvas.ForceUpdateCanvases();
        }

        private static string TranslateProgress(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "Генерируем мир...";
            }

            if (message.Contains("shape")) return "Создаём форму мира...";
            if (message.Contains("form")) return "Создаём форму мира...";
            if (message.Contains("mountains")) return "Поднимаем горы...";
            if (message.Contains("climate")) return "Расставляем климат...";
            if (message.Contains("biomes")) return "Расставляем биомы...";
            if (message.Contains("rivers")) return "Прокладываем реки...";
            if (message.Contains("regions")) return "Именуем регионы...";
            if (message.Contains("locations")) return message.StartsWith("Generating locations") ? message.Replace("Generating locations", "Генерируем локации") : "Выбираем локации...";
            if (message.Contains("Connecting")) return "Прокладываем переходы...";
            if (message.Contains("Naming")) return "Именуем регионы и локации...";
            if (message.Contains("Drawing")) return "Рисуем карту мира...";
            if (message.Contains("Saving")) return "Сохраняем мир...";
            return message;
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("Quit requested from main menu.");
#endif
            UnityEngine.Application.Quit();
        }
    }
}
