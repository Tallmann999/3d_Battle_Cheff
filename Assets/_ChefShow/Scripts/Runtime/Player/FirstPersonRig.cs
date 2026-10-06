using ChefShow.Core;
using System;
using ChefShow.Inventory;
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
        private float focusYaw, focusPitch;
        public Func<bool> CancelInteraction;
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
            else
            {
                focusYaw = Mathf.Clamp(focusYaw + look.x, -55, 55);
                focusPitch = Mathf.Clamp(focusPitch - look.y, 5, 65);
                viewCamera.transform.localRotation = Quaternion.Euler(focusPitch, focusYaw, 0);
            }
            RefreshTarget();
            if (map.FindAction("Cancel")?.WasPressedThisFrame() == true)
            {
                if (CancelInteraction?.Invoke() != true && Focused) ExitFocus();
            }
            else if (!Focused && map.FindAction("Interact", true).WasPressedThisFrame()
                && Target != null && Target.GetComponent<InventoryInteractable>() == null && Target.CanFocus(config.PlayerTeam)) EnterFocus();
        }

        public void RefreshTarget()
        {
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            // CharacterController может дать self-hit на границе skin при взгляде
            // вниз. Исключаем игрока/предметы в его руках, сохраняя ближайшую стену.
            Target = null;
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(ray, config.InteractionDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                Target = hit.collider.GetComponentInParent<PrototypeInteractable>();
            }
        }

        private void EnterFocus()
        {
            if (Target == null || !Target.CanFocus(config.PlayerTeam)) return;
            Focused = true;
            var point = Target.transform.position + Vector3.up * 0.3f;
            viewCamera.transform.LookAt(point);
            focusPitch = Mathf.DeltaAngle(0, viewCamera.transform.localEulerAngles.x);
            focusYaw = Mathf.DeltaAngle(0, viewCamera.transform.localEulerAngles.y);
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
