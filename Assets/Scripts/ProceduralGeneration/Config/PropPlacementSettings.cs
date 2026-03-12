using System;
using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Config
{
    /// <summary>
    /// Defines placement behavior for a single prop category (trees, rocks, bushes, etc.).
    /// </summary>
    [Serializable]
    public class PropCategoryPlacementSettings
    {
        [SerializeField] private string categoryName = "Trees";
        [SerializeField] private bool enabled = true;
        [SerializeField] private GameObject[] prefabs = Array.Empty<GameObject>();

        [Tooltip("Instances per 10,000 square meters (100m x 100m).")]
        [Min(0f)]
        [SerializeField] private float densityPer10kSqm = 20f;

        [Min(0f)]
        [SerializeField] private float minDistanceBetweenInstances = 3f;

        [SerializeField] private Vector2 allowedSlopeRange = new(0f, 35f);
        [SerializeField] private Vector2 allowedHeightRange = new(0f, 220f);
        [SerializeField] private Vector2 randomScaleRange = new(0.9f, 1.2f);
        [SerializeField] private bool randomYRotation = true;

        [Tooltip("Logs a warning if a prefab in this category does not include an LODGroup component.")]
        [SerializeField] private bool warnIfMissingLodGroup = true;

        [Tooltip("If enabled, generation logs explicit warnings for prefabs without LODGroup in this category.")]
        [SerializeField] private bool expectLodGroup = false;

        [Tooltip("Optional max visible distance in world units. 0 disables generator-side distance culling.")]
        [Min(0f)]
        [SerializeField] private float maxDrawDistance = 0f;

        [Tooltip("Higher values try more candidates before giving up.")]
        [Range(1f, 20f)]
        [SerializeField] private float attemptsMultiplier = 6f;

        public string CategoryName => string.IsNullOrWhiteSpace(categoryName) ? "Props" : categoryName;
        public bool Enabled => enabled;
        public GameObject[] Prefabs => prefabs;
        public float DensityPer10kSqm => densityPer10kSqm;
        public float MinDistanceBetweenInstances => minDistanceBetweenInstances;
        public Vector2 AllowedSlopeRange => NormalizeRange(allowedSlopeRange, 0f, 90f);
        public Vector2 AllowedHeightRange => NormalizeRange(allowedHeightRange, float.MinValue, float.MaxValue);
        public Vector2 RandomScaleRange => NormalizeRange(randomScaleRange, 0.01f, 100f);
        public bool RandomYRotation => randomYRotation;
        public bool WarnIfMissingLodGroup => warnIfMissingLodGroup;
        public bool ExpectLodGroup => expectLodGroup;
        public float MaxDrawDistance => maxDrawDistance;
        public float AttemptsMultiplier => attemptsMultiplier;

        private static Vector2 NormalizeRange(Vector2 range, float minClamp, float maxClamp)
        {
            var min = Mathf.Clamp(Mathf.Min(range.x, range.y), minClamp, maxClamp);
            var max = Mathf.Clamp(Mathf.Max(range.x, range.y), minClamp, maxClamp);
            return new Vector2(min, max);
        }
    }
}
