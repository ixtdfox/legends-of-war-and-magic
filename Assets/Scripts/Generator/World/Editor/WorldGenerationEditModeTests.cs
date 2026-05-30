#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Persistence;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Unity;
using LegendsOfWarAndMagic.Generator.Common;
using LegendsOfWarAndMagic.Generator.Common.Graph;
using LegendsOfWarAndMagic.Generator.Naming;
using LegendsOfWarAndMagic.Game.World.Ports;
using NUnit.Framework;

namespace LegendsOfWarAndMagic.Generator.World.Editor
{
    public sealed class WorldGenerationEditModeTests
    {
        [Test]
        public void FantasyNameGenerator_DoesNotDuplicateWithinSession()
        {
            var generator = new FantasyNameGenerator();
            var random = new SeededRandom(81235);
            var names = new HashSet<string>();

            for (var i = 0; i < 200; i++)
            {
                var name = generator.Next(NameKind.LocationName, random);
                Assert.IsTrue(names.Add(name), $"Duplicate generated name: {name}");
            }
        }

        [Test]
        public void WorldGeneration_IsDeterministicForSameSeedAndConfig()
        {
            var config = new WorldGenerationConfig
            {
                Seed = 424242,
                MinRegions = 10,
                MaxRegions = 10,
                PlayableLocationCount = 15,
                ForcedShape = WorldShapeType.HugeIsland
            };

            var first = new TopDownWorldGenerator().Generate(config);
            var second = new TopDownWorldGenerator().Generate(config);

            Assert.AreEqual(first.Id.Value, second.Id.Value);
            Assert.AreEqual(first.Name.Value, second.Name.Value);
            Assert.AreEqual(first.Shape.Type, second.Shape.Type);
            Assert.AreEqual(first.Locations.Count, second.Locations.Count);

            var firstLocations = first.Locations.ToArray();
            var secondLocations = second.Locations.ToArray();
            for (var i = 0; i < firstLocations.Length; i++)
            {
                Assert.AreEqual(firstLocations[i].Name.Value, secondLocations[i].Name.Value);
                Assert.AreEqual(firstLocations[i].DominantBiome, secondLocations[i].DominantBiome);
                Assert.AreEqual(firstLocations[i].WorldMapPosition.X, secondLocations[i].WorldMapPosition.X, 0.0001f);
                Assert.AreEqual(firstLocations[i].WorldMapPosition.Y, secondLocations[i].WorldMapPosition.Y, 0.0001f);
            }
        }

        [Test]
        public void WorldGenerationConfig_DefaultsToSingleLocationAndClampsSelectionRange()
        {
            var config = new WorldGenerationConfig();

            Assert.AreEqual(1, config.PlayableLocationCount);

            config.PlayableLocationCount = 0;
            Assert.AreEqual(1, config.PlayableLocationCount);

            config.PlayableLocationCount = 99;
            Assert.AreEqual(15, config.PlayableLocationCount);
        }

        [Test]
        public void WorldGeneration_DefaultConfigCreatesSingleStartLocation()
        {
            var world = new TopDownWorldGenerator().Generate(new WorldGenerationConfig
            {
                Seed = 81077,
                ForcedShape = WorldShapeType.HugeIsland
            });

            Assert.AreEqual(1, world.Locations.Count);
            Assert.NotNull(world.StartLocation);
            Assert.IsTrue(world.StartLocation.IsStartLocation);
            Assert.AreEqual(1, world.Map.Points.Count);
        }

        [Test]
        public void SingleLocationWorld_CanRenderLocationMapImage()
        {
            var world = new TopDownWorldGenerator().Generate(new WorldGenerationConfig
            {
                Seed = 81078,
                ForcedShape = WorldShapeType.HugeIsland
            });
            var location = world.StartLocation;
            var outputDirectory = Path.Combine(Path.GetTempPath(), "LegendsOfWarAndMagicTests", world.Id.Value);
            var outputPath = Path.Combine(outputDirectory, "location_map.png");

            try
            {
                var result = new TextureLocationMapRenderer().Render(world, location, outputPath, new LocationMapRenderSettings(256, 256));

                Assert.AreEqual(outputPath, result.ImagePath);
                Assert.IsTrue(File.Exists(outputPath));
                Assert.Greater(new FileInfo(outputPath).Length, 0);
            }
            finally
            {
                if (Directory.Exists(outputDirectory))
                {
                    Directory.Delete(outputDirectory, true);
                }
            }
        }

        [Test]
        public void GeneratedLocationGraph_IsConnectedAndSymmetric()
        {
            var world = new TopDownWorldGenerator().Generate(new WorldGenerationConfig
            {
                Seed = 77701,
                PlayableLocationCount = 15,
                ForcedShape = WorldShapeType.TwoPeninsulas
            });

            var graph = new LocationGraph(world.Locations);
            Assert.IsTrue(graph.IsConnected());
            Assert.IsTrue(graph.HasSymmetricConnections());
            Assert.IsTrue(world.Locations.All(location => location.Connections.Count > 0));
        }

        [Test]
        public void PlayableLocations_AreSpatiallySpread()
        {
            var world = new TopDownWorldGenerator().Generate(new WorldGenerationConfig
            {
                Seed = 99013,
                PlayableLocationCount = 15,
                ForcedShape = WorldShapeType.HugeIsland
            });

            var width = world.Locations.Max(location => location.WorldMapPosition.X) - world.Locations.Min(location => location.WorldMapPosition.X);
            var height = world.Locations.Max(location => location.WorldMapPosition.Y) - world.Locations.Min(location => location.WorldMapPosition.Y);
            Assert.Greater(width, 0.25f);
            Assert.Greater(height, 0.25f);
        }

        [Test]
        public void WorldSaveMapper_RoundTripsWorldStructure()
        {
            var world = new TopDownWorldGenerator().Generate(new WorldGenerationConfig
            {
                Seed = 100300,
                PlayableLocationCount = 15,
                ForcedShape = WorldShapeType.RiverDeltaRegion
            });

            var dto = WorldSaveMapper.ToDto(world);
            var restored = WorldSaveMapper.ToDomain(dto);

            Assert.AreEqual(world.Id.Value, restored.Id.Value);
            Assert.AreEqual(world.Seed.Value, restored.Seed.Value);
            Assert.AreEqual(world.Regions.Count, restored.Regions.Count);
            Assert.AreEqual(world.Locations.Count, restored.Locations.Count);
            Assert.AreEqual(world.Locations.Sum(location => location.Connections.Count), restored.Locations.Sum(location => location.Connections.Count));
        }
    }
}
#endif
