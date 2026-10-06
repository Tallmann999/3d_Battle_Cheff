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
        [SerializeField] private Vector3 focusCameraOffset = new Vector3(0, -0.18f, 0.22f);
        [SerializeField, Range(30, 75)] private float focusFieldOfView = 52;
        [SerializeField, Range(0.05f, 0.5f)] private float focusTransitionSeconds = 0.18f;
        private PrototypeGameConfig config;
        private InputActionAsset input;
        private CharacterController controller;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 cameraHome;
        private Vector3 cameraFocus;
        private float cameraHomeFieldOfView;
        private float focusBlend;
        private float initialPitch;
        private float pitch;
        private float verticalSpeed;
        private float sensitivity;
        private bool initialized;
        private float focusYaw, focusPitch, focusBaseYaw;
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
            cameraHomeFieldOfView = viewCamera.fieldOfView;
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
            Focused = false;
            focusBlend = 0;
            ApplyCameraPose();
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
                var move = map.FindAction("Move", true).ReadValue<Vector2>();
                var speed = map.FindAction("Sprint", true).IsPressed() ? config.RunSpeed : config.WalkSpeed;
                var direction = Vector3.ClampMagnitude(transform.right * move.x + transform.forward * move.y, 1);
                if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
                verticalSpeed += Physics.gravity.y * clock.Delta;
                controller.Move((direction * speed + Vector3.up * verticalSpeed) * clock.Delta);
            }
            else
            {
                focusYaw = Mathf.Clamp(focusYaw + look.x, focusBaseYaw - 55, focusBaseYaw + 55);
                focusPitch = Mathf.Clamp(focusPitch - look.y, 5, 78);
            }
            focusBlend = Mathf.MoveTowards(focusBlend, Focused ? 1 : 0, clock.Delta / Mathf.Max(0.05f, focusTransitionSeconds));
            ApplyCameraPose();
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
            cameraFocus = cameraHome + focusCameraOffset;
            var point = Target.FocusPoint != null ? Target.FocusPoint.position
                : Target.GetComponent<Collider>() is Collider surface
                    ? new Vector3(surface.bounds.center.x, surface.bounds.max.y + 0.12f, surface.bounds.center.z)
                    : Target.transform.position + Vector3.up * 0.3f;
            var pose = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(point - transform.TransformPoint(SafeFocusPosition()));
            focusPitch = Mathf.Clamp(Mathf.DeltaAngle(0, pose.eulerAngles.x), 5, 78);
            focusYaw = focusBaseYaw = Mathf.DeltaAngle(0, pose.eulerAngles.y);
        }

        public void ExitFocus()
        {
            Focused = false;
        }

        private void ApplyCameraPose()
        {
            if (viewCamera == null) return;
            float blend = Mathf.SmoothStep(0, 1, focusBlend);
            viewCamera.transform.localPosition = Vector3.Lerp(cameraHome, focusBlend > 0 ? SafeFocusPosition() : cameraHome, blend);
            viewCamera.fieldOfView = Mathf.Lerp(cameraHomeFieldOfView, Mathf.Min(cameraHomeFieldOfView, focusFieldOfView), blend);
            viewCamera.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(pitch, 0, 0), Quaternion.Euler(focusPitch, focusYaw, 0), blend);
        }

        private Vector3 SafeFocusPosition()
        {
            var origin = transform.TransformPoint(cameraHome);
            var delta = transform.TransformVector(cameraFocus - cameraHome);
            float distance = delta.magnitude;
            if (distance < 0.001f) return cameraHome;
            float allowed = distance;
            foreach (var hit in Physics.SphereCastAll(origin, viewCamera.nearClipPlane + 0.02f,
                delta / distance, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - 0.02f));
            }
            return Vector3.Lerp(cameraHome, cameraFocus, allowed / distance);
        }
    }
}
