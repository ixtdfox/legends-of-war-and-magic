using LegendsOfWarAndMagic.Game.World.Domain.Settlements;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SettlementBuildingPrefabMetadata : MonoBehaviour
    {
        [SerializeField] private BuildingType buildingType;
        [SerializeField] private int level = 1;
        [SerializeField] private Vector2 footprintSize = Vector2.one;
        [SerializeField] private string prefabKey;

        public BuildingType BuildingType => buildingType;
        public int Level => Mathf.Max(1, level);
        public Vector2 FootprintSize => footprintSize;
        public string PrefabKey => prefabKey;

        public void Configure(BuildingType type, int buildingLevel, Vector2 footprint, string key)
        {
            buildingType = type;
            level = Mathf.Max(1, buildingLevel);
            footprintSize = new Vector2(Mathf.Max(0.5f, footprint.x), Mathf.Max(0.5f, footprint.y));
            prefabKey = key ?? string.Empty;
        }
    }
}
