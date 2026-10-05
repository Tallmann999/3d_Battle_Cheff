using ChefShow.Core;
using ChefShow.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChefShow.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonRig : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        private PrototypeGameConfig config;
        private InputActionAsset input;
        private CharacterController controller;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 cameraHome;
        private float initialPitch;
        private float pitch;
        private float verticalSpeed;
        private float sensitivity;
        private bool initialized;
        public bool Focused { get; private set; }
        public PrototypeInteractable Target { get; private set; }
        public Camera ViewCamera => viewCamera;

        public void Configure(Camera camera) => viewCamera = camera;

        public void Initialize(PrototypeGameConfig settings, InputActionAsset actions)
        {
            config = settings;
            sensitivity = settings.LookSensitivity;
            input = actions;
            controller = GetComponent<CharacterController>();
            startPosition = transform.position;
            startRotation = transform.rotation;
            cameraHome = viewCamera.transform.localPosition;
            initialPitch = Mathf.DeltaAngle(0, viewCamera.transform.localEulerAngles.x);
            initialized = true;
            ResetRig();
        }

        public void ResetRig()
        {
            if (!initialized) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            controller.enabled = true;
            verticalSpeed = 0;
            pitch = initialPitch;
            ExitFocus();
            Target = null;
        }

        public void SetSensitivity(float value) => sensitivity = Mathf.Clamp(value, 0.01f, 0.5f);

        public void Step(GameClock clock, bool acceptInput)
        {
            if (!initialized || !acceptInput) return;
            var map = input.FindActionMap(Focused ? "Station" : "Gameplay", true);
            var look = map.FindAction("Look", true).ReadValue<Vector2>() * sensitivity;
            if (!Focused)
            {
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -75, 75);
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                var move = map.FindAction("Move", true).ReadValue<Vector2>();
                var speed = map.FindAction("Sprint", true).IsPressed() ? config.RunSpeed : config.WalkSpeed;
                var direction = Vector3.ClampMagnitude(transform.right * move.x + transform.forward * move.y, 1);
                if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed += Physics.gravity.y * clock.Delta;
                controller.Move((direction * speed + Vector3.up * verticalSpeed) * clock.Delta);
            }
            RefreshTarget();
            if (Focused && map.FindAction("Cancel", true).WasPressedThisFrame()) ExitFocus();
            else if (!Focused && map.FindAction("Interact", true).WasPressedThisFrame()
                && Target != null && Target.CanFocus(config.PlayerTeam)) EnterFocus();
        }

        public void RefreshTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            Target = Physics.Raycast(ray, out var hit, config.InteractionDistance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) ? hit.collider.GetComponentInParent<PrototypeInteractable>() : null;
        }

        private void EnterFocus()
        {
            if (Target == null || !Target.CanFocus(config.PlayerTeam)) return;
            Focused = true;
            var point = Target.transform.position + Vector3.up * 0.3f;
            viewCamera.transform.LookAt(point);
        }

        public void ExitFocus()
        {
            Focused = false;
            if (viewCamera == null) return;
            viewCamera.transform.localPosition = cameraHome;
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
