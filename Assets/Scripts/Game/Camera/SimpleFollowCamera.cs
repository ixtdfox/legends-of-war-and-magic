using UnityEngine;

namespace LegendsOfWarAndMagic.Game.Camera
{
    [DisallowMultipleComponent]
    public sealed class SimpleFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 7.5f, -10.5f);
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private float lookAtHeight = 1.35f;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            if (target != null)
            {
                transform.position = target.position + offset;
                LookAtTarget();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var desiredPosition = target.position + offset;
            var t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
            LookAtTarget();
        }

        private void LookAtTarget()
        {
            transform.LookAt(target.position + Vector3.up * lookAtHeight, Vector3.up);
        }
    }
}
