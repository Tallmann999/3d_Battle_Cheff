using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChefShow.Ingredients
{
    public readonly struct KitchenToolChanged
    {
        public readonly string RunId, RoundId, ActorId;
        public readonly KitchenToolKind Previous, Current;
        public readonly float SimulationTime;
        public KitchenToolChanged(PrototypeRun run, KitchenToolKind previous, KitchenToolKind current)
        {
            RunId = run.RunId; RoundId = "prototype"; ActorId = run.PlayerTeam + "1";
            SimulationTime = run.Clock.SimulationTime; Previous = previous; Current = current;
        }
    }

    public sealed class ToolDrawerController : MonoBehaviour
    {
        public ToolDrawer[] Drawers;
        public Transform RightHand, LeftFoodHand;
        private GameBootstrap bootstrap;
        private InputActionAsset input;
        private KitchenTool[] tools;
        private KitchenTool equipped;
        private string message;
        private float messageUntil;
        public KitchenTool EquippedObject => equipped;
        public KitchenToolKind Equipped => equipped == null ? KitchenToolKind.None : equipped.Kind;
        public bool FoodInLeftHand => true;

        public string Validate()
        {
            if (Drawers == null || Drawers.Length != 12 || RightHand == null || LeftFoodHand == null)
                return "Не заполнены ссылки ящиков и двух рук.";
            foreach (var drawer in Drawers)
            {
                if (drawer == null || drawer.Tray == null || drawer.Tools == null || drawer.Tools.Length != 4
                    || drawer.Compartments == null || drawer.Compartments.Length != 4 || string.IsNullOrEmpty(drawer.StationId))
                    return "Не настроен ящик станции с четырьмя ячейками.";
                for (int i = 0; i < 4; i++)
                {
                    var tool = drawer.Tools[i] == null ? null : drawer.Tools[i].GetComponent<KitchenTool>();
                    if (tool == null || tool.Drawer != drawer || tool.Kind != (KitchenToolKind)(i + 1)
                        || tool.Body == null || tool.PickupCollider == null || tool.GetComponent<PrototypeInteractable>() == null)
                        return "Не настроены физические инструменты ящика " + drawer.StationId;
                    var bottom = drawer.Compartments[i] == null ? null : drawer.Compartments[i].Find("Cell Bottom");
                    var cell = bottom == null ? null : bottom.GetComponent<ToolDrawerTarget>();
                    var collider = bottom == null ? null : bottom.GetComponent<BoxCollider>();
                    if (cell == null || cell.Drawer != drawer || cell.CompartmentIndex != i || collider == null
                        || !collider.isTrigger || bottom.GetComponent<PrototypeInteractable>() == null)
                        return "Не настроена ячейка возврата " + drawer.StationId + "/" + i;
                }
            }
            return null;
        }

        public void Initialize(GameBootstrap owner, InputActionAsset actions)
        {
            bootstrap = owner; input = actions;
            tools = new KitchenTool[Drawers.Length * 4];
            int index = 0;
            foreach (var drawer in Drawers)
                foreach (var root in drawer.Tools)
                {
                    var tool = root.GetComponent<KitchenTool>(); tool.Initialize(); tools[index++] = tool;
                    Physics.IgnoreCollision(tool.PickupCollider, bootstrap.Player.GetComponent<CharacterController>());
                }
        }

        public void ResetPresentation()
        {
            equipped = null; message = null;
            foreach (var tool in tools) tool.ReturnHome();
            foreach (var drawer in Drawers) drawer.ShowOpen(false);
            bootstrap.Inventory.PresentFoodHand(true, LeftFoodHand);
            Physics.SyncTransforms();
        }

        // Возвращает true, если команда E/G уже обработана и не должна переносить еду/корзину.
        public bool Step(bool acceptInput)
        {
            foreach (var tool in tools) tool.FreezePhysics(acceptInput);
            foreach (var drawer in Drawers)
            {
                // At timeout game time has stopped; finish the drawer's cleanup immediately.
                if (bootstrap.Run.RemainingSeconds <= 0) drawer.ShowOpen(false);
                else drawer.Step(bootstrap.Run.Clock.Delta);
            }
            bool consumed = false;
            if (acceptInput)
            {
                var map = input.FindActionMap(bootstrap.Player.Focused ? "Station" : "Gameplay", true);
                if (map.FindAction("DropBasket", true).WasPressedThisFrame() && equipped != null)
                {
                    var previous = Equipped;
                    equipped.Drop(bootstrap.Inventory.WorldRoot, bootstrap.Player.ViewCamera.transform);
                    equipped = null; Publish(previous); consumed = true;
                }
                else if (map.FindAction("Primary", true).WasPressedThisFrame() || map.FindAction("Secondary", true).WasPressedThisFrame())
                {
                    bootstrap.Player.RefreshTarget();
                    var target = bootstrap.Player.Target;
                    bool left = map.FindAction("Primary", true).WasPressedThisFrame();
                    if (!left && target!=null && target.GetComponent<ToolDrawerTarget>()!=null && ReturnAimedTool()) return true;
                    var drawer = DrawerTarget(target);
                    var tool = target == null ? null : target.GetComponent<KitchenTool>();
                    if (left && drawer != null)
                    {
                        consumed = true;
                        if (Own(drawer)) drawer.AnimateOpen(!drawer.IsOpen);
                        else Notify("Это ящик другого участника.");
                    }
                    else if (!left && tool != null)
                    {
                        consumed = true;
                        if (!Own(tool.Drawer)) Notify("Это инструмент другого участника.");
                        else if (tool.Placement == KitchenToolPlacement.Stored && !tool.Drawer.CanTakeTools)
                            Notify("Сначала откройте ящик.");
                        else if (bootstrap.Run.Inventory.Held != null && bootstrap.Run.Inventory.Placement == BasketPlacement.Carried)
                            Notify("Левую руку занимает корзина. Сначала поставьте её Tab.");
                        else
                        {
                            var previous = Equipped;
                            if (equipped != null) equipped.ReturnHome();
                            equipped = tool; tool.Equip(RightHand); Publish(previous); message = null;
                        }
                    }
                }
            }
            bootstrap.Inventory.PresentFoodHand(FoodInLeftHand, LeftFoodHand);
            return consumed;
        }

        // Called before food cancellation/focus exit so a single RMB has one owner.
        public bool ReturnAimedTool()
        {
            if (equipped == null) return false;
            bootstrap.Player.RefreshTarget();
            var target = bootstrap.Player.Target;
            var cell = target == null ? null : target.GetComponent<ToolDrawerTarget>();
            var stored = target == null ? null : target.GetComponent<KitchenTool>();
            int index = cell != null ? cell.CompartmentIndex
                : stored != null && stored.Placement == KitchenToolPlacement.Stored ? (int)stored.Kind - 1 : -1;
            if (index < 0 || index > 3) return false;
            var drawer = cell != null ? cell.Drawer : stored.Drawer;
            if (!Own(drawer)) Notify("Это ячейка другого участника.");
            else if (!drawer.CanTakeTools) Notify("Сначала полностью откройте ящик.");
            else if (equipped.Drawer != drawer || (int)equipped.Kind - 1 != index)
                Notify("Наведите на свою ячейку: " + Name(Equipped) + ".");
            else
            {
                var previous = Equipped;
                equipped.ReturnHome(); equipped = null; message = null;
                bootstrap.Inventory.PresentFoodHand(true, LeftFoodHand);
                Publish(previous);
            }
            return true;
        }

        private static ToolDrawer DrawerTarget(PrototypeInteractable target)
        {
            if (target == null) return null;
            var panel = target.GetComponent<ToolDrawer>();
            return panel != null ? panel : target.GetComponent<ToolDrawerTarget>()?.Drawer;
        }

        private bool Own(ToolDrawer drawer) => drawer.StationId == bootstrap.Inventory.PlayerStationId;
        private void Publish(KitchenToolKind previous)
        {
            bootstrap.Run.Events.Publish(new KitchenToolChanged(bootstrap.Run, previous, Equipped));
        }
        private void Notify(string text) { message = text; messageUntil = bootstrap.Run.Clock.SimulationTime + 2.5f; }

        public string Describe(PrototypeInteractable target)
        {
            if (message != null && bootstrap.Run.Clock.SimulationTime < messageUntil) return message;
            var cell = target == null ? null : target.GetComponent<ToolDrawerTarget>();
            if (cell != null && cell.CompartmentIndex >= 0 && equipped != null)
            {
                if (!Own(cell.Drawer)) return "Ячейка другого участника";
                if (!cell.Drawer.CanTakeTools) return "Сначала откройте ящик";
                return cell.Drawer == equipped.Drawer && cell.CompartmentIndex == (int)equipped.Kind - 1
                    ? "ПКМ — Положить " + Accusative(Equipped) + " · ЛКМ — закрыть ящик"
                    : "Ячейка: " + Name((KitchenToolKind)(cell.CompartmentIndex + 1));
            }
            var drawer = DrawerTarget(target);
            if (drawer != null) return Own(drawer) ? "ЛКМ — " + (drawer.IsOpen ? "Закрыть ящик" : "Открыть ящик") : "Ящик другого участника";
            var tool = target == null ? null : target.GetComponent<KitchenTool>();
            if (tool == null) return null;
            if (!Own(tool.Drawer)) return "Инструмент другого участника";
            if (tool.Placement == KitchenToolPlacement.Stored && !tool.Drawer.CanTakeTools) return "Сначала откройте ящик";
            if (bootstrap.Run.Inventory.Held != null && bootstrap.Run.Inventory.Placement == BasketPlacement.Carried)
                return "Левую руку занимает корзина. Поставьте её Tab.";
            return "ПКМ — Взять " + Accusative(tool.Kind);
        }

        public string Summary => Equipped == KitchenToolKind.None ? "Правая рука свободна · ПКМ — взять инструмент"
            : "Правая рука: " + Name(Equipped) + " · ПКМ по своей ячейке — положить · G — уронить";
        public static string Name(KitchenToolKind kind) => kind == KitchenToolKind.Knife ? "Нож"
            : kind == KitchenToolKind.Fork ? "Вилка" : kind == KitchenToolKind.Spoon ? "Ложка"
            : kind == KitchenToolKind.Spatula ? "Деревянная лопатка" : "Нет";
        public static string Accusative(KitchenToolKind kind) => kind == KitchenToolKind.Knife ? "нож"
            : kind == KitchenToolKind.Fork ? "вилку" : kind == KitchenToolKind.Spoon ? "ложку" : "деревянную лопатку";
    }
}
