using System;
using System.Collections.Generic;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.PointsOfInterest.Config
{
    [CreateAssetMenu(
        fileName = "PointOfInterestCatalog",
        menuName = "Legends of War and Magic/Procedural Generation/Point Of Interest Catalog",
        order = 11)]
    public sealed class PointOfInterestCatalog : ScriptableObject
    {
        [SerializeField] private PointOfInterestConfigEntry[] entries = Array.Empty<PointOfInterestConfigEntry>();

        public IReadOnlyList<PointOfInterestConfigEntry> Entries => entries;

        public IReadOnlyList<PointOfInterestConfigEntry> ResolveEntries()
        {
            if (entries == null || entries.Length == 0)
            {
                return DefaultPointOfInterestCatalog.CreateEntries();
            }

            return entries;
        }
    }
}
