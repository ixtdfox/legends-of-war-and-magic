using LegendsOfWarAndMagic.Game.World.Domain.PointsOfInterest;
using LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Model;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.WorldGeneration.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PointOfInterestPrefabMetadata : MonoBehaviour
    {
        [SerializeField] private PointOfInterestType pointOfInterestType;
        [SerializeField] private int dangerLevel;
        [SerializeField] private float radius = 8f;
        [SerializeField] private string prefabKey;
        [SerializeField] private string tags;

        public PointOfInterestType PointOfInterestType => pointOfInterestType;
        public int DangerLevel => Mathf.Clamp(dangerLevel, 0, 10);
        public float Radius => Mathf.Max(1f, radius);
        public string PrefabKey => prefabKey;
        public string Tags => tags;

        public void Configure(PointOfInterestType type, int danger, float footprintRadius, string key, string tagList)
        {
            pointOfInterestType = type;
            dangerLevel = Mathf.Clamp(danger, 0, 10);
            radius = Mathf.Max(1f, footprintRadius);
            prefabKey = key ?? string.Empty;
            tags = tagList ?? string.Empty;
        }
    }
}
