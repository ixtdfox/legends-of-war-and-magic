using UnityEngine;

namespace LegendsOfWarAndMagic.ProceduralGeneration.Steps
{
    /// <summary>
    /// Lightweight distance-based culling fallback for generated props that do not rely on streaming.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedPropDistanceCulling : MonoBehaviour
    {
        [SerializeField] private float maxDrawDistance = 0f;

        private Renderer[] cachedRenderers;
        private Camera cachedCamera;
        private bool isVisible = true;

        public void Initialize(float maxDistance)
        {
            maxDrawDistance = Mathf.Max(0f, maxDistance);
            CacheRenderers();
            UpdateVisibility();
        }

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnEnable()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0)
            {
                CacheRenderers();
            }
        }

        private void LateUpdate()
        {
            if (maxDrawDistance <= 0f)
            {
                return;
            }

            UpdateVisibility();
        }

        private void CacheRenderers()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void UpdateVisibility()
        {
            var targetCamera = ResolveCamera();
            if (targetCamera == null)
            {
                return;
            }

            var maxSqrDistance = maxDrawDistance * maxDrawDistance;
            var currentSqrDistance = (targetCamera.transform.position - transform.position).sqrMagnitude;
            var shouldBeVisible = currentSqrDistance <= maxSqrDistance;
            if (isVisible == shouldBeVisible)
            {
                return;
            }

            isVisible = shouldBeVisible;
            for (var i = 0; i < cachedRenderers.Length; i++)
            {
                if (cachedRenderers[i] != null)
                {
                    cachedRenderers[i].enabled = shouldBeVisible;
                }
            }
        }

        private Camera ResolveCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
            {
                return cachedCamera;
            }

            cachedCamera = Camera.main;
            return cachedCamera;
        }
    }
}
