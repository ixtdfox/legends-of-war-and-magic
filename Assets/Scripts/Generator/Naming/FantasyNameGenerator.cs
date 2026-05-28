using System;
using System.Collections.Generic;
using LegendsOfWarAndMagic.Generator.Common;

namespace LegendsOfWarAndMagic.Generator.Naming
{
    public enum NameKind
    {
        WorldName,
        RegionName,
        LocationName,
        RiverName,
        MountainRangeName,
        LakeName
    }

    public interface INameGenerator
    {
        string Next(NameKind kind, SeededRandom random);
        void Reserve(string name);
    }

    public sealed class FantasyNameGenerator : INameGenerator
    {
        private readonly HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

        public string Next(NameKind kind, SeededRandom random)
        {
            var safeRandom = random ?? new SeededRandom(1);
            for (var attempt = 0; attempt < 128; attempt++)
            {
                var name = BuildName(kind, safeRandom);
                if (usedNames.Add(name))
                {
                    return name;
                }
            }

            var fallback = $"{BuildName(kind, safeRandom)} {safeRandom.NextInt(2, 999)}";
            usedNames.Add(fallback);
            return fallback;
        }

        public void Reserve(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                usedNames.Add(name.Trim());
            }
        }

        private static string BuildName(NameKind kind, SeededRandom random)
        {
            return kind switch
            {
                NameKind.WorldName => random.Chance(0.52f)
                    ? $"{Pick(random, WorldAdjectives)} {Pick(random, WorldNouns)}"
                    : $"{Pick(random, Prefixes)}{Pick(random, WorldEndings)}",
                NameKind.RegionName => random.Chance(0.42f)
                    ? $"The {Pick(random, RegionAdjectives)} {Pick(random, RegionNouns)}"
                    : $"{Pick(random, Prefixes)}{Pick(random, RegionEndings)}",
                NameKind.LocationName => random.Chance(0.55f)
                    ? $"{Pick(random, Prefixes)} {Pick(random, LocationNouns)}"
                    : $"{Pick(random, Prefixes)}{Pick(random, LocationEndings)}",
                NameKind.RiverName => $"{Pick(random, RiverPrefixes)} {Pick(random, RiverNouns)}",
                NameKind.MountainRangeName => $"{Pick(random, Prefixes)} {Pick(random, MountainNouns)}",
                NameKind.LakeName => $"{Pick(random, LakePrefixes)} {Pick(random, LakeNouns)}",
                _ => $"{Pick(random, Prefixes)} {Pick(random, LocationNouns)}"
            };
        }

        private static string Pick(SeededRandom random, IReadOnlyList<string> values)
        {
            return random.Pick(values);
        }

        private static readonly string[] Prefixes =
        {
            "Ash", "Raven", "Elder", "Moon", "Star", "Iron", "Dagger", "Thorn", "Mist", "Frost",
            "Ember", "Hollow", "Black", "Silver", "Grim", "Wolf", "Dragon", "Witch", "Sun", "Storm",
            "Cinder", "Old Pine", "Mourning", "Bright", "Duskwater", "Red", "White", "Night", "Bracken", "Stone"
        };

        private static readonly string[] LocationNouns =
        {
            "Shore", "March", "Hollow", "Wood", "Fen", "Reach", "Vale", "Crown", "Gorge", "Coast",
            "Harbor", "Watch", "Spire", "Moor", "Isles", "Pass", "Ridge", "Wound", "Barrow", "Crossing",
            "Haven", "Ford", "Cairn", "Overlook", "Landing", "Gate"
        };

        private static readonly string[] LocationEndings =
        {
            "mere", "reach", "fall", "glen", "root", "wake", "vein", "watch", "barrow", "cliff",
            "fen", "strand", "hollow", "haven", "march", "garde"
        };

        private static readonly string[] RegionAdjectives =
        {
            "Ashen", "Silent", "Broken", "Seven-Wind", "Cinderfall", "Moonlit", "Iron", "Sunken",
            "Witchlight", "Dragonbone", "Frostvein", "Blackroot", "Silver", "Mourning"
        };

        private static readonly string[] RegionNouns =
        {
            "Coast", "Woods", "Expanse", "Highlands", "Fen", "Isles", "Marches", "Moor", "Delta",
            "Basin", "Reach", "Vale", "Desert", "Crown", "Shallows"
        };

        private static readonly string[] RegionEndings =
        {
            "woods", "coast", "march", "fen", "highlands", "expanse", "isles", "reach", "basin", "moor"
        };

        private static readonly string[] WorldAdjectives =
        {
            "Sevenfold", "Elder", "Dawnless", "Iron", "Stormbound", "Moonfallen", "Bright", "Ashen"
        };

        private static readonly string[] WorldNouns =
        {
            "Jadewind", "Arkenfall", "Varandor", "Eldoria", "Greyreach", "Sunmere", "Thornwake", "Iravale"
        };

        private static readonly string[] WorldEndings =
        {
            "dor", "mere", "var", "thalas", "rath", "garde", "wyn", "vale", "reach", "fall"
        };

        private static readonly string[] RiverPrefixes =
        {
            "Silver", "Red", "Mist", "Dawn", "Black", "Willow", "Frost", "Ember", "King's", "Widow's"
        };

        private static readonly string[] RiverNouns =
        {
            "Run", "Water", "Vein", "River", "Flow", "Current", "Wash", "Stream"
        };

        private static readonly string[] MountainNouns =
        {
            "Crown Ridge", "Spine", "Peaks", "High Wall", "Teeth", "Range", "Cairns", "Heights"
        };

        private static readonly string[] LakePrefixes =
        {
            "Mirror", "Still", "Blue", "Grey", "Moon", "Cinder", "Old", "Deep"
        };

        private static readonly string[] LakeNouns =
        {
            "Lake", "Mere", "Water", "Basin", "Pool", "Loch"
        };
    }
}
