using UnityEngine;
using UnityEngine.SceneManagement;

namespace LegendsOfWarAndMagic.DebugTools.Snapshots
{
    public static class CameraSnapshotSerializer
    {
        public static object CaptureCamera(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            return new
            {
                name = camera.name,
                scene = SceneManager.GetActiveScene().name,
                position = camera.transform.position,
                rotation = camera.transform.rotation,
                eulerAngles = camera.transform.eulerAngles,
                forward = camera.transform.forward,
                right = camera.transform.right,
                up = camera.transform.up,
                fieldOfView = camera.fieldOfView,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane,
                aspect = camera.aspect,
                clearFlags = camera.clearFlags.ToString(),
                cullingMask = camera.cullingMask
            };
        }

        public static object CapturePlayer(GameObject player)
        {
            if (player == null)
            {
                return null;
            }

            var controller = player.GetComponent<CharacterController>();
            return new
            {
                name = player.name,
                tag = player.tag,
                position = player.transform.position,
                rotation = player.transform.rotation,
                eulerAngles = player.transform.eulerAngles,
                forward = player.transform.forward,
                right = player.transform.right,
                up = player.transform.up,
                activeSelf = player.activeSelf,
                characterController = controller != null
                    ? new
                    {
                        controller.height,
                        controller.radius,
                        controller.center,
                        controller.isGrounded,
                        controller.velocity,
                        controller.slopeLimit,
                        controller.stepOffset
                    }
                    : null
            };
        }
    }
}
