#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using LegendsOfWarAndMagic.ProceduralGeneration.Config;
using LegendsOfWarAndMagic.ProceduralGeneration.Core;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Masks;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.Game.World.Domain.Common;
using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Roads;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Roads.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Config;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Spatial;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Presentation;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Terrain;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Editor.Tests
{
    public sealed class ProceduralWorldFeatureGenerationTests
    {
        [Test]
        public void GameWorldModel_DoesNotDependOnUnityEngine()
        {
            var domainRoot = Path.Combine(Application.dataPath, "Scripts/Game/World/Domain");
            var forbiddenPatterns = new[]
            {
                "using UnityEngine",
                "\\bGameObject\\b",
                "\\bMonoBehaviour\\b",
                "\\bScriptableObject\\b",
                "\\bTransform\\b",
                "\\bResources\\.Load\\b",
                "\\bJsonUtility\\b",
                "\\bMathf\\b"
            };

            foreach (var file in Directory.GetFiles(domainRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var pattern in forbiddenPatterns)
                {
                    Assert.IsFalse(
                        Regex.IsMatch(text, pattern),
                        $"Pure world domain file references Unity/runtime API: {file} matches {pattern}");
                }
            }
        }

        [Test]
        public void ProceduralWorldGeneration_DoesNotOwnGameplayModelFolders()
        {
            var worldGenerationRoot = Path.Combine(Application.dataPath, "Scripts/ProceduralGeneration/WorldGeneration");

            Assert.IsFalse(Directory.Exists(Path.Combine(worldGenerationRoot, "Settlements/Domain")));
            Assert.IsFalse(Directory.Exists(Path.Combine(worldGenerationRoot, "Roads/Domain")));
            Assert.IsFalse(Directory.Exists(Path.Combine(worldGenerationRoot, "PointsOfInterest/Domain")));
        }

        [Test]
        public void Settlement_ConstructBuildingRespectsTier()
        {
            var villageBlacksmith = new SettlementBuildingDefinition(
                "Blacksmith_Level1",
                "Blacksmith",
                BuildingType.Blacksmith,
                1,
                SettlementTier.Village,
                new WorldSize2D(10f, 9f),
                Array.Empty<BuildingTag>());
            var camp = new LegendsOfWarAndMagic.Game.World.Domain.Settlements.Settlement(
                "camp",
                "Camp",
                SettlementTier.Camp,
                null,
                new WorldPoint2D(0f, 0f),
                24f,
                Array.Empty<SettlementBuilding>(),
                null,
                null,
                null);
            var village = new LegendsOfWarAndMagic.Game.World.Domain.Settlements.Settlement(
                "village",
                "Village",
                SettlementTier.Village,
                null,
                new WorldPoint2D(0f, 0f),
                80f,
                Array.Empty<SettlementBuilding>(),
                null,
                null,
                null);

            Assert.IsFalse(camp.CanConstructBuilding(villageBlacksmith));
            Assert.IsTrue(village.CanConstructBuilding(villageBlacksmith));
        }

        [Test]
        public void PointOfInterest_StateTransitionsAreExplicit()
        {
            var poi = new LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest.PointOfInterest(
                "poi",
                "Old Shrine",
                PointOfInterestType.Shrine,
                new WorldPoint2D(3f, 4f),
                12f,
                1,
                null,
                Array.Empty<string>(),
                PointOfInterestState.Undiscovered);

            poi.MarkKnown();
            Assert.AreEqual(PointOfInterestState.Known, poi.State);

            poi.MarkCleared();
            Assert.AreEqual(PointOfInterestState.Cleared, poi.State);
        }

        [Test]
        public void SettlementGeneration_IsDeterministicForSameSeed()
        {
            var settings = CreateSettings();
            var first = GenerateSettlements(settings, 123456, out _);
            var second = GenerateSettlements(settings, 123456, out _);

            Assert.AreEqual(first.Count, second.Count);
            for (var i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].Tier, second[i].Tier);
                Assert.AreEqual(first[i].WorldPosition.x, second[i].WorldPosition.x, 0.001f);
                Assert.AreEqual(first[i].WorldPosition.y, second[i].WorldPosition.y, 0.001f);
                Assert.AreEqual(first[i].Buildings.Count, second[i].Buildings.Count);
            }
        }

        [Test]
        public void SettlementCount_StaysInsideConfiguredBounds()
        {
            var settings = CreateSettings();
            var settlements = GenerateSettlements(settings, 88512, out _);
            var config = settings.Settlements;

            Assert.GreaterOrEqual(settlements.Count, config.MinSettlements + config.MinCamps);
            Assert.LessOrEqual(settlements.Count, config.MaxSettlements + config.MaxCamps);
        }

        [Test]
        public void BuildingCatalog_RespectsMinimumSettlementTier()
        {
            var definitions = DefaultSettlementBuildingCatalog.CreateDefinitions();

            Assert.NotNull(Find(definitions, BuildingType.Blacksmith, 1));
            Assert.AreEqual(SettlementTier.Village, Find(definitions, BuildingType.Blacksmith, 1).MinSettlementTier);
            Assert.AreEqual(SettlementTier.Town, Find(definitions, BuildingType.Blacksmith, 2).MinSettlementTier);
            Assert.GreaterOrEqual(Find(definitions, BuildingType.Temple, 1).MinSettlementTier, SettlementTier.Town);
        }

        [Test]
        public void SettlementLayout_DoesNotOverlapBuildingFootprints()
        {
            var settings = CreateSettings();
            var settlements = GenerateSettlements(settings, 441002, out _);

            foreach (var settlement in settlements)
            {
                for (var i = 0; i < settlement.Buildings.Count; i++)
                {
                    var left = settlement.Buildings[i];
                    var leftBounds = BuildingPlacementSolver.ResolveBounds(left.WorldPosition, left.FootprintSize, left.RotationDegrees, 0f);
                    for (var j = i + 1; j < settlement.Buildings.Count; j++)
                    {
                        var right = settlement.Buildings[j];
                        var rightBounds = BuildingPlacementSolver.ResolveBounds(right.WorldPosition, right.FootprintSize, right.RotationDegrees, 0f);
                        Assert.IsFalse(leftBounds.Intersects(rightBounds), $"{settlement.Name}: {left.Id} overlaps {right.Id}");
                    }
                }
            }
        }

        [Test]
        public void SettlementSites_KeepMinimumDistance()
        {
            var settings = CreateSettings();
            var settlements = GenerateSettlements(settings, 78124, out _);
            var minimum = settings.Settlements.MinimumSettlementDistance;

            for (var i = 0; i < settlements.Count; i++)
            {
                for (var j = i + 1; j < settlements.Count; j++)
                {
                    Assert.GreaterOrEqual(
                        Vector2.Distance(settlements[i].WorldPosition, settlements[j].WorldPosition),
                        minimum,
                        $"{settlements[i].Name} too close to {settlements[j].Name}");
                }
            }
        }

        [Test]
        public void RoadNetwork_ConnectsRequiredSettlementAndTransitionNodes()
        {
            var settings = CreateSettings();
            var settlements = GenerateSettlements(settings, 90443, out var masks);
            var poi = new PointOfInterestGenerator().Generate(settings, new ProceduralTerrainSampler(settings, 90443), settings.PointsOfInterest, settlements, masks, 90443);
            var roadConfig = CreateRoadConfig(32);
            var network = new RoadNetworkGenerator().Generate(settings, new ProceduralTerrainSampler(settings, 90443), roadConfig, settings.PointsOfInterest, settlements, poi, masks, 90443);

            Assert.Greater(network.Segments.Count, 0);
            foreach (var node in network.Nodes)
            {
                if (node.Type != RoadNodeType.Settlement && node.Type != RoadNodeType.LocationTransition)
                {
                    continue;
                }

                Assert.IsTrue(HasSegment(network, node.Id), $"Required road node has no edge: {node.Id}");
            }
        }

        [Test]
        public void RoadPathfinder_AvoidsForbiddenZonesWhenPossible()
        {
            var settings = CreateSettings(512f);
            var sampler = new FlatTerrainSampler(settings.GetWorldBounds(), 28f);
            var masks = new WorldGenerationMaskSet(24f);
            masks.AddZone(new GenerationMaskZone("blocked", GenerationZoneKind.NoSpawn, Vector2.zero, 80f, 1f, "test"));
            var roadConfig = CreateRoadConfig(36);
            SetPrivateField(roadConfig, "forbiddenZonePenalty", 5000f);

            var result = new TerrainAwareRoadPathfinder().FindPath(
                new Vector2(-220f, 0f),
                new Vector2(220f, 0f),
                settings,
                sampler,
                masks,
                roadConfig,
                roadConfig.Resolve(RoadType.DirtRoad));

            foreach (var point in result.Points)
            {
                Assert.Greater(Vector2.Distance(point, Vector2.zero), 60f);
            }
        }

        [Test]
        public void RoadSlopeCost_PrefersSmootherRoute()
        {
            var config = CreateRoadConfig(24);
            var roadType = config.Resolve(RoadType.DirtRoad);
            var pathfinder = new TerrainAwareRoadPathfinder();

            var smooth = pathfinder.EvaluateStepCostForTests(10f, 5f, false, config, roadType);
            var steep = pathfinder.EvaluateStepCostForTests(10f, 38f, false, config, roadType);

            Assert.Greater(steep, smooth * 2f);
        }

        [Test]
        public void PointOfInterestPlacement_RespectsSettlementFootprints()
        {
            var settings = CreateSettings();
            var settlements = GenerateSettlements(settings, 334455, out var masks);
            var poi = new PointOfInterestGenerator().Generate(settings, new ProceduralTerrainSampler(settings, 334455), settings.PointsOfInterest, settlements, masks, 334455);

            foreach (var point in poi)
            {
                foreach (var settlement in settlements)
                {
                    Assert.Greater(
                        Vector2.Distance(point.WorldPosition, settlement.WorldPosition),
                        settlement.Radius,
                        $"{point.Name} spawned inside {settlement.Name}");
                }
            }
        }

        [Test]
        public void Masks_RejectVegetationSpawnInOccupiedZones()
        {
            var masks = new WorldGenerationMaskSet(16f);
            masks.AddZone(new GenerationMaskZone("building", GenerationZoneKind.NoSpawn, new Vector2(10f, 10f), 8f, 1f, "test"));
            masks.AddPath(new GenerationPathMask("road", GenerationZoneKind.Road, new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) }, 3f, 1f, "road"));

            Assert.IsTrue(masks.IsNoSpawn(new Vector2(10f, 10f)));
            Assert.IsTrue(masks.IsNoSpawn(Vector2.zero));
            Assert.IsFalse(masks.IsNoSpawn(new Vector2(40f, 40f)));
        }

        [Test]
        public void GeneratedSettlementPrefabs_HaveBuildingMetadata()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Prefab/Generated/Settlements/Blacksmith_Level1.prefab");

            Assert.NotNull(prefab);
            var metadata = prefab.GetComponent<SettlementBuildingPrefabMetadata>();
            Assert.NotNull(metadata);
            Assert.AreEqual(BuildingType.Blacksmith, metadata.BuildingType);
            Assert.AreEqual(1, metadata.Level);
            Assert.Greater(metadata.FootprintSize.x, 0f);
        }

        [Test]
        public void SettlementCatalog_ReferencesModelPrefabAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SettlementBuildingCatalog>("Assets/Resources/ProceduralGeneration/DefaultSettlementBuildingCatalog.asset");

            Assert.NotNull(catalog);
            foreach (var entry in catalog.Buildings)
            {
                Assert.NotNull(entry.Prefab, $"Missing prefab for {entry.PrefabKey}");
                var path = AssetDatabase.GetAssetPath(entry.Prefab);
                Assert.IsTrue(path.StartsWith("Assets/Models/Prefab/Generated/Settlements/", StringComparison.Ordinal), path);
            }
        }

        [Test]
        public void GeneratedPoiPrefabs_HavePoiMetadata()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Prefab/Generated/POI/CaveEntrance_Level1.prefab");

            Assert.NotNull(prefab);
            var metadata = prefab.GetComponent<PointOfInterestPrefabMetadata>();
            Assert.NotNull(metadata);
            Assert.AreEqual(PointOfInterestType.CaveEntrance, metadata.PointOfInterestType);
            Assert.Greater(metadata.Radius, 0f);
        }

        [Test]
        public void PointOfInterestCatalog_ReferencesModelPrefabAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config.PointOfInterestCatalog>("Assets/Resources/ProceduralGeneration/DefaultPointOfInterestCatalog.asset");

            Assert.NotNull(catalog);
            foreach (var entry in catalog.Entries)
            {
                Assert.NotNull(entry.Prefab, $"Missing prefab for {entry.PrefabKey}");
                var path = AssetDatabase.GetAssetPath(entry.Prefab);
                Assert.IsTrue(path.StartsWith("Assets/Models/Prefab/Generated/POI/", StringComparison.Ordinal), path);
            }
        }

        [Test]
        public void RuntimeSpawner_DoesNotCreateSeparateRoadMeshes()
        {
            var settings = CreateSettings(256f);
            var root = new GameObject("RuntimeRoadSpawnerTestRoot").transform;

            try
            {
                var context = new GenerationContext(settings, 77, root)
                {
                    TerrainSampler = new FlatTerrainSampler(settings.GetWorldBounds(), 12f),
                    WorldLayers = new WorldGenerationLayers(
                        Array.Empty<GeneratedSettlement>(),
                        Array.Empty<GeneratedPointOfInterest>(),
                        new GeneratedRoadNetwork(
                            Array.Empty<GeneratedRoadNode>(),
                            new[]
                            {
                                new GeneratedRoadSegment(
                                    "test-road",
                                    "a",
                                    "b",
                                    RoadType.DirtRoad,
                                    new[] { new Vector2(-40f, 0f), new Vector2(40f, 0f) },
                                    6f,
                                    1f)
                            }),
                        new WorldGenerationMaskSet(),
                        new Dictionary<string, double>())
                };

                WorldFeatureRuntimeSpawner.Spawn(context);

                var filters = root.GetComponentsInChildren<MeshFilter>();
                for (var i = 0; i < filters.Length; i++)
                {
                    Assert.IsFalse(filters[i].name.Contains("road", StringComparison.OrdinalIgnoreCase), filters[i].name);
                }

                Assert.IsNull(root.Find("GeneratedWorldFeatures/Roads"), "Roads must be terrain carving/painting, not separate mesh objects.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void RuntimeSpawner_LoadsFallbackCatalogAndSpawnsSettlementBuildingMesh()
        {
            var settings = CreateSettings(256f);
            var root = new GameObject("RuntimeSettlementSpawnerTestRoot").transform;

            try
            {
                var definition = new GeneratedSettlementBuildingDefinition(
                    "StoneHouse_Level1",
                    "Stone House",
                    BuildingType.House,
                    3,
                    SettlementTier.City,
                    new Vector2(11f, 10f),
                    Array.Empty<BuildingTag>(),
                    "StoneHouse_Level1");
                var building = new GeneratedSettlementBuilding(
                    "building-stone-house",
                    definition,
                    "core",
                    new Vector2(8f, -6f),
                    35f,
                    definition.FootprintSize);
                var settlement = new GeneratedSettlement(
                    "city-test",
                    "Testopolis",
                    SettlementTier.City,
                    null,
                    Vector2.zero,
                    80f,
                    Array.Empty<GeneratedSettlementDistrict>(),
                    new[] { building },
                    Array.Empty<GeneratedSettlementRoadSegment>(),
                    null,
                    null,
                    null,
                    null);
                var context = new GenerationContext(settings, 79, root)
                {
                    TerrainSampler = new FlatTerrainSampler(settings.GetWorldBounds(), 14f),
                    WorldLayers = new WorldGenerationLayers(
                        new[] { settlement },
                        Array.Empty<GeneratedPointOfInterest>(),
                        new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), Array.Empty<GeneratedRoadSegment>()),
                        new WorldGenerationMaskSet(),
                        new Dictionary<string, double>())
                };

                WorldFeatureRuntimeSpawner.Spawn(context);

                var metadata = root.GetComponentInChildren<SettlementBuildingPrefabMetadata>();
                Assert.NotNull(metadata, "GeneratedSettlement building prefab was not instantiated.");
                Assert.AreEqual(BuildingType.House, metadata.BuildingType);
                Assert.Greater(root.GetComponentsInChildren<MeshRenderer>().Length, 0, "Spawned settlement prefab has no visible mesh renderer.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void TerrainRoadCarver_CutsHillIntoSmoothedTerrainBench()
        {
            var settings = CreateSettings(200f);
            settings.ConfigureTerrain(65, 80f, 160f, 3, 0.42f, 2f, 1f, TerrainLandShape.Mainland, 0f, 0f, 0f, 0f, 0f, false, 0.82f, 2.2f);
            var sampler = new HillTerrainSampler(settings.GetWorldBounds(), settings.TerrainHeight);
            var roadConfig = CreateRoadConfig(32);
            var road = new GeneratedRoadSegment(
                "terrain-road",
                "a",
                "b",
                RoadType.DirtRoad,
                new[] { new Vector2(-80f, 0f), new Vector2(80f, 0f) },
                8f,
                1f);
            var network = new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), new[] { road });
            var context = RoadTerrainCarvingContext.Build(settings, sampler, roadConfig, network);
            var heights = sampler.BuildHeightMap(65, -100f, -100f, 200f, 200f);
            var originalCenter = heights[32, 32];
            var originalFarEdge = heights[52, 32];

            var changed = TerrainRoadCarver.Apply(heights, -100f, -100f, 200f, 200f, settings, context);

            Assert.IsTrue(changed);
            Assert.Less(heights[32, 32], originalCenter - 0.08f, "Road center should cut the hill instead of riding over the bump.");
            Assert.AreEqual(originalFarEdge, heights[52, 32], 0.02f, "Terrain outside the road shoulder should remain almost unchanged.");
            Assert.Less(Mathf.Abs(heights[31, 32] - heights[33, 32]), 0.025f, "Road bench should reduce cross-slope across the road width.");
        }

        [Test]
        public void TerrainRoadCarver_ModifiesWideShoulderAndOuterSmoothingZone()
        {
            var settings = CreateSettings(200f);
            settings.ConfigureTerrain(129, 80f, 160f, 3, 0.42f, 2f, 1f, TerrainLandShape.Mainland, 0f, 0f, 0f, 0f, 0f, false, 0.82f, 2.2f);
            var sampler = new HillTerrainSampler(settings.GetWorldBounds(), settings.TerrainHeight);
            var roadConfig = CreateRoadConfig(32);
            var road = new GeneratedRoadSegment(
                "wide-road",
                "a",
                "b",
                RoadType.DirtRoad,
                new[] { new Vector2(-80f, 0f), new Vector2(80f, 0f) },
                4f,
                1f);
            var network = new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), new[] { road });
            var context = RoadTerrainCarvingContext.Build(settings, sampler, roadConfig, network);
            var heights = sampler.BuildHeightMap(129, -100f, -100f, 200f, 200f);
            var shoulderOriginal = heights[68, 64];
            var outerOriginal = heights[73, 64];
            var farOriginal = heights[84, 64];

            TerrainRoadCarver.Apply(heights, -100f, -100f, 200f, 200f, settings, context);

            Assert.Greater(Mathf.Abs(heights[68, 64] - shoulderOriginal), 0.002f, "Shoulder zone must be modified, not left as a hard edge.");
            Assert.Greater(Mathf.Abs(heights[73, 64] - outerOriginal), 0.0005f, "Outer smoothing zone must receive gentle smoothing to prevent terrain ribs.");
            Assert.AreEqual(farOriginal, heights[84, 64], 0.002f, "Terrain outside total road influence should stay unchanged.");
        }

        [Test]
        public void TerrainRoadCarver_LimitsNeighborDeltasAroundCutRoad()
        {
            var settings = CreateSettings(200f);
            settings.ConfigureTerrain(129, 80f, 160f, 3, 0.42f, 2f, 1f, TerrainLandShape.Mainland, 0f, 0f, 0f, 0f, 0f, false, 0.82f, 2.2f);
            var sampler = new SpikyHillTerrainSampler(settings.GetWorldBounds(), settings.TerrainHeight);
            var roadConfig = CreateRoadConfig(32);
            var road = new GeneratedRoadSegment(
                "spiky-road",
                "a",
                "b",
                RoadType.DirtRoad,
                new[] { new Vector2(-80f, 0f), new Vector2(80f, 0f) },
                4f,
                1f);
            var network = new GeneratedRoadNetwork(Array.Empty<GeneratedRoadNode>(), new[] { road });
            var context = RoadTerrainCarvingContext.Build(settings, sampler, roadConfig, network);
            var heights = sampler.BuildHeightMap(129, -100f, -100f, 200f, 200f);
            var before = MaxAdjacentDelta(heights, 54, 74, 54, 74);

            TerrainRoadCarver.Apply(heights, -100f, -100f, 200f, 200f, settings, context);

            var after = MaxAdjacentDelta(heights, 54, 74, 54, 74);
            Assert.Less(after, before * 0.72f, "Road carving should smooth local spike gradients instead of leaving pyramid waves.");
            Assert.Less(Mathf.Abs(heights[64, 62] - heights[64, 66]), 0.01f, "Road bed should be flat across width after carving.");
        }

        [Test]
        public void SettlementTerrainCarver_FlattensPlatformAndKeepsWideSmoothEdge()
        {
            var settings = CreateSettings(200f);
            settings.ConfigureTerrain(129, 80f, 160f, 3, 0.42f, 2f, 1f, TerrainLandShape.Mainland, 0f, 0f, 0f, 0f, 0f, false, 0.82f, 2.2f);
            var sampler = new SpikyHillTerrainSampler(settings.GetWorldBounds(), settings.TerrainHeight);
            var settlement = CreateSettlement("test_village", SettlementTier.Village, Vector2.zero, 34f);
            var heights = sampler.BuildHeightMap(129, -100f, -100f, 200f, 200f);
            var shoulderOriginal = heights[88, 64];
            var farOriginal = heights[128, 128];

            SettlementTerrainCarver.Apply(
                heights,
                -100f,
                -100f,
                200f,
                200f,
                settings,
                new[] { settlement },
                sampler);

            Assert.Less(Mathf.Abs(heights[64, 54] - heights[64, 74]), 0.004f, "GeneratedSettlement platform should be flat enough for buildings.");
            Assert.Greater(Mathf.Abs(heights[88, 64] - shoulderOriginal), 0.001f, "GeneratedSettlement shoulder must be modified instead of leaving a hard height edge.");
            Assert.AreEqual(farOriginal, heights[128, 128], 0.003f, "Terrain beyond settlement influence should remain unchanged.");
            var maxDelta = MaxAdjacentDelta(heights, 50, 82, 50, 82, out var from, out var to);
            Assert.Less(maxDelta, 0.022f, $"GeneratedSettlement flattening should not create pyramid waves around the platform. Worst edge {from}->{to}.");
        }

        [Test]
        public void RoadMaskRasterizer_KeepsVegetationAtLeastOneMeterAwayFromRoad()
        {
            var masks = new WorldGenerationMaskSet(16f);
            var road = new GeneratedRoadSegment(
                "mask-road",
                "a",
                "b",
                RoadType.DirtRoad,
                new[] { new Vector2(-50f, 0f), new Vector2(50f, 0f) },
                4f,
                1f);

            RoadMaskRasterizer.AddToMask(masks, road);

            Assert.IsTrue(masks.IsNoSpawn(new Vector2(0f, 2.99f)), "Vegetation must be rejected at least one meter beyond the visible road edge.");
            Assert.Greater(masks.Evaluate(new Vector2(0f, 5f), GenerationZoneKind.ReducedVegetation), 0.01f, "Vegetation density should still be reduced outside the hard road no-spawn band.");
        }

        private static IReadOnlyList<GeneratedSettlement> GenerateSettlements(ProceduralLocationSettings settings, int seed, out WorldGenerationMaskSet masks)
        {
            masks = new WorldGenerationMaskSet(32f);
            var sampler = new ProceduralTerrainSampler(settings, seed);
            return new SettlementGenerator().Generate(settings, sampler, settings.Settlements, masks, seed);
        }

        private static ProceduralLocationSettings CreateSettings(float size = 1400f)
        {
            var settings = ScriptableObject.CreateInstance<ProceduralLocationSettings>();
            settings.ConfigureGlobal(size, size);
            settings.ConfigureSeed(SeedMode.Fixed, 1);
            settings.ConfigureTerrain(257, 120f, 280f, 4, 0.45f, 2f, 1f, TerrainLandShape.Mainland, 0.18f, 0.16f, 0.08f, 0.02f, 0.04f, false, 0.82f, 2.2f);
            settings.ConfigureWater(false, 0f, 0f, Color.clear);
            return settings;
        }

        private static RoadGenerationConfig CreateRoadConfig(int resolution)
        {
            var config = new RoadGenerationConfig();
            SetPrivateField(config, "pathfindingGridResolution", resolution);
            SetPrivateField(config, "slopePenalty", 45f);
            return config;
        }

        private static GeneratedSettlement CreateSettlement(string id, SettlementTier tier, Vector2 position, float radius)
        {
            return new GeneratedSettlement(
                id,
                id,
                tier,
                null,
                position,
                radius,
                Array.Empty<GeneratedSettlementDistrict>(),
                Array.Empty<GeneratedSettlementBuilding>(),
                Array.Empty<GeneratedSettlementRoadSegment>(),
                null,
                null,
                null,
                null);
        }

        private static GeneratedSettlementBuildingDefinition Find(IReadOnlyList<GeneratedSettlementBuildingDefinition> definitions, BuildingType type, int level)
        {
            for (var i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Type == type && definitions[i].Level == level)
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private static bool HasSegment(GeneratedRoadNetwork network, string nodeId)
        {
            for (var i = 0; i < network.Segments.Count; i++)
            {
                if (network.Segments[i].FromNodeId == nodeId || network.Segments[i].ToNodeId == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static float MaxAdjacentDelta(float[,] heights, int minX, int maxX, int minZ, int maxZ)
        {
            return MaxAdjacentDelta(heights, minX, maxX, minZ, maxZ, out _, out _);
        }

        private static float MaxAdjacentDelta(
            float[,] heights,
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            out Vector2Int from,
            out Vector2Int to)
        {
            var maxDelta = 0f;
            from = default;
            to = default;
            for (var z = minZ; z <= maxZ; z++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (x + 1 < heights.GetLength(1))
                    {
                        var delta = Mathf.Abs(heights[z, x] - heights[z, x + 1]);
                        if (delta > maxDelta)
                        {
                            maxDelta = delta;
                            from = new Vector2Int(x, z);
                            to = new Vector2Int(x + 1, z);
                        }
                    }

                    if (z + 1 < heights.GetLength(0))
                    {
                        var delta = Mathf.Abs(heights[z, x] - heights[z + 1, x]);
                        if (delta > maxDelta)
                        {
                            maxDelta = delta;
                            from = new Vector2Int(x, z);
                            to = new Vector2Int(x, z + 1);
                        }
                    }
                }
            }

            return maxDelta;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing private field {fieldName} on {typeof(T).Name}");
            field.SetValue(target, value);
        }

        private sealed class FlatTerrainSampler : IProceduralTerrainSampler
        {
            private readonly float height;

            public FlatTerrainSampler(Bounds bounds, float height)
            {
                WorldBounds = bounds;
                this.height = height;
            }

            public Bounds WorldBounds { get; }

            public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
            {
                point = new Vector3(worldX, height, worldZ);
                normal = Vector3.up;
                return worldX >= WorldBounds.min.x &&
                       worldX <= WorldBounds.max.x &&
                       worldZ >= WorldBounds.min.z &&
                       worldZ <= WorldBounds.max.z;
            }

            public float SampleHeight01(float worldX, float worldZ)
            {
                return 0.25f;
            }

            public float SampleHeightMeters(float worldX, float worldZ)
            {
                return height;
            }

            public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
            {
                return Vector3.up;
            }

            public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
            {
                var heights = new float[resolution, resolution];
                for (var y = 0; y < resolution; y++)
                {
                    for (var x = 0; x < resolution; x++)
                    {
                        heights[y, x] = 0.25f;
                    }
                }

                return heights;
            }
        }

        private sealed class HillTerrainSampler : IProceduralTerrainSampler
        {
            private readonly float terrainHeight;

            public HillTerrainSampler(Bounds bounds, float terrainHeight)
            {
                WorldBounds = bounds;
                this.terrainHeight = terrainHeight;
            }

            public Bounds WorldBounds { get; }

            public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
            {
                point = new Vector3(worldX, SampleHeightMeters(worldX, worldZ), worldZ);
                normal = SampleNormal(worldX, worldZ);
                return worldX >= WorldBounds.min.x &&
                       worldX <= WorldBounds.max.x &&
                       worldZ >= WorldBounds.min.z &&
                       worldZ <= WorldBounds.max.z;
            }

            public float SampleHeight01(float worldX, float worldZ)
            {
                return Mathf.Clamp01(SampleHeightMeters(worldX, worldZ) / Mathf.Max(1f, terrainHeight));
            }

            public float SampleHeightMeters(float worldX, float worldZ)
            {
                var hill = Mathf.Exp(-(worldX * worldX + worldZ * worldZ * 0.25f) / 900f);
                return 18f + hill * 34f;
            }

            public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
            {
                var left = SampleHeightMeters(worldX - sampleDistance, worldZ);
                var right = SampleHeightMeters(worldX + sampleDistance, worldZ);
                var down = SampleHeightMeters(worldX, worldZ - sampleDistance);
                var up = SampleHeightMeters(worldX, worldZ + sampleDistance);
                return new Vector3(left - right, sampleDistance * 2f, down - up).normalized;
            }

            public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
            {
                var heights = new float[resolution, resolution];
                for (var y = 0; y < resolution; y++)
                {
                    var worldZ = minZ + length * (y / (float)Mathf.Max(1, resolution - 1));
                    for (var x = 0; x < resolution; x++)
                    {
                        var worldX = minX + width * (x / (float)Mathf.Max(1, resolution - 1));
                        heights[y, x] = SampleHeight01(worldX, worldZ);
                    }
                }

                return heights;
            }
        }

        private sealed class SpikyHillTerrainSampler : IProceduralTerrainSampler
        {
            private readonly float terrainHeight;

            public SpikyHillTerrainSampler(Bounds bounds, float terrainHeight)
            {
                WorldBounds = bounds;
                this.terrainHeight = terrainHeight;
            }

            public Bounds WorldBounds { get; }

            public bool TrySample(float worldX, float worldZ, out Vector3 point, out Vector3 normal)
            {
                point = new Vector3(worldX, SampleHeightMeters(worldX, worldZ), worldZ);
                normal = SampleNormal(worldX, worldZ);
                return worldX >= WorldBounds.min.x &&
                       worldX <= WorldBounds.max.x &&
                       worldZ >= WorldBounds.min.z &&
                       worldZ <= WorldBounds.max.z;
            }

            public float SampleHeight01(float worldX, float worldZ)
            {
                return Mathf.Clamp01(SampleHeightMeters(worldX, worldZ) / Mathf.Max(1f, terrainHeight));
            }

            public float SampleHeightMeters(float worldX, float worldZ)
            {
                var broadHill = Mathf.Exp(-(worldX * worldX + worldZ * worldZ * 0.35f) / 1300f) * 18f;
                var sharpBump = Mathf.Exp(-(worldX * worldX + worldZ * worldZ) / 42f) * 34f;
                return 16f + broadHill + sharpBump;
            }

            public Vector3 SampleNormal(float worldX, float worldZ, float sampleDistance = 2f)
            {
                var left = SampleHeightMeters(worldX - sampleDistance, worldZ);
                var right = SampleHeightMeters(worldX + sampleDistance, worldZ);
                var down = SampleHeightMeters(worldX, worldZ - sampleDistance);
                var up = SampleHeightMeters(worldX, worldZ + sampleDistance);
                return new Vector3(left - right, sampleDistance * 2f, down - up).normalized;
            }

            public float[,] BuildHeightMap(int resolution, float minX, float minZ, float width, float length)
            {
                var heights = new float[resolution, resolution];
                for (var y = 0; y < resolution; y++)
                {
                    var worldZ = minZ + length * (y / (float)Mathf.Max(1, resolution - 1));
                    for (var x = 0; x < resolution; x++)
                    {
                        var worldX = minX + width * (x / (float)Mathf.Max(1, resolution - 1));
                        heights[y, x] = SampleHeight01(worldX, worldZ);
                    }
                }

                return heights;
            }
        }
    }
}
#endif
