using UnityEngine;

namespace ChefShow.Ingredients
{
    public enum KitchenToolKind { None, Knife, Fork, Spoon, Spatula }

    /// <summary>Сохранённый ящик станции; содержимое доступно для ручного осмотра.</summary>
    public sealed class ToolDrawer : MonoBehaviour
    {
        public string StationId;
        public Transform Tray;
        public Vector3 ClosedPosition;
        public Vector3 OpenOffset;
        public Transform[] Tools;
        public Transform[] Compartments;
        [HideInInspector] public int LayoutVersion;
        [Tooltip("Время выдвижения/закрытия; движение использует игровое время.")]
        [Min(.05f)] public float SlideSeconds = .3f;
        [Tooltip("Предпросмотр открытого ящика в Edit Mode.")]
        public bool PreviewOpen;
        private float slide;
        private bool targetOpen;
        public bool IsOpen => targetOpen;
        public bool CanTakeTools => targetOpen && slide >= .999f;

        public void ShowOpen(bool open)
        {
            targetOpen = open; slide = open ? 1 : 0; ApplyPose();
        }

        public void AnimateOpen(bool open) => targetOpen = open;

        public void Step(float delta)
        {
            slide = Mathf.MoveTowards(slide, targetOpen ? 1 : 0, delta / Mathf.Max(.05f, SlideSeconds));
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (Tray != null) Tray.localPosition = ClosedPosition + OpenOffset * Mathf.SmoothStep(0, 1, slide);
            // Trigger остаётся целью прицела, но не толкает капсулу игрока.
            var frontCollider = GetComponent<Collider>();
            if (frontCollider != null) { frontCollider.isTrigger = true; frontCollider.enabled = true; }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) ShowOpen(PreviewOpen);
        }
    }
}
