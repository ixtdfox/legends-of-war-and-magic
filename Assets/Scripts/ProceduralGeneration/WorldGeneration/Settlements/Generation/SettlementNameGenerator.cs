using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Pipeline;
using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Settlements.Generation
{
    public static class SettlementNameGenerator
    {
        private static readonly string[] Prefixes =
        {
            "Ash", "Briar", "Crow", "Dawn", "Elder", "Frost", "Gold", "Grey", "High", "Iron", "Mist", "Oak", "Raven", "Stone", "Vale", "White"
        };

        private static readonly string[] Suffixes =
        {
            "ford", "watch", "wick", "mere", "stead", "bridge", "field", "gate", "haven", "hold", "shire", "fall", "reach", "moor", "brook", "wall"
        };

        public static string Generate(SettlementTier tier, int seed, int index)
        {
            var random = new System.Random(DeterministicRandom.Hash(seed, 3109 + index * 37 + (int)tier * 991));
            var name = Prefixes[random.Next(0, Prefixes.Length)] + Suffixes[random.Next(0, Suffixes.Length)];
            return tier switch
            {
                SettlementTier.Camp => $"{name} Camp",
                SettlementTier.Capital => $"{name} Crown",
                _ => name
            };
        }
    }
}
