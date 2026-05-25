using UnityEngine;
using UnityEngine.InputSystem;

namespace LegendsOfWarAndMagic.Game.Player
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class SimplePlayerController : MonoBehaviour
    {
        [SerializeField] private float walkSpeed = 6f;
        [SerializeField] private float sprintMultiplier = 1.55f;
        [SerializeField] private float jumpHeight = 2.2f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float rotationSpeed = 12f;

        private CharacterController controller;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            var moveInput = ReadMoveInput();
            var jumpPressed = ReadJumpPressed();
            var sprinting = ReadSprintPressed();

            var moveDirection = BuildCameraRelativeDirection(moveInput);
            var speed = walkSpeed * (sprinting ? sprintMultiplier : 1f);
            var horizontalVelocity = moveDirection * speed;

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpPressed && controller.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalVelocity += gravity * Time.deltaTime;

            var motion = horizontalVelocity;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            if (moveDirection.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        private static Vector2 ReadMoveInput()
        {
            var input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                input += gamepad.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private static bool ReadJumpPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        }

        private static bool ReadSprintPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            {
                return true;
            }

            var gamepad = Gamepad.current;
            return gamepad != null && gamepad.leftStickButton.isPressed;
        }

        private Vector3 BuildCameraRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var cameraTransform = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform : transform;
            var forward = cameraTransform.forward;
            var right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }
    }
}
