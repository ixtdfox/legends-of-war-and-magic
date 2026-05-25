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
        [SerializeField] private Transform viewCamera;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gamepadLookSpeed = 140f;
        [SerializeField] private float minPitch = -82f;
        [SerializeField] private float maxPitch = 82f;
        [SerializeField] private bool lockCursorOnStart = true;

        private CharacterController controller;
        private float verticalVelocity;
        private float pitch;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            HandleLook();

            var moveInput = ReadMoveInput();
            var jumpPressed = ReadJumpPressed();
            var sprinting = ReadSprintPressed();

            var moveDirection = BuildPlayerRelativeDirection(moveInput);
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
        }

        public void SetViewCamera(Transform cameraTransform)
        {
            viewCamera = cameraTransform;
            pitch = 0f;
            ApplyCameraPitch();
        }

        private void HandleLook()
        {
            var mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            var gamepadDelta = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;

            var yawDelta = mouseDelta.x * mouseSensitivity;
            var pitchDelta = mouseDelta.y * mouseSensitivity;

            if (gamepadDelta.sqrMagnitude > 0.0001f)
            {
                yawDelta += gamepadDelta.x * gamepadLookSpeed * Time.deltaTime;
                pitchDelta += gamepadDelta.y * gamepadLookSpeed * Time.deltaTime;
            }

            if (Mathf.Abs(yawDelta) <= 0.0001f && Mathf.Abs(pitchDelta) <= 0.0001f)
            {
                return;
            }

            transform.Rotate(Vector3.up, yawDelta, Space.Self);
            pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);
            ApplyCameraPitch();
        }

        private void ApplyCameraPitch()
        {
            if (viewCamera != null)
            {
                viewCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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

        private Vector3 BuildPlayerRelativeDirection(Vector2 input)
        {
            if (input.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return (transform.forward * input.y + transform.right * input.x).normalized;
        }
    }
}
