using System;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Runtime
{
    [Obsolete("Production generation must use ProceduralEnvironmentAssetCatalog with local Fristy Resources prefabs.")]
    internal static class RuntimePrototypePropFactory
    {
        private static readonly GameObject[] EmptyPrefabs = Array.Empty<GameObject>();
        private static bool loggedFallback;

        public static GameObject[] GetForestPrefabs()
        {
            LogDisabledFallback();
            return EmptyPrefabs;
        }

        public static GameObject[] GetRockPrefabs()
        {
            LogDisabledFallback();
            return EmptyPrefabs;
        }

        public static GameObject[] GetCliffPrefabs()
        {
            LogDisabledFallback();
            return EmptyPrefabs;
        }

        public static GameObject[] GetGroundCoverPrefabs()
        {
            LogDisabledFallback();
            return EmptyPrefabs;
        }

        private static void LogDisabledFallback()
        {
            if (loggedFallback)
            {
                return;
            }

            Debug.LogError(
                "Runtime prototype prop fallback is disabled. Rebuild DefaultEnvironmentAssetCatalog from local Fristy Resources assets.");
            loggedFallback = true;
        }
    }
}
