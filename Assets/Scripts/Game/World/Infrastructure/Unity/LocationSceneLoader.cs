using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using LegendsOfWarAndMagic.Game.World.Ports;
using LegendsOfWarAndMagic.UI.Shared;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Unity
{
    public sealed class LocationSceneLoader : ILocationTransitionService
    {
        public void EnterLocation(LocationId targetLocationId, LocationId fromLocationId, WorldDirection entryDirection)
        {
            if (!GeneratedWorldSession.HasWorld)
            {
                GeneratedWorldRuntimeState.TryRestoreSession();
            }

            if (!GeneratedWorldSession.HasWorld)
            {
                return;
            }

            var targetLocation = GeneratedWorldSession.CurrentWorld.FindLocation(targetLocationId);
            var targetName = targetLocation?.Name.Value ?? "локацию";
            GeneratedWorldSession.Enter(targetLocationId, fromLocationId, entryDirection);
            GeneratedWorldRuntimeState.SaveCurrentSession();
            RuntimeLoadingOverlay.LoadGameScene($"Переходим в «{targetName}»...", false);
        }
    }
}
