using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChefShow.Inventory
{
    public sealed class InventoryController : MonoBehaviour
    {
        public IngredientCatalog Catalog;
        public Rigidbody BasketBody;
        public BoxCollider BasketCollider;
        public Transform BasketMount, PantryRest, StationDock, WorldRoot;
        public FoodDisplay[] BasketDisplays, TrayDisplays, SocketDisplays, Flights;
        public FoodDisplay HeldDisplay;
        public string PlayerStationId;
        [Range(0.3f, 1)] public float CarriedBasketScale = 0.65f;
        private GameBootstrap bootstrap;
        private InputActionAsset input;
        private int version = -1, flightIndex;
        private Vector3 pausedVelocity, pausedAngularVelocity;
        private bool floorPaused;
        private Vector3[] flightStarts;
        private float[] flightElapsed;
        private string message;
        private float messageUntil;
        private Transform foodRightParent;
        private Vector3 foodRightPosition;
        private Quaternion foodRightRotation;
        public bool FoodInLeftHand { get; private set; }
        public InventoryState State => bootstrap.Run.Inventory;

        public string Validate()
        {
            if (Catalog == null || Catalog.Validate() != null) return Catalog == null ? "Нет каталога продуктов." : Catalog.Validate();
            if (BasketBody == null || BasketCollider == null || BasketMount == null || PantryRest == null || StationDock == null
                || WorldRoot == null || HeldDisplay == null || string.IsNullOrWhiteSpace(PlayerStationId)
                || BasketDisplays == null || BasketDisplays.Length != 10 || TrayDisplays == null || TrayDisplays.Length != 24
                || SocketDisplays == null || SocketDisplays.Length != 2 || Flights == null || Flights.Length != 4)
                return "Не заполнены ссылки корзины, лотка и переносимых визуалов.";
            foreach (var group in new[] { BasketDisplays, TrayDisplays, SocketDisplays, Flights })
                foreach (var view in group) if (view == null || view.Mesh == null || view.Visual == null) return "Не настроен визуал продукта.";
            for (int i = 0; i < TrayDisplays.Length; i++)
            {
                var target = TrayDisplays[i].GetComponentInParent<InventoryInteractable>();
                if (target == null || target.Kind != InventoryTargetKind.TrayItem || target.Index != i || target.GetComponent<BoxCollider>() == null)
                    return "Не настроены цели выбора продуктов лотка.";
            }
            return null;
        }

        public void Initialize(GameBootstrap owner, InputActionAsset actions)
        {
            bootstrap = owner; input = actions;
            foodRightParent = HeldDisplay.transform.parent; foodRightPosition = HeldDisplay.transform.localPosition;
            foodRightRotation = HeldDisplay.transform.localRotation;
            flightStarts = new Vector3[Flights.Length]; flightElapsed = new float[Flights.Length];
            for (int i = 0; i < flightElapsed.Length; i++) flightElapsed[i] = -1;
        }
        public void ResetPresentation()
        {
            BasketBody.isKinematic = true;
            BasketBody.transform.SetParent(WorldRoot, true);
            BasketBody.transform.localScale = Vector3.one;
            BasketBody.transform.SetPositionAndRotation(PantryRest.position, PantryRest.rotation);
            BasketCollider.enabled = true; floorPaused = false; version = -1; message = null;
            Physics.SyncTransforms();
            for (int i = 0; i < Flights.Length; i++) { flightElapsed[i] = -1; Flights[i].Present(null); }
            Sync();
        }

        public void Step(bool acceptInput, bool commandConsumed = false)
        {
            UpdateFloorPhysics(acceptInput);
            AnimateFlights(bootstrap.Run.Clock.Delta);
            if (acceptInput && !commandConsumed)
            {
                var map = input.FindActionMap(bootstrap.Player.Focused ? "Station" : "Gameplay", true);
                if (map.FindAction("Basket", true).WasPressedThisFrame()) HandleBasket();
                else if (map.FindAction("DropBasket", true).WasPressedThisFrame()) DropBasket();
                else if (map.FindAction("Interact", true).WasPressedThisFrame()) Act();
            }
            Sync();
        }

        public bool CancelHeld()
        {
            if (State.Held == null) return false;
            if (!State.TryCancelHeld(out string reason)) Notify(reason);
            Sync(); return true;
        }
        private InventoryInteractable Target()
        {
            // Повторный raycast при команде: старой подсказке не доверяем.
            bootstrap.Player.RefreshTarget();
            return bootstrap.Player.Target == null ? null : bootstrap.Player.Target.GetComponent<InventoryInteractable>();
        }
        private bool Own(InventoryInteractable target) => string.IsNullOrEmpty(target.StationId) || target.StationId == PlayerStationId;

        private void HandleBasket()
        {
            var target = Target();
            string reason;
            if (State.Placement != BasketPlacement.Carried && bootstrap.Tools != null && bootstrap.Tools.FoodInLeftHand && State.Held != null)
            { Notify("Левую руку занимает продукт. Сначала положите его."); return; }
            if (State.Placement != BasketPlacement.Carried)
            {
                if (target == null || target.Kind != InventoryTargetKind.Basket) { Notify("Посмотрите на корзину и нажмите Tab."); return; }
                if (!State.TryMoveBasket(BasketPlacement.Carried, out reason)) { Notify(reason); return; }
                BasketBody.isKinematic = true; BasketCollider.enabled = false;
                BasketBody.transform.SetParent(BasketMount, false);
                BasketBody.transform.localPosition = Vector3.zero; BasketBody.transform.localRotation = Quaternion.identity;
                BasketBody.transform.localScale = Vector3.one * CarriedBasketScale;
                bootstrap.Player.ExitFocus();
            }
            else
            {
                bool station = target != null && !string.IsNullOrEmpty(target.StationId) && Own(target);
                station |= bootstrap.Player.Target != null && bootstrap.Player.Target.CanFocus(bootstrap.Config.PlayerTeam);
                bool pantry = target != null && target.Kind == InventoryTargetKind.PantryRest;
                if (!station && !pantry) { Notify("Посмотрите на свой стол или место корзины у кладовой. G — уронить."); return; }
                if (!State.TryMoveBasket(station ? BasketPlacement.Station : BasketPlacement.Pantry, out reason)) { Notify(reason); return; }
                BasketBody.isKinematic = true;
                BasketBody.transform.SetParent(WorldRoot, true);
                BasketBody.transform.localScale = Vector3.one;
                var mount = station ? StationDock : PantryRest;
                BasketBody.transform.SetPositionAndRotation(mount.position, mount.rotation);
                BasketCollider.enabled = true;
            }
            Physics.SyncTransforms();
        }
        private void DropBasket()
        {
            if (!State.TryMoveBasket(BasketPlacement.Floor, out string reason)) { Notify(reason); return; }
            var camera = bootstrap.Player.ViewCamera.transform;
            BasketBody.transform.SetParent(WorldRoot, true);
            BasketBody.transform.localScale = Vector3.one;
            BasketBody.transform.SetPositionAndRotation(camera.position + camera.forward * 0.65f - camera.right * 0.38f,
                Quaternion.Euler(0, bootstrap.Player.transform.eulerAngles.y, 0));
            BasketCollider.enabled = true; BasketBody.isKinematic = false;
            BasketBody.linearVelocity = camera.forward * 0.4f; BasketBody.angularVelocity = Vector3.zero;
            floorPaused = false;
            Physics.SyncTransforms();
        }
        private void Act()
        {
            var target = Target();
            if (target == null) return;
            if (!Own(target)) { Notify("Это станция другого участника."); return; }
            bool takingFood = State.Held == null && (target.Kind == InventoryTargetKind.TrayItem
                || target.Kind == InventoryTargetKind.Socket);
            if (takingFood && bootstrap.Tools != null && bootstrap.Tools.FoodInLeftHand && State.Placement == BasketPlacement.Carried)
            { Notify("Левую руку занимает корзина. Сначала поставьте её Tab."); return; }
            bool success = false; string reason = null;
            switch (target.Kind)
            {
                case InventoryTargetKind.Pickup:
                    if (State.Held != null) success = State.TryRemove(true, out reason);
                    else
                    {
                        success = State.TryCollect(target.Ingredient, out reason);
                        if (success) Fly(target.Ingredient, target.transform.position);
                    }
                    break;
                case InventoryTargetKind.Basket:
                    success = State.TryUnload(out reason);
                    break;
                case InventoryTargetKind.TrayItem:
                    success = State.Held == null ? State.TryTakeTray(target.Index, out reason) : State.TryPutInTray(out reason); break;
                case InventoryTargetKind.Tray:
                    success = State.TryPutInTray(out reason); break;
                case InventoryTargetKind.Socket:
                    success = State.Held == null ? State.TryTakeSocket(target.Index, out reason) : State.TryPlaceSocket(target.Index, out reason); break;
                case InventoryTargetKind.Trash: success = State.TryRemove(false, out reason); break;
                case InventoryTargetKind.PantryReturn: success = State.TryRemove(true, out reason); break;
            }
            if (!success && reason != null) Notify(reason);
            else if (success) message = null;
        }
        private void Notify(string text) { message = text; messageUntil = bootstrap.Run.Clock.SimulationTime + 2.5f; }

        public void PresentFoodHand(bool left, Transform leftHand)
        {
            FoodInLeftHand = left;
            var parent = left ? leftHand : foodRightParent;
            if (HeldDisplay.transform.parent == parent) return;
            HeldDisplay.transform.SetParent(parent, false);
            HeldDisplay.transform.localPosition = left ? Vector3.zero : foodRightPosition;
            HeldDisplay.transform.localRotation = left ? Quaternion.identity : foodRightRotation;
        }

        private void Sync()
        {
            if (version == State.Version) return;
            version = State.Version;
            for (int i = 0; i < BasketDisplays.Length; i++) Show(BasketDisplays[i], i < State.Basket.Count ? State.Basket[i] : null);
            for (int i = 0; i < TrayDisplays.Length; i++)
            {
                bool occupied = i < State.Tray.Count;
                Show(TrayDisplays[i], occupied ? State.Tray[i] : null);
                // Пустое место не должно перекрывать видимый продукт за ним.
                TrayDisplays[i].GetComponentInParent<InventoryInteractable>().GetComponent<BoxCollider>().enabled = occupied;
            }
            for (int i = 0; i < SocketDisplays.Length; i++) Show(SocketDisplays[i], State.Socket(i));
            Show(HeldDisplay, State.Held);
        }
        private static void Show(FoodDisplay view, FoodPortion portion) => view.PresentPortion(portion);
        private void Fly(IngredientDefinition ingredient, Vector3 from)
        {
            int index = flightIndex++ % Flights.Length;
            flightStarts[index] = from; flightElapsed[index] = 0;
            Flights[index].Present(ingredient); Flights[index].transform.position = from;
        }
        private void AnimateFlights(float delta)
        {
            for (int i = 0; i < Flights.Length; i++)
            {
                if (flightElapsed[i] < 0) continue;
                flightElapsed[i] += delta; float t = Mathf.Clamp01(flightElapsed[i] / 0.2f);
                Flights[i].transform.position = Vector3.Lerp(flightStarts[i], BasketBody.transform.position + Vector3.up * 0.1f, t)
                    + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.25f);
                if (t >= 1) { Flights[i].Present(null); flightElapsed[i] = -1; }
            }
        }
        private void UpdateFloorPhysics(bool active)
        {
            if (State.Placement != BasketPlacement.Floor) return;
            if (!active && !floorPaused)
            {
                pausedVelocity = BasketBody.linearVelocity; pausedAngularVelocity = BasketBody.angularVelocity;
                BasketBody.isKinematic = true; floorPaused = true;
            }
            else if (active && floorPaused)
            {
                BasketBody.isKinematic = false;
                BasketBody.linearVelocity = pausedVelocity; BasketBody.angularVelocity = pausedAngularVelocity;
                floorPaused = false;
            }
        }
        public string Summary => $"Корзина {State.Basket.Count}/{State.BasketCapacity} · лоток {State.Tray.Count}/{State.TrayCapacity}"
            + (State.Held == null ? "" : " · " + (FoodInLeftHand ? "левая" : "правая") + " рука: " + State.Held.Ingredient.DisplayName);

        public static string FoodName(IngredientDefinition food)
        {
            // Имена действия в винительном падеже; каталог остаётся именительным.
            switch (food.Id)
            {
                case "potato": return "картошку";
                case "apple": return "яблоко";
                case "egg": return "яйцо";
                case "carrot": return "морковь";
                case "onion": return "лук";
                case "meat": return "мясо";
                case "beef": return "говядину";
                case "cheese": return "сыр";
                case "potato_sack": return "мешок картошки";
                case "egg_carton": return "коробку яиц";
                default: return food.DisplayName;
            }
        }
        public string Describe(PrototypeInteractable aimed)
        {
            if (message != null && bootstrap.Run.Clock.SimulationTime < messageUntil) return message;
            if (aimed == null) return State.Held == null ? "" : "RMB — вернуть продукт на прежнее место";
            var target = aimed.GetComponent<InventoryInteractable>();
            if (target != null && !Own(target)) return "Станция другого участника";
            if (State.Placement == BasketPlacement.Carried && ((target != null && !string.IsNullOrEmpty(target.StationId)) || aimed.CanFocus(bootstrap.Config.PlayerTeam)))
                return "Tab — поставить корзину на свой стол";
            if (target == null) return aimed.CanFocus(bootstrap.Config.PlayerTeam) ? "E — фокус станции · мышь — выбор предмета" : aimed.DisplayName;
            switch (target.Kind)
            {
                case InventoryTargetKind.Pickup: return State.Held != null ? "E — Вернуть продукт в кладовую"
                    : State.Placement == BasketPlacement.Carried ? "E — Взять " + FoodName(target.Ingredient) + " в корзину"
                    : target.Ingredient.DisplayName + " · сначала возьмите корзину Tab";
                case InventoryTargetKind.Basket: return State.Placement == BasketPlacement.Station
                    ? "E — Выгрузить всё в лоток · Tab — взять корзину" : "Tab — взять корзину";
                case InventoryTargetKind.BasketDock: return "Место корзины · Tab — поставить";
                case InventoryTargetKind.PantryRest: return "Tab — поставить корзину";
                case InventoryTargetKind.TrayItem:
                    return State.Held != null ? "E — положить продукт в лоток"
                        : target.Index < State.Tray.Count ? "E — Взять " + FoodName(State.Tray[target.Index].Ingredient)
                        : "Пустое место лотка";
                case InventoryTargetKind.Tray: return State.Held != null ? "E — Положить " + FoodName(State.Held.Ingredient) + " в лоток"
                    : "Лоток · выберите продукт прицелом";
                case InventoryTargetKind.Socket:
                    if (target.Index == (int)StationSocketKind.WorkSurface && State.Held != null && !InventoryState.CanPlaceOnServingSurface(State.Held))
                        return "Готовое блюдо · упаковки и контейнеры положите в лоток";
                    if (target.Index == (int)StationSocketKind.WorkSurface && State.Held == null && State.Socket(target.Index) == null)
                        return "Готовое блюдо · можно собрать экспериментальное блюдо";
                    return State.Held != null ? "E — Положить " + FoodName(State.Held.Ingredient) + " · " + aimed.DisplayName
                    : State.Socket(target.Index) != null ? "E — Взять " + FoodName(State.Socket(target.Index).Ingredient) : aimed.DisplayName + " · пусто";
                case InventoryTargetKind.Trash: return "E — выбросить продукт из руки";
                case InventoryTargetKind.PantryReturn: return "E — вернуть продукт из руки в кладовую";
                default: return "";
            }
        }
    }
}
