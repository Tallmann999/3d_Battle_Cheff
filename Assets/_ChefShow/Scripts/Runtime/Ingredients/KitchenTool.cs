using UnityEngine;

namespace ChefShow.Ingredients
{
    public enum KitchenToolPlacement { Stored, Held, Dropped }

    /// <summary>Один физический инструмент: ячейка, правая рука или мир.</summary>
    public sealed class KitchenTool : MonoBehaviour
    {
        public KitchenToolKind Kind;
        public ToolDrawer Drawer;
        public Rigidbody Body;
        public BoxCollider PickupCollider;
        public KitchenToolPlacement Placement { get; private set; }
        private Transform home;
        private Vector3 homePosition, homeScale;
        private Quaternion homeRotation;
        private Vector3 pausedVelocity, pausedAngularVelocity;
        private bool frozen;

        public void Initialize()
        {
            home = transform.parent; homePosition = transform.localPosition;
            homeRotation = transform.localRotation; homeScale = transform.localScale;
        }

        public void ReturnHome()
        {
            StopPhysics();
            transform.SetParent(home, false);
            transform.localPosition = homePosition; transform.localRotation = homeRotation; transform.localScale = homeScale;
            Placement = KitchenToolPlacement.Stored;
            PickupCollider.enabled = true; PickupCollider.isTrigger = true;
        }

        public void Equip(Transform hand)
        {
            StopPhysics(); PickupCollider.enabled = false;
            transform.SetParent(hand, false);
            transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one;
            Placement = KitchenToolPlacement.Held;
        }

        public void Drop(Transform world, Transform camera)
        {
            transform.SetParent(world, true);
            transform.SetPositionAndRotation(camera.position + camera.forward * .65f + camera.right * .24f, camera.rotation);
            PickupCollider.enabled = true; PickupCollider.isTrigger = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate; Body.isKinematic = false; Body.linearVelocity = camera.forward * .7f; Body.angularVelocity = Vector3.zero;
            Placement = KitchenToolPlacement.Dropped; frozen = false;
            Physics.SyncTransforms();
        }

        public void FreezePhysics(bool active)
        {
            if (Placement != KitchenToolPlacement.Dropped) return;
            if (!active && !frozen)
            {
                pausedVelocity = Body.linearVelocity; pausedAngularVelocity = Body.angularVelocity;
                Body.isKinematic = true; frozen = true;
            }
            else if (active && frozen)
            {
                Body.isKinematic = false; Body.linearVelocity = pausedVelocity; Body.angularVelocity = pausedAngularVelocity;
                frozen = false;
            }
        }

        private void StopPhysics()
        {
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            Body.isKinematic = true; Body.interpolation = RigidbodyInterpolation.None; frozen = false;
        }
    }
}
