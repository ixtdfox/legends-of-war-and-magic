using System;
using LegendsOfWarAndMagic.Game.World.Application;
using LegendsOfWarAndMagic.Game.World.Domain;
using UnityEngine;

namespace LegendsOfWarAndMagic.Game.World.Infrastructure.Unity
{
    public static class GeneratedWorldRuntimeState
    {
        private const string WorldIdKey = "GeneratedWorldRuntime.WorldId";
        private const string CurrentLocationIdKey = "GeneratedWorldRuntime.CurrentLocationId";
        private const string PreviousLocationIdKey = "GeneratedWorldRuntime.PreviousLocationId";
        private const string EntryDirectionKey = "GeneratedWorldRuntime.EntryDirection";

        public static void SaveCurrentSession()
        {
            var world = GeneratedWorldSession.CurrentWorld;
            if (world == null)
            {
                return;
            }

            var currentLocation = GeneratedWorldSession.CurrentLocation ?? world.StartLocation;
            Save(
                world.Id,
                currentLocation?.Id ?? default,
                GeneratedWorldSession.PreviousLocationId,
                GeneratedWorldSession.EntryDirection);
        }

        public static void Save(WorldId worldId, LocationId currentLocationId, LocationId previousLocationId, WorldDirection entryDirection)
        {
            if (string.IsNullOrWhiteSpace(worldId.Value))
            {
                return;
            }

            PlayerPrefs.SetString(WorldIdKey, worldId.Value);
            PlayerPrefs.SetString(CurrentLocationIdKey, currentLocationId.Value ?? string.Empty);
            PlayerPrefs.SetString(PreviousLocationIdKey, previousLocationId.Value ?? string.Empty);
            PlayerPrefs.SetString(EntryDirectionKey, entryDirection.ToString());
            PlayerPrefs.Save();
        }

        public static bool TryRestoreSession()
        {
            if (GeneratedWorldSession.HasWorld)
            {
                return true;
            }

            var worldIdValue = PlayerPrefs.GetString(WorldIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(worldIdValue))
            {
                return false;
            }

            try
            {
                var world = WorldRuntimeServices.CreateLoadWorldUseCase().Execute(new WorldId(worldIdValue));
                GeneratedWorldSession.Start(world);

                var currentLocationValue = PlayerPrefs.GetString(CurrentLocationIdKey, string.Empty);
                if (!string.IsNullOrWhiteSpace(currentLocationValue))
                {
                    var currentLocationId = new LocationId(currentLocationValue);
                    if (world.FindLocation(currentLocationId) != null)
                    {
                        var previousLocationValue = PlayerPrefs.GetString(PreviousLocationIdKey, string.Empty);
                        var previousLocationId = string.IsNullOrWhiteSpace(previousLocationValue)
                            ? currentLocationId
                            : new LocationId(previousLocationValue);
                        var entryDirection = ParseDirection(PlayerPrefs.GetString(EntryDirectionKey, string.Empty));
                        GeneratedWorldSession.Enter(currentLocationId, previousLocationId, entryDirection);
                    }
                }

                return GeneratedWorldSession.HasWorld;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to restore generated world session '{worldIdValue}': {exception.Message}");
                return false;
            }
        }

        private static WorldDirection ParseDirection(string value)
        {
            return Enum.TryParse<WorldDirection>(value, out var direction)
                ? direction
                : WorldDirection.South;
        }
    }
}
