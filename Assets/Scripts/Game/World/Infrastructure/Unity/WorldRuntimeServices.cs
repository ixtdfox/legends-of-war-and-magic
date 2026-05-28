using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Infrastructure.Persistence;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.Generator.Location;
using LegendsOfWarAndMagic.Generator.World;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Unity
{
    public static class WorldRuntimeServices
    {
        public static IWorldRepository CreateRepository()
        {
            return new JsonWorldRepository();
        }

        public static NewWorldUseCase CreateNewWorldUseCase()
        {
            var repository = CreateRepository();
            return new NewWorldUseCase(
                new TopDownWorldGenerator(),
                new TextureWorldMapRenderer(),
                new LegacyLocationTerrainGenerator(),
                new TextureLocationMapRenderer(),
                repository);
        }

        public static LoadWorldUseCase CreateLoadWorldUseCase()
        {
            return new LoadWorldUseCase(CreateRepository());
        }

        public static WorldService CreateWorldService()
        {
            return new WorldService(CreateRepository());
        }
    }
}
