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
        public bool FoodInLeftHand => equipped != null;

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
            bootstrap.Inventory.PresentFoodHand(false, LeftFoodHand);
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
                else if (map.FindAction("Interact", true).WasPressedThisFrame())
                {
                    bootstrap.Player.RefreshTarget();
                    var target = bootstrap.Player.Target;
                    var drawer = DrawerTarget(target);
                    var tool = target == null ? null : target.GetComponent<KitchenTool>();
                    if (drawer != null)
                    {
                        consumed = true;
                        if (Own(drawer)) drawer.AnimateOpen(!drawer.IsOpen);
                        else Notify("Это ящик другого участника.");
                    }
                    else if (tool != null)
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
            var drawer = DrawerTarget(target);
            if (drawer != null) return Own(drawer) ? "E — " + (drawer.IsOpen ? "Закрыть ящик" : "Открыть ящик") : "Ящик другого участника";
            var tool = target == null ? null : target.GetComponent<KitchenTool>();
            if (tool == null) return null;
            if (!Own(tool.Drawer)) return "Инструмент другого участника";
            if (tool.Placement == KitchenToolPlacement.Stored && !tool.Drawer.CanTakeTools) return "Сначала откройте ящик";
            if (bootstrap.Run.Inventory.Held != null && bootstrap.Run.Inventory.Placement == BasketPlacement.Carried)
                return "Левую руку занимает корзина. Поставьте её Tab.";
            return "E — Взять " + Accusative(tool.Kind);
        }

        public string Summary => Equipped == KitchenToolKind.None ? "Правая рука: продукт / свободна"
            : "Правая рука: " + Name(Equipped) + " · G — уронить";
        public static string Name(KitchenToolKind kind) => kind == KitchenToolKind.Knife ? "Нож"
            : kind == KitchenToolKind.Fork ? "Вилка" : kind == KitchenToolKind.Spoon ? "Ложка"
            : kind == KitchenToolKind.Spatula ? "Деревянная лопатка" : "Нет";
        public static string Accusative(KitchenToolKind kind) => kind == KitchenToolKind.Knife ? "нож"
            : kind == KitchenToolKind.Fork ? "вилку" : kind == KitchenToolKind.Spoon ? "ложку" : "деревянную лопатку";
    }
}
