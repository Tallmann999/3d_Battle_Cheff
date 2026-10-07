using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChefShow.Ingredients
{
    public sealed class PreparationController : MonoBehaviour
    {
        public ChoppingBoard[] Boards;
        [Range(.08f, .5f)] public float KnifeStrokeSeconds = .18f;
        private GameBootstrap bootstrap;
        private InputActionAsset input;
        private ChoppingBoard own;
        private KitchenTool strokeTool;
        private KitchenTool observedTool;
        private int observedVersion = -1;
        private float strokeTime = -1;
        private Vector3 strokeStart, contact;
        private Quaternion strokeRotation, contactRotation;
        private string message;
        private InventoryInteractable messageTarget;
        private float messageUntil;
        public string Validate(InventoryController inventory)
        {
            if (Boards == null || Boards.Length != 12 || Boards.Any(b => b == null || b.Target == null || b.Caption == null
                || b.KnifeContactPoint == null || b.Target.Kind != InventoryTargetKind.Socket || b.Target.Index != 0)
                || Boards.Select(b => b.StationId).Distinct().Count() != 12
                || Boards.Count(b => b.StationId == inventory.PlayerStationId) != 1)
                return "Нужны 12 досок со ссылками на цель, подпись и точку ножа.";
            if (KnifeStrokeSeconds <= 0 || float.IsNaN(KnifeStrokeSeconds) || float.IsInfinity(KnifeStrokeSeconds))
                return "Длительность движения ножа должна быть конечной и положительной.";
            var views = inventory.BasketDisplays.Concat(inventory.TrayDisplays).Concat(inventory.SocketDisplays).Append(inventory.HeldDisplay);
            if (views.Any(v => v.CutPieces == null || v.CutPieces.Length != InventoryState.RequiredChopPresses + 1
                || v.CutPieces.Any(r => r == null))) return "Не сохранены семь визуальных частей каждой переносимой порции.";
            return null;
        }
        public void Initialize(GameBootstrap owner, InputActionAsset actions)
        {
            bootstrap = owner; input = actions; own = Boards.Single(b => b.StationId == owner.Inventory.PlayerStationId);
        }
        public void ResetPresentation()
        {
            StopStroke(); message = null; messageTarget = null;
            foreach (var board in Boards) board.Present(null);
        }
        public void Step(bool acceptInput, bool commandConsumed)
        {
            if (observedTool != bootstrap.Tools.EquippedObject || observedVersion != bootstrap.Run.Inventory.Version)
            {
                message = null; messageTarget = null; observedTool = bootstrap.Tools.EquippedObject; observedVersion = bootstrap.Run.Inventory.Version;
            }
            if (strokeTool != null && (strokeTool != bootstrap.Tools.EquippedObject || strokeTool.Placement != KitchenToolPlacement.Held))
            { strokeTool = null; strokeTime = -1; }
            if (acceptInput && !commandConsumed)
            {
                var map = input.FindActionMap(bootstrap.Player.Focused ? "Station" : "Gameplay", true);
                if (map.FindAction("Primary", true).WasPressedThisFrame())
                {
                    bootstrap.Player.RefreshTarget();
                    var board = bootstrap.Player.Target == null ? null : bootstrap.Player.Target.GetComponent<ChoppingBoard>();
                    if (board != null)
                    {
                        string reason = "Это доска другого участника.";
                        bool success = board == own && bootstrap.Run.Inventory.TryChop(bootstrap.Tools.Equipped, out reason);
                        if (success) { message = null; StartStroke(board); }
                        else { message = reason; messageTarget = null; messageUntil = bootstrap.Run.Clock.SimulationTime + 2.5f; }
                    }
                    else
                    {
                        var item = bootstrap.Player.Target == null ? null : bootstrap.Player.Target.GetComponent<InventoryInteractable>();
                        if (item != null && item.Kind == InventoryTargetKind.TrayItem)
                        {
                            string reason = "Это лоток другого участника.";
                            var state = bootstrap.Run.Inventory;
                            var package = item.Index >= 0 && item.Index < state.Tray.Count ? state.Tray[item.Index] : null;
                            if (package != null && package.PackedIngredient != null)
                            {
                                bool success = item.StationId == bootstrap.Inventory.PlayerStationId && state.TryUnpack(item.Index, out reason);
                                message = success ? "Распаковано: " + PackageContentsName(package) : reason; messageTarget = item;
                                messageUntil = bootstrap.Run.Clock.SimulationTime + 2.5f;
                                observedVersion = bootstrap.Run.Inventory.Version;
                            }
                        }
                    }
                }
            }
            if (acceptInput) AnimateStroke(bootstrap.Run.Clock.Delta);
            own.Present(bootstrap.Run.Inventory.Socket(0));
        }
        private void StartStroke(ChoppingBoard board)
        {
            strokeTool = bootstrap.Tools.EquippedObject; strokeTime = 0;
            strokeStart = strokeTool.transform.position; strokeRotation = strokeTool.transform.rotation;
            var forward = Vector3.ProjectOnPlane(bootstrap.Player.ViewCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = bootstrap.Player.transform.forward;
            contactRotation = Quaternion.FromToRotation(Vector3.right, forward) * Quaternion.Euler(90, 0, 0);
            contact = board.KnifeContactPoint.position - contactRotation * new Vector3(.09f, .015f, 0);
        }
        private void AnimateStroke(float delta)
        {
            if (strokeTool == null || strokeTime < 0) return;
            strokeTime += delta;
            float t = Mathf.Clamp01(strokeTime / KnifeStrokeSeconds);
            var hand = bootstrap.Tools.RightHand;
            float phase = Mathf.SmoothStep(0, 1, t <= .5f ? t * 2 : (t - .5f) * 2);
            strokeTool.transform.SetPositionAndRotation(t <= .5f ? Vector3.Lerp(strokeStart, contact, phase) : Vector3.Lerp(contact, hand.position, phase),
                t <= .5f ? Quaternion.Slerp(strokeRotation, contactRotation, phase) : Quaternion.Slerp(contactRotation, hand.rotation, phase));
            if (t >= 1) StopStroke();
        }
        private void StopStroke()
        {
            if (strokeTool != null && strokeTool.Placement == KitchenToolPlacement.Held)
            { strokeTool.transform.localPosition = Vector3.zero; strokeTool.transform.localRotation = Quaternion.identity; }
            strokeTool = null; strokeTime = -1;
        }
        public string Describe(PrototypeInteractable target)
        {
            var board = target == null ? null : target.GetComponent<ChoppingBoard>();
            if (board == null)
            {
                var item = target == null ? null : target.GetComponent<InventoryInteractable>();
                if (item == null || item.Kind != InventoryTargetKind.TrayItem) return null;
                if (item.StationId != bootstrap.Inventory.PlayerStationId) return "Лоток другого участника";
                if (message != null && item == messageTarget && bootstrap.Run.Clock.SimulationTime < messageUntil)
                    return bootstrap.Inventory.Describe(target) + "\n" + message;
                var trayState = bootstrap.Run.Inventory;
                var package = item.Index >= 0 && item.Index < trayState.Tray.Count ? trayState.Tray[item.Index] : null;
                if (bootstrap.Run.Inventory.Held != null || package == null || package.PackedIngredient == null) return null;
                return bootstrap.Inventory.Describe(target) + "\nЛКМ — открыть · " + PackageContentsName(package);
            }
            if (board != own) return "Доска другого участника";
            if (message != null && bootstrap.Run.Clock.SimulationTime < messageUntil) return message;
            var state = bootstrap.Run.Inventory; var food = state.Socket(0);
            if (state.Held != null || food == null) return null;
            string take = bootstrap.Inventory.Describe(target);
            return take + "\n" + (food.Preparation == PreparationState.Chopped ? "Нарезано · можно перенести"
                : bootstrap.Tools.Equipped == KitchenToolKind.Knife ? "ЛКМ — нарезать · " + food.ChopPresses + "/" + InventoryState.RequiredChopPresses
                : "Для нарезки возьмите нож: E");
        }
        private static string PackageContentsName(FoodPortion package)
        {
            string name = package.PackedIngredient.Id == "potato" ? "картофелин"
                : package.PackedIngredient.Id == "egg" ? "яиц" : package.PackedIngredient.DisplayName;
            return package.PackedQuantity + " " + name;
        }
    }
}
