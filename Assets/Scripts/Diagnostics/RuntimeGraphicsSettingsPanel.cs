using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using LegendsOfWarAndMagic.Game.Player;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Steps;
using LegendsOfWarAndMagic.UI.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace LegendsOfWarAndMagic.Diagnostics
{
    [DisallowMultipleComponent]
    public sealed class RuntimeGraphicsSettingsPanel : MonoBehaviour
    {
        private const float PanelWidth = 660f;
        private const float PanelHeight = 920f;

        private static readonly BindingFlags SerializedFieldFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly HashSet<string> GrassRebuildFields = new(StringComparer.Ordinal)
        {
            "chunkSize",
            "placementSpacing",
            "densityScale",
            "densityGridResolution",
            "grassSeed",
            "macroNoiseScale",
            "microNoiseScale",
            "noiseOctaves",
            "noisePersistence",
            "noiseLacunarity",
            "noiseThresholdLow",
            "noiseThresholdHigh",
            "noiseContrast",
            "nearBladeCount",
            "midBladeCount",
            "slopeFadeStart",
            "slopeFadeEnd",
            "waterFadeStart",
            "waterFadeEnd",
            "heightFadeStart",
            "heightFadeEnd",
            "grassVariationLayerWeight",
            "shoreSuppression",
            "rockSuppression",
            "terrainGrassBoost",
            "terrainVariationBoost",
            "terrainRockSuppression",
            "terrainDetailDensityScale",
            "terrainDetailFallbackDistance"
        };

        private static readonly HashSet<string> HiddenRuntimeFields = new(StringComparer.Ordinal)
        {
            "sourceShadowDistanceScale",
            "minimumSourceShadowDistance",
            "maxHighDetailTrees",
            "maxShadowCastingTrees"
        };

        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool visibleOnStart;

        private readonly Dictionary<string, Vector2> fallbackRanges = new(StringComparer.Ordinal)
        {
            ["drawDistance"] = new Vector2(0f, 200f),
            ["highDetailDistance"] = new Vector2(0f, 80f),
            ["chunkSize"] = new Vector2(4f, 32f),
            ["placementSpacing"] = new Vector2(0.2f, 2f),
            ["maxVisibleClumps"] = new Vector2(0f, 70000f),
            ["nearDistance"] = new Vector2(0f, 48f),
            ["midDistance"] = new Vector2(0f, 90f),
            ["farVisualDistance"] = new Vector2(0f, 220f),
            ["maxVisibleGrassTriangles"] = new Vector2(0f, 300000f),
            ["maxVisibleNearInstances"] = new Vector2(0f, 40000f),
            ["maxVisibleMidInstances"] = new Vector2(0f, 50000f),
            ["densityGridResolution"] = new Vector2(32f, 1024f),
            ["grassSeed"] = new Vector2(-10000f, 10000f),
            ["macroNoiseScale"] = new Vector2(1f, 140f),
            ["microNoiseScale"] = new Vector2(0.5f, 24f),
            ["lodFadeDistance"] = new Vector2(0f, 32f),
            ["terrainDetailFallbackDistance"] = new Vector2(0f, 160f),
            ["lod0Distance"] = new Vector2(0f, 120f),
            ["lod1Distance"] = new Vector2(0f, 220f),
            ["lod2Distance"] = new Vector2(0f, 320f),
            ["cullDistance"] = new Vector2(0f, 360f),
            ["shadowDistance"] = new Vector2(0f, 160f),
            ["minimumSourceShadowDistance"] = new Vector2(0f, 80f),
            ["maxHighDetailTrees"] = new Vector2(0f, 700f),
            ["maxShadowCastingTrees"] = new Vector2(0f, 400f)
        };

        private readonly Dictionary<string, string> labelTooltips = new(StringComparer.Ordinal)
        {
            ["Camera FOV"] = "Меняет угол обзора камеры. Больше значение показывает шире, но сильнее искажает перспективу.",
            ["Camera near clip"] = "Минимальная дистанция от камеры, на которой начинается рендер. Слишком большое значение будет срезать ближние объекты.",
            ["Camera far clip"] = "Максимальная дистанция рендера камеры. Меньше значение дешевле, но дальние объекты могут исчезнуть.",
            ["Quality LOD bias"] = "Глобально сдвигает LOD-переходы. Больше значение держит детальные LOD дальше и повышает стоимость кадра.",
            ["Max LOD level"] = "Принудительно запрещает более детальные LOD. Больше значение дешевле, но может заметно упростить объекты.",
            ["Unity shadow distance"] = "Дальность теней Unity. Увеличение резко повышает стоимость shadow passes.",
            ["Shadow cascades"] = "Количество каскадов теней. Больше каскадов улучшает качество дальних теней, но повышает CPU/GPU cost.",
            ["MSAA"] = "Сглаживание геометрических краев. Может быть дорогим в зависимости от render pipeline.",
            ["Texture mip limit"] = "Глобальное ограничение качества текстур. Больше значение использует более низкие mip levels и экономит память/трафик.",
            ["VSync"] = "Синхронизация FPS с монитором. Может скрывать реальные spikes и добавлять input latency.",
            ["Target FPS"] = "Целевой FPS для Application.targetFrameRate. Не снижает стоимость кадра, только ограничивает частоту.",
            ["Terrain draw instanced"] = "Включает instanced terrain rendering. Обычно дешевле для Terrain, если pipeline поддерживает.",
            ["Terrain casts shadows"] = "Разрешает Terrain отбрасывать тени. Может заметно увеличить submitted tris в Unity Stats.",
            ["Heightmap pixel error"] = "LOD точность terrain mesh. Больше значение дешевле, но рельеф становится грубее.",
            ["Basemap distance"] = "Дистанция перехода terrain к basemap. Меньше значение дешевле, но дальняя земля может выглядеть проще.",
            ["Detail distance"] = "Дальность встроенных Terrain Details. Для этой сцены лучше держать низко, чтобы не дублировать GPU grass.",
            ["Detail density"] = "Плотность встроенных Terrain Details. Увеличение может вернуть лишнюю траву/овердроу.",
            ["Built-in tree distance"] = "Дальность встроенных Terrain trees. Здесь деревья в основном рисуются instanced renderer-ом.",
            ["Tree billboard distance"] = "Дистанция перехода встроенных Terrain trees в billboard.",
            ["Tree fade length"] = "Длина crossfade для встроенных Terrain trees.",
            ["Full LOD tree count"] = "Сколько встроенных Terrain trees Unity держит в полном LOD.",
            ["Light shadows"] = "Включает тени directional light. Основной множитель стоимости shadow rendering.",
            ["Intensity"] = "Яркость directional light.",
            ["Shadow strength"] = "Контраст теней directional light."
        };

        private readonly Dictionary<string, string> fieldTooltips = new(StringComparer.Ordinal)
        {
            ["enabled"] = "Включает или отключает этот renderer/settings block.",
            ["drawDistance"] = "Legacy distance field. Сейчас effective grass draw distance берется из far visual distance.",
            ["highDetailDistance"] = "Legacy high detail field. Сейчас effective near ring берется из near distance.",
            ["chunkSize"] = "Размер grass cluster grid. Меньше размер точнее culling, но больше clusters и CPU overhead при build/cull.",
            ["placementSpacing"] = "Базовый шаг candidate placement. Меньше значение дает больше потенциальных clumps и дороже build.",
            ["maxVisibleClumps"] = "Legacy общий лимит clumps. Сейчас near/mid budgets задают основной лимит.",
            ["densityScale"] = "Общий множитель плотности grass placement. Изменения требуют Rebuild Grass.",
            ["nearDistance"] = "Дальность near ring с полноценными grass cards. Увеличение быстро растит geometry cost.",
            ["midDistance"] = "Дальность mid ring с более дешевыми и разреженными grass cards.",
            ["farVisualDistance"] = "Дальность terrain-driven grass visual/tint. Far ring почти не добавляет mesh grass.",
            ["maxVisibleGrassTriangles"] = "Hard budget видимых grass triangles. Renderer режет mid/near additions по этому лимиту.",
            ["maxVisibleNearInstances"] = "Лимит near clumps. Влияет на силуэт травы рядом с камерой.",
            ["maxVisibleMidInstances"] = "Лимит mid clumps. Влияет на ощущение массы на средней дистанции.",
            ["midDensityMultiplier"] = "Сколько packed instances допускается в mid ring. Меньше значение дешевле и реже.",
            ["enableGrassShadows"] = "Разрешает near grass cast shadows. Обычно выключено на low/weak, потому что дорого.",
            ["enableTerrainDensityTint"] = "Подмешивает density map в terrain splat/tint, чтобы far ring не выглядел голым без mesh grass.",
            ["enableJobs"] = "Зарезервированный флаг для Jobs path. Baseline сейчас CPU без обязательного Jobs.",
            ["enableIndirectHighTier"] = "Зарезервированный флаг для optional indirect/high tier path.",
            ["useOptimizedClusterRenderer"] = "Включает новый clustered grass renderer. Если выключить, grass renderer не будет добавлять mesh instances.",
            ["densityGridResolution"] = "Разрешение baked density grid. Больше точнее patches, но дороже build и память.",
            ["grassSeed"] = "Дополнительный deterministic seed для grass placement/noise.",
            ["macroNoiseScale"] = "Размер больших patches травы в метрах. Больше значение дает более крупные пятна.",
            ["microNoiseScale"] = "Размер мелкого breakup noise. Меньше значение делает пятна более дробными.",
            ["noiseOctaves"] = "Количество FBM octave. Больше деталей, но дороже bake density grid.",
            ["noisePersistence"] = "Сила последующих octave в FBM. Выше значение делает noise более контрастным/рваным.",
            ["noiseLacunarity"] = "Рост частоты octave. Меняет характер noise breakup.",
            ["noiseThresholdLow"] = "Нижний порог density noise. Повышение увеличивает проплешины.",
            ["noiseThresholdHigh"] = "Верхний порог density noise. Меняет мягкость перехода между пусто/густо.",
            ["noiseContrast"] = "Контраст density noise. Больше значение делает patches резче.",
            ["lodFadeDistance"] = "Длина fade-out mid grass перед far terrain-only ring.",
            ["atlasColumns"] = "Количество колонок atlas variants в grass shader.",
            ["atlasRows"] = "Количество строк atlas variants в grass shader.",
            ["nearBladeCount"] = "Сколько cards в near clump mesh. Больше красивее рядом, но умножает triangles.",
            ["midBladeCount"] = "Сколько cards в mid clump mesh. Больше плотнее, но дороже на средней дистанции.",
            ["slopeFadeStart"] = "С какого slope grass density начинает уменьшаться.",
            ["slopeFadeEnd"] = "На каком slope grass density становится почти нулевой.",
            ["waterFadeStart"] = "Минимальный зазор над водой, с которого grass начинает появляться.",
            ["waterFadeEnd"] = "Зазор над водой, на котором water mask уже почти не подавляет grass.",
            ["heightFadeStart"] = "Высота terrain, с которой grass density начинает спадать.",
            ["heightFadeEnd"] = "Высота terrain, на которой high-altitude grass почти исчезает.",
            ["grassVariationLayerWeight"] = "Вес variation terrain layer в grass mask.",
            ["shoreSuppression"] = "Насколько shore layer подавляет grass placement.",
            ["rockSuppression"] = "Насколько rock layer подавляет grass placement.",
            ["terrainGrassBoost"] = "Сила усиления grass layer при terrain density tint.",
            ["terrainVariationBoost"] = "Сила добавления variation layer при terrain density tint.",
            ["terrainRockSuppression"] = "Насколько terrain tint приглушает rock/shore layers в grass patches.",
            ["terrainDetailDensityScale"] = "Множитель встроенных Terrain Details относительно GPU grass settings.",
            ["terrainDetailFallbackDistance"] = "Дистанция fallback details, если используются Terrain Details.",
            ["windStrength"] = "Амплитуда vertex wind у grass shader.",
            ["windSpeed"] = "Скорость анимации wind.",
            ["windScale"] = "World-space scale wind phase. Меняет размер волн ветра.",
            ["receiveShadows"] = "Разрешает grass получать тени. Может влиять на shader cost.",
            ["lod0Distance"] = "Дистанция LOD0 для instanced trees/props. Больше значение держит дорогие деревья ближе/дальше.",
            ["lod1Distance"] = "Дистанция LOD1 для instanced trees/props.",
            ["lod2Distance"] = "Дистанция LOD2/billboard для instanced trees/props.",
            ["cullDistance"] = "Дистанция полного culling деревьев/props.",
            ["shadowDistance"] = "Прямая максимальная дистанция теней для generated instanced trees/props. Также ограничивается глобальным Unity shadow distance.",
            ["disableLeafShadowsAfterLod0"] = "Отключает тени листьев после LOD0, снижая shadow submitted tris.",
            ["disableAllShadowsAfterLod1"] = "Отключает все тени после LOD1.",
            ["useFarBillboardLod"] = "Разрешает дальний billboard/LOD2 вместо удержания LOD1.",
            ["foliageDensityScale"] = "Множитель плотности листвы/forest placement, использующийся при генерации."
        };

        private readonly List<SimplePlayerController> disabledControllers = new();

        private Canvas canvas;
        private RectTransform contentRect;
        private Text statusText;
        private RectTransform tooltipRect;
        private Text tooltipText;
        private CanvasGroup tooltipGroup;
        private bool visible;
        private bool grassRebuildDirty;
        private bool inputSuspended;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;

        private GeneratedGpuGrassRenderer[] grassRenderers = Array.Empty<GeneratedGpuGrassRenderer>();
        private GeneratedInstancedPropRenderer[] propRenderers = Array.Empty<GeneratedInstancedPropRenderer>();
        private Terrain[] terrains = Array.Empty<Terrain>();
        private Light directionalLight;
        private GpuGrassSettings grassSettings;
        private ForestLodSettings forestSettings;

        public static RuntimeGraphicsSettingsPanel Ensure(Camera camera = null)
        {
            var existing = FindFirstObjectByType<RuntimeGraphicsSettingsPanel>();
            if (existing != null)
            {
                if (camera != null)
                {
                    existing.targetCamera = camera;
                }

                return existing;
            }

            var panelObject = new GameObject("Runtime Graphics Settings Panel");
            var panel = panelObject.AddComponent<RuntimeGraphicsSettingsPanel>();
            panel.targetCamera = camera;
            return panel;
        }

        private void Awake()
        {
            visible = false;
            ResolveTargets();
            BuildUi();
            SetVisible(visibleOnStart);
        }

        private void OnDisable()
        {
            if (visible)
            {
                RestorePlayerInput();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame)
            {
                SetVisible(!visible);
            }
        }

        private void BuildUi()
        {
            canvas = RuntimeUiFactory.CreateCanvas("Runtime Graphics Settings Canvas");
            canvas.sortingOrder = 5100;
            canvas.transform.SetParent(transform, false);

            var panelImage = RuntimeUiFactory.CreateImage(canvas.transform, "Graphics Settings Panel", new Color(0.018f, 0.022f, 0.024f, 0.93f));
            var panelRect = panelImage.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -24f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var title = RuntimeUiFactory.CreateText(
                panelImage.transform,
                "Graphics Settings Title",
                "GRAPHICS SETTINGS  [F4]",
                25,
                new Color(0.92f, 0.98f, 0.84f, 1f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -12f);
            titleRect.sizeDelta = new Vector2(-28f, 36f);

            var topRow = RuntimeUiFactory.CreateUiObject(panelImage.transform, "Graphics Settings Top Buttons");
            var topRowRect = topRow.GetComponent<RectTransform>();
            topRowRect.anchorMin = new Vector2(0f, 1f);
            topRowRect.anchorMax = new Vector2(1f, 1f);
            topRowRect.pivot = new Vector2(0.5f, 1f);
            topRowRect.anchoredPosition = new Vector2(0f, -54f);
            topRowRect.sizeDelta = new Vector2(-28f, 38f);
            RuntimeUiFactory.AddHorizontalLayout(topRow, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            CreateSmallButton(
                topRow.transform,
                "Refresh",
                ResolveAndRebuildContents,
                130f,
                "Повторно находит активные terrain/grass/tree renderers и пересобирает список настроек без изменения графики.");
            CreateSmallButton(
                topRow.transform,
                "Rebuild Grass",
                RebuildGrassNow,
                160f,
                "Пересобирает grass density grid и packed instances после изменения параметров генерации травы.");
            CreateSmallButton(topRow.transform, "Close", () => SetVisible(false), 100f);

            var viewport = RuntimeUiFactory.CreateImage(panelImage.transform, "Graphics Settings Viewport", new Color(0f, 0f, 0f, 0.18f));
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(14f, 46f);
            viewportRect.offsetMax = new Vector2(-34f, -100f);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = RuntimeUiFactory.CreateUiObject(viewport.transform, "Graphics Settings Content");
            contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            RuntimeUiFactory.AddVerticalLayout(content, 10f, new RectOffset(8, 8, 8, 8), TextAnchor.UpperCenter);
            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = CreateScrollbar(panelImage.transform);
            var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 42f;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 4f;

            statusText = RuntimeUiFactory.CreateText(
                panelImage.transform,
                "Graphics Settings Status",
                string.Empty,
                18,
                new Color(0.77f, 0.88f, 0.72f, 1f),
                TextAnchor.MiddleLeft);
            var statusRect = statusText.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 10f);
            statusRect.sizeDelta = new Vector2(-28f, 28f);

            BuildTooltipUi(canvas.transform);
            RebuildContents();
        }

        private void ResolveAndRebuildContents()
        {
            ResolveTargets();
            RebuildContents();
        }

        private void RebuildContents()
        {
            if (contentRect == null)
            {
                return;
            }

            for (var i = contentRect.childCount - 1; i >= 0; i--)
            {
                var child = contentRect.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            ResolveSettingsObjects();
            AddVisibilitySection();
            AddUnityCameraSection();
            AddTerrainSection();
            AddLightSection();
            AddForestSection();
            AddGrassSection();
            RefreshStatus();
        }

        private void ResolveTargets()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                targetCamera = Camera.main != null
                    ? Camera.main
                    : FindFirstObjectByType<Camera>();
            }

            grassRenderers = FindObjectsByType<GeneratedGpuGrassRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            propRenderers = FindObjectsByType<GeneratedInstancedPropRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            terrains = FindObjectsByType<Terrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            directionalLight = ResolveDirectionalLight();
        }

        private void ResolveSettingsObjects()
        {
            if (grassRenderers.Length > 0 && grassRenderers[0] != null)
            {
                grassSettings = grassRenderers[0].RuntimeSettings.Clone();
            }
            else
            {
                grassSettings ??= GpuGrassSettings.CreatePreset(ForestQualityLevel.High);
            }

            if (propRenderers.Length > 0 && propRenderers[0] != null && propRenderers[0].CurrentForestLodSettings != null)
            {
                forestSettings = propRenderers[0].CurrentForestLodSettings.Clone();
            }
            else
            {
                forestSettings ??= ForestLodSettings.CreatePreset(ForestQualityLevel.High);
            }
        }

        private void AddUnityCameraSection()
        {
            var section = CreateSection("Unity / Camera");
            AddSlider(section.transform, "Camera FOV", 35f, 100f, targetCamera != null ? targetCamera.fieldOfView : 68f, false, value =>
            {
                if (targetCamera != null)
                {
                    targetCamera.fieldOfView = value;
                }
            }, TooltipForLabel("Camera FOV"));
            AddSlider(section.transform, "Camera near clip", 0.01f, 1f, targetCamera != null ? targetCamera.nearClipPlane : 0.1f, false, value =>
            {
                if (targetCamera != null)
                {
                    targetCamera.nearClipPlane = Mathf.Max(0.01f, value);
                }
            }, TooltipForLabel("Camera near clip"));
            AddSlider(section.transform, "Camera far clip", 50f, 5000f, targetCamera != null ? targetCamera.farClipPlane : 3000f, false, value =>
            {
                if (targetCamera != null)
                {
                    targetCamera.farClipPlane = Mathf.Max(1f, value);
                }
            }, TooltipForLabel("Camera far clip"));
            AddSlider(section.transform, "Quality LOD bias", 0.25f, 4f, QualitySettings.lodBias, false, value => QualitySettings.lodBias = value, TooltipForLabel("Quality LOD bias"));
            AddSlider(section.transform, "Max LOD level", 0f, 4f, QualitySettings.maximumLODLevel, true, value => QualitySettings.maximumLODLevel = Mathf.RoundToInt(value), TooltipForLabel("Max LOD level"));
            AddSlider(section.transform, "Unity shadow distance", 0f, 220f, QualitySettings.shadowDistance, false, value => QualitySettings.shadowDistance = value, TooltipForLabel("Unity shadow distance"));
            AddSlider(section.transform, "Shadow cascades", 0f, 4f, QualitySettings.shadowCascades, true, value =>
            {
                var rounded = Mathf.RoundToInt(value);
                QualitySettings.shadowCascades = rounded <= 0 ? 0 : rounded <= 2 ? 2 : 4;
            }, TooltipForLabel("Shadow cascades"));
            AddSlider(section.transform, "MSAA", 0f, 8f, QualitySettings.antiAliasing, true, value =>
            {
                var rounded = Mathf.RoundToInt(value);
                QualitySettings.antiAliasing = rounded <= 0 ? 0 : rounded <= 2 ? 2 : rounded <= 4 ? 4 : 8;
            }, TooltipForLabel("MSAA"));
            AddSlider(section.transform, "Texture mip limit", 0f, 3f, QualitySettings.globalTextureMipmapLimit, true, value => QualitySettings.globalTextureMipmapLimit = Mathf.RoundToInt(value), TooltipForLabel("Texture mip limit"));
            AddSlider(section.transform, "VSync", 0f, 2f, QualitySettings.vSyncCount, true, value => QualitySettings.vSyncCount = Mathf.RoundToInt(value), TooltipForLabel("VSync"));
            AddSlider(section.transform, "Target FPS", 30f, 240f, Application.targetFrameRate > 0 ? Application.targetFrameRate : 120, true, value => Application.targetFrameRate = Mathf.RoundToInt(value), TooltipForLabel("Target FPS"));
        }

        private void AddVisibilitySection()
        {
            var section = CreateSection("Visibility / Isolation");
            AddToggle(
                section.transform,
                "Show terrain",
                IsTerrainVisible(),
                SetTerrainVisible,
                "Показывает или скрывает Unity Terrain. Удобно смотреть вклад terrain в Unity Stats и F3.");
            AddToggle(
                section.transform,
                "Show GPU grass",
                IsGrassVisible(),
                SetGrassVisible,
                "Показывает или скрывает generated GPU grass renderer, чтобы оценить вклад травы в текущий кадр.");
            AddToggle(
                section.transform,
                "Show trees",
                IsPropCategoryVisible(GeneratedPropVisibilityCategory.Trees),
                value => SetPropCategoryVisible(GeneratedPropVisibilityCategory.Trees, value),
                "Показывает или скрывает generated instanced tree categories: Tree, ForestCoreTrees, ForestAccentTrees.");
            AddToggle(
                section.transform,
                "Show rocks / cliffs",
                IsPropCategoryVisible(GeneratedPropVisibilityCategory.Rocks),
                value => SetPropCategoryVisible(GeneratedPropVisibilityCategory.Rocks, value),
                "Показывает или скрывает generated rocks, large rocks, small/medium rocks и cliffs.");
            AddToggle(
                section.transform,
                "Show bushes",
                IsPropCategoryVisible(GeneratedPropVisibilityCategory.Bushes),
                value => SetPropCategoryVisible(GeneratedPropVisibilityCategory.Bushes, value),
                "Показывает или скрывает generated bush categories.");
            AddToggle(
                section.transform,
                "Show plants / cover",
                IsPropCategoryVisible(GeneratedPropVisibilityCategory.Plants),
                value => SetPropCategoryVisible(GeneratedPropVisibilityCategory.Plants, value),
                "Показывает или скрывает low plants, shore plants, ground cover и prop grass, если они есть как props.");
            AddToggle(
                section.transform,
                "Show logs / other props",
                IsPropCategoryVisible(GeneratedPropVisibilityCategory.Logs) && IsPropCategoryVisible(GeneratedPropVisibilityCategory.Other),
                value =>
                {
                    SetPropCategoryVisible(GeneratedPropVisibilityCategory.Logs, value);
                    SetPropCategoryVisible(GeneratedPropVisibilityCategory.Other, value);
                },
                "Показывает или скрывает logs и остальные generated props, не попавшие в отдельные группы.");
        }

        private void AddTerrainSection()
        {
            var terrain = terrains.Length > 0 ? terrains[0] : null;
            var section = CreateSection("Terrain Runtime");
            AddToggle(section.transform, "Terrain draw instanced", terrain != null && terrain.drawInstanced, value =>
            {
                ForEachTerrain(t => t.drawInstanced = value);
            }, TooltipForLabel("Terrain draw instanced"));
            AddToggle(section.transform, "Terrain casts shadows", terrain != null && terrain.shadowCastingMode != ShadowCastingMode.Off, value =>
            {
                ForEachTerrain(t => t.shadowCastingMode = value ? ShadowCastingMode.On : ShadowCastingMode.Off);
            }, TooltipForLabel("Terrain casts shadows"));
            AddSlider(section.transform, "Heightmap pixel error", 1f, 32f, terrain != null ? terrain.heightmapPixelError : 8f, false, value =>
            {
                ForEachTerrain(t => t.heightmapPixelError = value);
            }, TooltipForLabel("Heightmap pixel error"));
            AddSlider(section.transform, "Basemap distance", 20f, 2000f, terrain != null ? terrain.basemapDistance : 1000f, false, value =>
            {
                ForEachTerrain(t => t.basemapDistance = value);
            }, TooltipForLabel("Basemap distance"));
            AddSlider(section.transform, "Detail distance", 0f, 250f, terrain != null ? terrain.detailObjectDistance : 0f, false, value =>
            {
                ForEachTerrain(t => t.detailObjectDistance = value);
            }, TooltipForLabel("Detail distance"));
            AddSlider(section.transform, "Detail density", 0f, 1f, terrain != null ? terrain.detailObjectDensity : 0f, false, value =>
            {
                ForEachTerrain(t => t.detailObjectDensity = value);
            }, TooltipForLabel("Detail density"));
            AddSlider(section.transform, "Built-in tree distance", 0f, 600f, terrain != null ? terrain.treeDistance : 0f, false, value =>
            {
                ForEachTerrain(t => t.treeDistance = value);
            }, TooltipForLabel("Built-in tree distance"));
            AddSlider(section.transform, "Tree billboard distance", 0f, 600f, terrain != null ? terrain.treeBillboardDistance : 0f, false, value =>
            {
                ForEachTerrain(t => t.treeBillboardDistance = value);
            }, TooltipForLabel("Tree billboard distance"));
            AddSlider(section.transform, "Tree fade length", 0f, 200f, terrain != null ? terrain.treeCrossFadeLength : 0f, false, value =>
            {
                ForEachTerrain(t => t.treeCrossFadeLength = value);
            }, TooltipForLabel("Tree fade length"));
            AddSlider(section.transform, "Full LOD tree count", 0f, 1200f, terrain != null ? terrain.treeMaximumFullLODCount : 0f, true, value =>
            {
                ForEachTerrain(t => t.treeMaximumFullLODCount = Mathf.RoundToInt(value));
            }, TooltipForLabel("Full LOD tree count"));
        }

        private void AddLightSection()
        {
            var section = CreateSection("Directional Light");
            AddToggle(section.transform, "Light shadows", directionalLight != null && directionalLight.shadows != LightShadows.None, value =>
            {
                if (directionalLight != null)
                {
                    directionalLight.shadows = value ? LightShadows.Soft : LightShadows.None;
                }
            }, TooltipForLabel("Light shadows"));
            AddSlider(section.transform, "Intensity", 0f, 4f, directionalLight != null ? directionalLight.intensity : 1f, false, value =>
            {
                if (directionalLight != null)
                {
                    directionalLight.intensity = value;
                }
            }, TooltipForLabel("Intensity"));
            AddSlider(section.transform, "Shadow strength", 0f, 1f, directionalLight != null ? directionalLight.shadowStrength : 1f, false, value =>
            {
                if (directionalLight != null)
                {
                    directionalLight.shadowStrength = value;
                }
            }, TooltipForLabel("Shadow strength"));
        }

        private void AddForestSection()
        {
            var section = CreateSection("Trees / Instanced Props");
            AddPresetButtons(section.transform, preset =>
            {
                forestSettings.ApplyPreset(preset);
                ApplyForestSettings();
                RebuildContents();
            });
            AddSerializedSettings(section.transform, forestSettings, OnForestSettingChanged);
        }

        private void AddGrassSection()
        {
            var section = CreateSection("GPU Grass");
            AddPresetButtons(section.transform, preset =>
            {
                grassSettings.ApplyPreset(preset);
                grassRebuildDirty = true;
                ApplyGrassSettings(false);
                RebuildContents();
            });
            AddSerializedSettings(section.transform, grassSettings, OnGrassSettingChanged);
        }

        private void AddSerializedSettings(Transform parent, object target, Action<FieldInfo> onChanged)
        {
            if (target == null)
            {
                return;
            }

            var currentHeader = "General";
            AddSubHeader(parent, currentHeader);
            var fields = target.GetType().GetFields(SerializedFieldFlags);
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (!field.IsDefined(typeof(SerializeField), true) || field.IsStatic)
                {
                    continue;
                }

                if (HiddenRuntimeFields.Contains(field.Name))
                {
                    continue;
                }

                var header = field.GetCustomAttribute<HeaderAttribute>();
                if (header != null && !string.Equals(currentHeader, header.header, StringComparison.Ordinal))
                {
                    currentHeader = header.header;
                    AddSubHeader(parent, currentHeader);
                }

                if (field.FieldType == typeof(bool))
                {
                    AddToggle(parent, NicifyName(field.Name), (bool)field.GetValue(target), value =>
                    {
                        field.SetValue(target, value);
                        onChanged?.Invoke(field);
                    }, TooltipForField(field));
                    continue;
                }

                if (field.FieldType == typeof(float))
                {
                    var range = ResolveRange(field, (float)field.GetValue(target));
                    AddSlider(parent, NicifyName(field.Name), range.x, range.y, (float)field.GetValue(target), false, value =>
                    {
                        field.SetValue(target, value);
                        onChanged?.Invoke(field);
                    }, TooltipForField(field));
                    continue;
                }

                if (field.FieldType == typeof(int))
                {
                    var value = (int)field.GetValue(target);
                    var range = ResolveRange(field, value);
                    AddSlider(parent, NicifyName(field.Name), range.x, range.y, value, true, sliderValue =>
                    {
                        field.SetValue(target, Mathf.RoundToInt(sliderValue));
                        onChanged?.Invoke(field);
                    }, TooltipForField(field));
                }
            }
        }

        private void OnForestSettingChanged(FieldInfo field)
        {
            ApplyForestSettings();
            RefreshStatus();
        }

        private void OnGrassSettingChanged(FieldInfo field)
        {
            if (field != null && GrassRebuildFields.Contains(field.Name))
            {
                grassRebuildDirty = true;
            }

            ApplyGrassSettings(false);
            RefreshStatus();
        }

        private bool IsTerrainVisible()
        {
            if (terrains.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null && !terrains[i].enabled)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetTerrainVisible(bool value)
        {
            ForEachTerrain(terrain => terrain.enabled = value);
            RefreshStatus();
        }

        private bool IsGrassVisible()
        {
            if (grassRenderers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < grassRenderers.Length; i++)
            {
                if (grassRenderers[i] != null && !grassRenderers[i].enabled)
                {
                    return false;
                }
            }

            return true;
        }

        private void SetGrassVisible(bool value)
        {
            for (var i = 0; i < grassRenderers.Length; i++)
            {
                if (grassRenderers[i] != null)
                {
                    grassRenderers[i].enabled = value;
                }
            }

            RefreshStatus();
        }

        private bool IsPropCategoryVisible(GeneratedPropVisibilityCategory category)
        {
            if (propRenderers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < propRenderers.Length; i++)
            {
                if (propRenderers[i] != null && !propRenderers[i].IsRuntimeCategoryVisible(category))
                {
                    return false;
                }
            }

            return true;
        }

        private void SetPropCategoryVisible(GeneratedPropVisibilityCategory category, bool value)
        {
            for (var i = 0; i < propRenderers.Length; i++)
            {
                if (propRenderers[i] != null)
                {
                    propRenderers[i].SetRuntimeCategoryVisible(category, value);
                }
            }

            SetFallbackPropRenderersVisible(category, value);
            RefreshStatus();
        }

        private static void SetFallbackPropRenderersVisible(GeneratedPropVisibilityCategory category, bool value)
        {
            var renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null ||
                    !TryResolveGeneratedPropCategory(renderer.transform, out var rendererCategory) ||
                    rendererCategory != category)
                {
                    continue;
                }

                renderer.enabled = value;
            }
        }

        private static bool TryResolveGeneratedPropCategory(Transform transform, out GeneratedPropVisibilityCategory category)
        {
            var current = transform;
            while (current != null)
            {
                if (current.parent != null && string.Equals(current.parent.name, "GeneratedProps", StringComparison.Ordinal))
                {
                    category = ResolveGeneratedPropCategory(current.name);
                    return true;
                }

                current = current.parent;
            }

            category = GeneratedPropVisibilityCategory.Other;
            return false;
        }

        private static GeneratedPropVisibilityCategory ResolveGeneratedPropCategory(string categoryName)
        {
            var key = (categoryName ?? string.Empty).ToLowerInvariant();
            if (key.Contains("tree") || key.Contains("forest"))
            {
                return GeneratedPropVisibilityCategory.Trees;
            }

            if (key.Contains("rock") || key.Contains("cliff"))
            {
                return GeneratedPropVisibilityCategory.Rocks;
            }

            if (key.Contains("bush"))
            {
                return GeneratedPropVisibilityCategory.Bushes;
            }

            if (key.Contains("plant") || key.Contains("grass") || key.Contains("shore") || key.Contains("cover"))
            {
                return GeneratedPropVisibilityCategory.Plants;
            }

            if (key.Contains("log"))
            {
                return GeneratedPropVisibilityCategory.Logs;
            }

            return GeneratedPropVisibilityCategory.Other;
        }

        private void ApplyForestSettings()
        {
            if (forestSettings == null)
            {
                return;
            }

            for (var i = 0; i < propRenderers.Length; i++)
            {
                if (propRenderers[i] != null)
                {
                    propRenderers[i].ApplyRuntimeLodSettings(forestSettings);
                }
            }
        }

        private void ApplyGrassSettings(bool rebuild)
        {
            if (grassSettings == null)
            {
                return;
            }

            for (var i = 0; i < grassRenderers.Length; i++)
            {
                if (grassRenderers[i] != null)
                {
                    grassRenderers[i].ApplyRuntimeSettings(grassSettings, rebuild);
                }
            }

            if (rebuild)
            {
                grassRebuildDirty = false;
            }
        }

        private void RebuildGrassNow()
        {
            ResolveTargets();
            ApplyGrassSettings(true);
            RefreshStatus();
        }

        private GameObject CreateSection(string title)
        {
            var sectionImage = RuntimeUiFactory.CreateImage(contentRect, title + " Section", new Color(0.055f, 0.064f, 0.058f, 0.88f));
            RuntimeUiFactory.AddVerticalLayout(sectionImage.gameObject, 8f, new RectOffset(10, 10, 10, 12), TextAnchor.UpperCenter);
            sectionImage.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleText = RuntimeUiFactory.CreateText(
                sectionImage.transform,
                title + " Title",
                title,
                22,
                new Color(0.95f, 0.89f, 0.66f, 1f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(titleText.gameObject, 0f, 30f);
            return sectionImage.gameObject;
        }

        private void AddSubHeader(Transform parent, string text)
        {
            var header = RuntimeUiFactory.CreateText(
                parent,
                text + " Header",
                text,
                18,
                new Color(0.68f, 0.83f, 0.64f, 1f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            RuntimeUiFactory.AddLayoutElement(header.gameObject, 0f, 24f);
        }

        private void AddPresetButtons(Transform parent, Action<ForestQualityLevel> onPreset)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, "Preset Buttons");
            RuntimeUiFactory.AddHorizontalLayout(row, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            RuntimeUiFactory.AddLayoutElement(row, 0f, 34f);
            CreateSmallButton(row.transform, "Low", () => onPreset?.Invoke(ForestQualityLevel.Low), 82f, "Применяет самый дешёвый профиль: короткие дистанции, жёсткие бюджеты и минимум теней.");
            CreateSmallButton(row.transform, "Medium", () => onPreset?.Invoke(ForestQualityLevel.Medium), 104f, "Применяет сбалансированный профиль с чуть большей дальностью и плотностью.");
            CreateSmallButton(row.transform, "High", () => onPreset?.Invoke(ForestQualityLevel.High), 82f, "Применяет более дорогой профиль: больше LOD distance, density и shadow budget.");
            CreateSmallButton(row.transform, "Ultra", () => onPreset?.Invoke(ForestQualityLevel.Ultra), 82f, "Применяет максимально дорогой профиль для проверки качества и верхней границы нагрузки.");
        }

        private void AddSlider(
            Transform parent,
            string label,
            float min,
            float max,
            float value,
            bool wholeNumbers,
            Action<float> onChanged)
        {
            AddSlider(parent, label, min, max, value, wholeNumbers, onChanged, TooltipForLabel(label));
        }

        private void AddSlider(
            Transform parent,
            string label,
            float min,
            float max,
            float value,
            bool wholeNumbers,
            Action<float> onChanged,
            string tooltip)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, label + " Row");
            RuntimeUiFactory.AddHorizontalLayout(row, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            RuntimeUiFactory.AddLayoutElement(row, 0f, 42f);
            AttachTooltip(row, tooltip);

            var labelText = RuntimeUiFactory.CreateText(row.transform, label + " Label", label, 17, new Color(0.88f, 0.91f, 0.80f, 1f), TextAnchor.MiddleLeft);
            RuntimeUiFactory.AddLayoutElement(labelText.gameObject, 230f, 34f);
            AttachTooltip(labelText.gameObject, tooltip);

            var slider = RuntimeUiFactory.CreateSlider(row.transform, label + " Slider", Mathf.InverseLerp(min, max, value), null, new Vector2(250f, 34f));
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
            AttachTooltip(slider.gameObject, tooltip);

            var valueText = RuntimeUiFactory.CreateText(row.transform, label + " Value", FormatNumber(value, wholeNumbers), 16, new Color(0.95f, 0.91f, 0.72f, 1f), TextAnchor.MiddleRight);
            RuntimeUiFactory.AddLayoutElement(valueText.gameObject, 90f, 34f);
            AttachTooltip(valueText.gameObject, tooltip);

            slider.onValueChanged.AddListener(rawValue =>
            {
                var nextValue = wholeNumbers ? Mathf.Round(rawValue) : rawValue;
                valueText.text = FormatNumber(nextValue, wholeNumbers);
                onChanged?.Invoke(nextValue);
            });
        }

        private void AddToggle(Transform parent, string label, bool value, Action<bool> onChanged)
        {
            AddToggle(parent, label, value, onChanged, TooltipForLabel(label));
        }

        private void AddToggle(Transform parent, string label, bool value, Action<bool> onChanged, string tooltip)
        {
            var row = RuntimeUiFactory.CreateUiObject(parent, label + " Row");
            RuntimeUiFactory.AddHorizontalLayout(row, 8f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleLeft);
            RuntimeUiFactory.AddLayoutElement(row, 0f, 36f);
            AttachTooltip(row, tooltip);

            var toggleObject = RuntimeUiFactory.CreateUiObject(row.transform, label + " Toggle");
            RuntimeUiFactory.AddLayoutElement(toggleObject, 32f, 32f);
            var background = toggleObject.AddComponent<Image>();
            background.color = new Color(0.10f, 0.12f, 0.10f, 1f);

            var checkmark = RuntimeUiFactory.CreateImage(toggleObject.transform, "Checkmark", new Color(0.75f, 0.92f, 0.38f, 1f));
            var checkRect = checkmark.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.18f, 0.18f);
            checkRect.anchorMax = new Vector2(0.82f, 0.82f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;

            var toggle = toggleObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.SetIsOnWithoutNotify(value);
            AttachTooltip(toggleObject, tooltip);

            var labelText = RuntimeUiFactory.CreateText(row.transform, label + " Label", label, 17, new Color(0.88f, 0.91f, 0.80f, 1f), TextAnchor.MiddleLeft);
            RuntimeUiFactory.AddLayoutElement(labelText.gameObject, 520f, 32f);
            AttachTooltip(labelText.gameObject, tooltip);
            toggle.onValueChanged.AddListener(nextValue => onChanged?.Invoke(nextValue));
        }

        private Button CreateSmallButton(Transform parent, string label, Action onClick, float width, string tooltip = null)
        {
            var button = RuntimeUiFactory.CreateButton(parent, label + " Button", label, () => onClick?.Invoke(), new Vector2(width, 32f));
            AttachTooltip(button.gameObject, tooltip);
            var layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = width;
                layout.preferredHeight = 32f;
                layout.minHeight = 32f;
            }

            var labelText = button.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.fontSize = 16;
                labelText.resizeTextMaxSize = 16;
            }

            return button;
        }

        private Scrollbar CreateScrollbar(Transform parent)
        {
            var scrollbarImage = RuntimeUiFactory.CreateImage(parent, "Graphics Settings Scrollbar", new Color(0.08f, 0.09f, 0.08f, 0.9f));
            var scrollbarRect = scrollbarImage.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-28f, 46f);
            scrollbarRect.offsetMax = new Vector2(-14f, -100f);

            var handle = RuntimeUiFactory.CreateImage(scrollbarImage.transform, "Handle", new Color(0.68f, 0.76f, 0.54f, 0.96f));
            RuntimeUiFactory.Stretch(handle.gameObject, Vector2.zero, Vector2.zero);

            var scrollbar = scrollbarImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            return scrollbar;
        }

        private void BuildTooltipUi(Transform parent)
        {
            var tooltipImage = RuntimeUiFactory.CreateImage(parent, "Graphics Settings Tooltip", new Color(0.015f, 0.018f, 0.016f, 0.96f));
            tooltipRect = tooltipImage.GetComponent<RectTransform>();
            tooltipRect.anchorMin = Vector2.zero;
            tooltipRect.anchorMax = Vector2.zero;
            tooltipRect.pivot = new Vector2(0f, 1f);
            tooltipRect.sizeDelta = new Vector2(390f, 116f);
            tooltipGroup = tooltipImage.gameObject.AddComponent<CanvasGroup>();
            tooltipGroup.alpha = 0f;
            tooltipGroup.blocksRaycasts = false;
            tooltipGroup.interactable = false;

            tooltipText = RuntimeUiFactory.CreateText(
                tooltipImage.transform,
                "Tooltip Text",
                string.Empty,
                17,
                new Color(0.92f, 0.96f, 0.82f, 1f),
                TextAnchor.UpperLeft);
            tooltipText.resizeTextForBestFit = false;
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            RuntimeUiFactory.Stretch(tooltipText.gameObject, new Vector2(12f, 10f), new Vector2(-12f, -10f));
            tooltipImage.gameObject.SetActive(false);
        }

        private void AttachTooltip(GameObject target, string tooltip)
        {
            if (target == null || string.IsNullOrWhiteSpace(tooltip))
            {
                return;
            }

            var targetComponent = target.GetComponent<RuntimeGraphicsTooltipTarget>() ??
                                  target.AddComponent<RuntimeGraphicsTooltipTarget>();
            targetComponent.Initialize(this, tooltip);
        }

        private void ShowTooltip(string tooltip, Vector2 screenPosition)
        {
            if (tooltipGroup == null || tooltipRect == null || tooltipText == null || string.IsNullOrWhiteSpace(tooltip))
            {
                return;
            }

            tooltipText.text = tooltip;
            tooltipRect.gameObject.SetActive(true);
            tooltipGroup.alpha = 1f;

            var width = tooltipRect.sizeDelta.x;
            var height = tooltipRect.sizeDelta.y;
            var x = Mathf.Clamp(screenPosition.x + 18f, 12f, Screen.width - width - 12f);
            var y = Mathf.Clamp(screenPosition.y - 18f, height + 12f, Screen.height - 12f);
            tooltipRect.position = new Vector3(x, y, 0f);
            tooltipRect.SetAsLastSibling();
        }

        private void HideTooltip(RuntimeGraphicsTooltipTarget source)
        {
            if (tooltipGroup == null || tooltipRect == null)
            {
                return;
            }

            tooltipGroup.alpha = 0f;
            tooltipRect.gameObject.SetActive(false);
        }

        private string TooltipForLabel(string label)
        {
            return !string.IsNullOrEmpty(label) && labelTooltips.TryGetValue(label, out var tooltip)
                ? tooltip
                : string.Empty;
        }

        private string TooltipForField(FieldInfo field)
        {
            if (field == null)
            {
                return string.Empty;
            }

            if (fieldTooltips.TryGetValue(field.Name, out var tooltip))
            {
                return tooltip;
            }

            return $"Runtime setting '{NicifyName(field.Name)}'. Меняет связанный параметр рендера; следи за Unity Stats и Geometry Debug.";
        }

        private Vector2 ResolveRange(FieldInfo field, float currentValue)
        {
            var range = field.GetCustomAttribute<RangeAttribute>();
            if (range != null)
            {
                return new Vector2(range.min, range.max);
            }

            if (fallbackRanges.TryGetValue(field.Name, out var fallback))
            {
                return fallback;
            }

            var min = field.GetCustomAttribute<MinAttribute>();
            if (min != null)
            {
                var max = Mathf.Max(min.min + 1f, Mathf.Max(currentValue * 2f, min.min + 100f));
                return new Vector2(min.min, max);
            }

            var magnitude = Mathf.Max(1f, Mathf.Abs(currentValue));
            return new Vector2(-magnitude * 2f, magnitude * 2f);
        }

        private void ForEachTerrain(Action<Terrain> action)
        {
            if (action == null)
            {
                return;
            }

            for (var i = 0; i < terrains.Length; i++)
            {
                if (terrains[i] != null)
                {
                    action(terrains[i]);
                }
            }
        }

        private static Light ResolveDirectionalLight()
        {
            var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                {
                    return lights[i];
                }
            }

            return null;
        }

        private void SetVisible(bool value)
        {
            if (visible == value && canvas != null && canvas.gameObject.activeSelf == value)
            {
                return;
            }

            visible = value;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }

            if (visible)
            {
                ResolveAndRebuildContents();
                SuspendPlayerInput();
            }
            else if (inputSuspended)
            {
                RestorePlayerInput();
            }
        }

        private void SuspendPlayerInput()
        {
            if (inputSuspended)
            {
                return;
            }

            inputSuspended = true;
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            disabledControllers.Clear();
            var controllers = FindObjectsByType<SimplePlayerController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].enabled)
                {
                    disabledControllers.Add(controllers[i]);
                    controllers[i].enabled = false;
                }
            }
        }

        private void RestorePlayerInput()
        {
            if (!inputSuspended)
            {
                return;
            }

            for (var i = 0; i < disabledControllers.Count; i++)
            {
                if (disabledControllers[i] != null)
                {
                    disabledControllers[i].enabled = true;
                }
            }

            disabledControllers.Clear();
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            inputSuspended = false;
        }

        private void RefreshStatus()
        {
            if (statusText == null)
            {
                return;
            }

            var builder = new StringBuilder(128);
            builder.Append($"targets: grass {grassRenderers.Length}, trees {propRenderers.Length}, terrain {terrains.Length}");
            if (grassRebuildDirty)
            {
                builder.Append("  |  grass rebuild pending");
            }

            statusText.text = builder.ToString();
        }

        private static string FormatNumber(float value, bool whole)
        {
            if (whole)
            {
                return Mathf.RoundToInt(value).ToString();
            }

            var abs = Mathf.Abs(value);
            if (abs >= 100f)
            {
                return value.ToString("0");
            }

            if (abs >= 10f)
            {
                return value.ToString("0.0");
            }

            return value.ToString("0.###");
        }

        private static string NicifyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(i == 0 ? char.ToUpperInvariant(c) : c);
            }

            return builder
                .Replace("Lod", "LOD")
                .Replace("Gpu", "GPU")
                .Replace("Fps", "FPS")
                .ToString();
        }

        private sealed class RuntimeGraphicsTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
        {
            private RuntimeGraphicsSettingsPanel owner;
            private string tooltip;

            public void Initialize(RuntimeGraphicsSettingsPanel panel, string text)
            {
                owner = panel;
                tooltip = text;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                owner?.ShowTooltip(tooltip, eventData.position);
            }

            public void OnPointerMove(PointerEventData eventData)
            {
                owner?.ShowTooltip(tooltip, eventData.position);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                owner?.HideTooltip(this);
            }
        }
    }
}
