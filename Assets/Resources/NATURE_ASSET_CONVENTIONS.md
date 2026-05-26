# Nature asset conventions

Источник для текущего набора: `Assets/Fristy stylize Modular Assets 2`.

Рабочие ассеты для генерации должны лежать только в `Assets/Resources`, чтобы runtime мог брать их локально через `Resources` и через `DefaultEnvironmentAssetCatalog.asset`.

## Директории

- `Assets/Resources/Prefabs/<Category>/`
- `Assets/Resources/Models/<Category>/`
- `Assets/Resources/Materials/<Category>/`
- `Assets/Resources/Textures/<Category>/`
- `Assets/Resources/TerrainLayers/<Category>/`
- `Assets/Resources/Shaders/<Category>/` только если материал реально требует кастомный shader/shadergraph

Категории: `Trees`, `Bushes`, `Rocks`, `Grass`, `Plants`, `Vines`, `ShorePlants`, `Terrain`, `Misc`.

## Имена

Формат: `Fristy_<ObjectType>_<Descriptor>_<Variant>`.

Примеры:

- `Fristy_Tree_03_01.prefab`
- `Fristy_Rock_04_02.fbx`
- `Fristy_Grass_01_Albedo.psd`
- `Fristy_Plant_Weed_05_01.prefab`
- `Fristy_Terrain_Soil.terrainlayer`

Правила:

- Без пробелов, амперсандов и слов `Variant`, `Prefab`, `Grouped`.
- Номера пишем двумя цифрами: `01`, `02`, `03`.
- Текстуры называем по роли карты: `Albedo`, `Normal`, `Height`, `Occlusion`, `Mask`.
- Опечатки исходников нормализуем: `Difuse`, `Diffuse`, `Defuse`, `Deffuse` -> `Albedo`; `Normals`, `NRM`, `NM` -> `Normal`.
- Не копировать `.meta` руками рядом с новым файлом. Копирование должно идти через Unity/AssetDatabase, чтобы Unity выдала новые GUID.

## Как обновлять

1. Положить новый пакет или новые исходники вне `Assets/Resources`.
2. Запустить меню Unity: `Tools/Legends of War and Magic/Procedural Generation/Organize Fristy Nature Resources`.
3. Утилита скопирует природные ассеты в `Resources`, переименует, ремапит ссылки на локальные GUID, починит битые material/texture references из исходного пакета, переведет материалы на `HDRP/Lit` и пересоберет `DefaultEnvironmentAssetCatalog.asset`.
4. Если материал после этого розовый, исправлять материал/модель в `Resources`, а не HDRP pipeline проекта.

## Terrain layers

Terrain layers живут в `Assets/Resources/TerrainLayers/Terrain/` и должны ссылаться только на текстуры из `Assets/Resources/Textures/`.
Текущий базовый набор:

- `Fristy_Terrain_Mud.terrainlayer` - берег/грязь у воды, `Textures/Terrain/Fristy_Terrain_Dark_Sand.png`.
- `Fristy_Terrain_Grass.terrainlayer` - основной зеленый слой земли, `Textures/Grass/Fristy_Grass_AlbedoFinal_02.png`.
- `Fristy_Terrain_Grass_01.terrainlayer` - вариация травы пятнами, `Textures/Grass/Fristy_Grass_02.psd`.
- `Fristy_Terrain_Rock.terrainlayer` - камень на склонах и обрывах, `Textures/Rocks/Fristy_Rock_T_D_02.png`.
- `Fristy_Terrain_Soil.terrainlayer` - земля с камнями для возвышенностей и сухих пятен, `Textures/Terrain/Fristy_Terrain_Soil_And_Rocks_Albedo.png`.

`ProceduralEnvironmentAssetCatalogBuilder` автоматически создает и обновляет эти `.terrainlayer`, затем кладет их в `DefaultEnvironmentAssetCatalog.asset`.
`GeneratedTerrainVisuals` ожидает пять слоев в таком порядке: mud shore, main grass, grass variation, rock, soil/highland.
Если добавляется новый terrain слой, добавь его в `EnsureFristyTerrainLayers()`, затем явно используй в `GeneratedTerrainVisuals`, чтобы порядок alphamap не разъехался.

## Материалы у FBX

Unity может создать служебные материалы рядом с FBX, например:

- `Assets/Resources/Models/Trees/Materials/Tree_3_Group.mat`
- `Assets/Resources/Models/Plants/Materials/Plants.mat`
- `Assets/Resources/Models/Rocks/Materials/2_Rock.mat`

Эти материалы тоже используются prefab-ами и должны зеркалить нормальные материалы из `Assets/Resources/Materials/<Category>/`.
Если у объекта белая поверхность без текстуры, сначала проверь, что у такого generated material заполнены `_BaseColorMap`, `_MainTex` и `_NormalMap`.
Организатор автоматически копирует параметры из стандартизированных материалов:

- `Tree_3_Group.mat` <- `Materials/Trees/Fristy_Tree_03_Group.mat`
- `Plants.mat` <- `Materials/Plants/Fristy_Plant_Common.mat`
- `2_Rock.mat`, `3_Rock.mat`, `4_Rocks.mat` <- соответствующие `Materials/Rocks/Fristy_Rock_*.mat`

## Physics colliders

Fristy prefabs могут приходить без physics collider, особенно деревья (`addColliders: 0` в `.fbx.meta`).
Не нужно вручную добавлять collider в каждый prefab: `PropPlacementStep` добавляет runtime blocking collider для твердых props после scale/rotation.

Твердые роли: `Tree`, `ForestCoreTrees`, `ForestAccentTrees`, `Rock`, `RocksSmallMedium`, `RocksLarge`, `Cliff`, `Log`, `Bushes`.
Трава, низкие растения и shore plants остаются без blocking collider.

## Dense forest generation

На максимальной густоте деревья должны выглядеть как лес, а не как одиночные props.
`MapGenerationPresetMapper` усиливает верхнюю часть `TreeDensity`: повышает density, снижает tree spacing, расширяет slope tolerance и forest cluster coverage.
`PropPlacementStep` разрешает кронам стоять ближе друг к другу через компактный tree placement footprint, но физический collider остается на стволе.
Не использовать per-instance `MaterialPropertyBlock` для tint листвы на тысячах деревьев: это резко увеличивает batches и ломает производительность.
Вариативность леса сейчас должна идти через prefab variety, Y-rotation и scale range; tint лучше делать только через небольшой набор заранее созданных material variants или через GPU-friendly shader/instancing.
