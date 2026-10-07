using System;
using System.Collections.Generic;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;

namespace ChefShow.Inventory
{
    public enum PortionLocation { Basket, Tray, Hand, Station, Returned, Trash, Unpacked }
    public enum StationSocketKind { Board, WorkSurface }
    public enum BasketPlacement { Pantry, Carried, Station, Floor }

    // Значения факта, без mutable FoodPortion/GameObject references.
    public readonly struct InventoryChanged
    {
        public readonly string RunId, RoundId, ActorId, Action, PortionId, IngredientId;
        public readonly TeamId Team;
        public readonly BasketPlacement Basket;
        public readonly PortionLocation? Location;
        public readonly float SimulationTime;
        public readonly int Count;
        public InventoryChanged(string runId, TeamId team, float time, string action, string portionId, string ingredientId,
            int count, BasketPlacement basket, PortionLocation? location)
        {
            RunId = runId; RoundId = "prototype"; Team = team; ActorId = team + "1";
            SimulationTime = time; Action = action; PortionId = portionId; IngredientId = ingredientId; Count = count;
            Basket = basket; Location = location;
        }
    }

    public sealed class InventoryState
    {
        private readonly List<FoodPortion> basket = new List<FoodPortion>();
        private readonly List<FoodPortion> tray = new List<FoodPortion>();
        private readonly List<FoodPortion> portions = new List<FoodPortion>();
        private readonly FoodPortion[] sockets = new FoodPortion[2];
        private readonly PrototypeRun run;
        private int serial, originIndex;
        private PortionLocation origin;
        public int BasketCapacity { get; }
        public int TrayCapacity { get; }
        public BasketPlacement Placement { get; private set; } = BasketPlacement.Pantry;
        public int Version { get; private set; }
        public IReadOnlyList<FoodPortion> Basket => basket.AsReadOnly();
        public IReadOnlyList<FoodPortion> Tray => tray.AsReadOnly();
        public IReadOnlyList<FoodPortion> Portions => portions.AsReadOnly();
        public FoodPortion Held { get; private set; }
        public FoodPortion Socket(int index) => index >= 0 && index < sockets.Length ? sockets[index] : null;
        public InventoryState(PrototypeRun owner, int basketCapacity, int trayCapacity)
        {
            if (basketCapacity < 1 || trayCapacity < 1) throw new ArgumentOutOfRangeException(nameof(basketCapacity));
            run = owner ?? throw new ArgumentNullException(nameof(owner));
            BasketCapacity = basketCapacity; TrayCapacity = trayCapacity;
        }

        private bool Active(out string reason)
        {
            reason = run.Disposed ? "Попытка завершена." : run.Clock.Paused ? "Игра на паузе."
                : run.RemainingSeconds <= 0 ? "Время вышло." : null;
            return reason == null;
        }
        private bool FreeHand(out string reason)
        {
            if (!Active(out reason)) return false;
            if (Held == null) return true;
            reason = "Сначала положите или верните продукт из руки."; return false;
        }
        private void Fact(string action, FoodPortion portion = null, int count = 1)
        {
            Version++;
            portion?.RecordOperation(action, run.Clock.SimulationTime);
            run.Events.Publish(new InventoryChanged(run.RunId, run.PlayerTeam, run.Clock.SimulationTime, action, portion?.Id,
                portion?.Ingredient.Id, count, Placement, portion?.Location));
        }

        public bool TryMoveBasket(BasketPlacement destination, out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (!Enum.IsDefined(typeof(BasketPlacement), destination) || destination == Placement
                || (Placement != BasketPlacement.Carried && destination != BasketPlacement.Carried))
            { reason = "Сначала возьмите корзину."; return false; }
            Placement = destination;
            Fact(destination == BasketPlacement.Carried ? "basket_picked_up" : destination == BasketPlacement.Floor ? "basket_dropped" : "basket_placed", count: basket.Count);
            return true;
        }

        public bool TryCollect(IngredientDefinition ingredient, out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.Id)) { reason = "Продукт не настроен."; return false; }
            if (Placement != BasketPlacement.Carried) { reason = "Сначала возьмите корзину: Tab."; return false; }
            if (basket.Count >= BasketCapacity)
            { reason = "Корзина заполнена: " + BasketCapacity + "/" + BasketCapacity + "."; Fact("basket_overloaded", count: 0); return false; }
            if (ingredient.Contents != null && (ingredient.ContentsQuantity < 1 || ingredient.Contents == ingredient
                || ingredient.Contents.Contents != null || ingredient.CanUseBoard || ingredient.IsDoseContainer))
            { reason = "Упаковка не настроена."; return false; }
            var portion = new FoodPortion(run.RunId + "_p" + ++serial, ingredient) { Location = PortionLocation.Basket };
            basket.Add(portion); portions.Add(portion); Fact("ingredient_taken", portion); return true;
        }

        public bool TryUnload(out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (Placement != BasketPlacement.Station) { reason = "Поставьте корзину на свою станцию: Tab."; return false; }
            if (basket.Count == 0) { reason = "Корзина пуста."; return false; }
            if (basket.Count > TrayCapacity - tray.Count) { reason = "Лоток заполнен: выгрузка целиком не помещается."; return false; }
            int count = basket.Count;
            foreach (var portion in basket)
            {
                portion.Location = PortionLocation.Tray;
                portion.RecordOperation("basket_unloaded", run.Clock.SimulationTime);
            }
            tray.AddRange(basket); basket.Clear(); Fact("basket_unloaded", count: count); return true;
        }

        public bool TryUnpack(out string reason)
        {
            if (!FreeHand(out reason)) return false;
            var package = Socket((int)StationSocketKind.WorkSurface);
            if (package == null || package.PackedIngredient == null)
            { reason = "Положите упаковку на место продукта: E."; return false; }
            int count = package.PackedQuantity;
            if (count < 1 || package.PackedIngredient.Contents != null || package.Preparation != PreparationState.Whole
                || package.ChopPresses != 0 || package.Cooking != CookState.Raw || package.HeatProgress != 0
                || package.SaltDoses != 0 || package.OilDoses != 0)
            { reason = "Упаковка не настроена для распаковки."; return false; }
            if (count > TrayCapacity - tray.Count)
            { reason = "Нужно " + count + " свободных мест в лотке; доступно " + (TrayCapacity - tray.Count) + ". Упаковка остаётся целой."; return false; }
            // Commit the complete transfer before callbacks; no partially consumed package or duplicate output.
            var contents = new FoodPortion[count];
            for (int i = 0; i < count; i++)
            {
                var food = new FoodPortion(run.RunId + "_p" + ++serial, package.PackedIngredient) { Location = PortionLocation.Tray };
                food.InheritPackage(package); food.RecordOperation("package_unpacked", run.Clock.SimulationTime); contents[i] = food;
            }
            sockets[(int)StationSocketKind.WorkSurface] = null;
            package.Location = PortionLocation.Unpacked; package.SocketIndex = -1;
            package.RecordOperation("package_unpacked", run.Clock.SimulationTime);
            tray.AddRange(contents); portions.AddRange(contents); Version++;
            var fact = new PackageUnpacked(run, package, contents);
            run.Events.Publish(fact);
            return true;
        }

        public const int RequiredChopPresses = 6;
        public bool TryChop(KitchenToolKind tool, out string reason)
        {
            if (!Active(out reason)) return false;
            if (Held != null) { reason = "Сначала положите продукт из руки."; return false; }
            if (tool != KitchenToolKind.Knife) { reason = "Возьмите нож из ящика: E."; return false; }
            var portion = Socket((int)StationSocketKind.Board);
            if (portion == null) { reason = "Положите продукт на доску: E."; return false; }
            if (!portion.Ingredient.CanUseBoard || portion.PackedIngredient != null || portion.Ingredient.IsDoseContainer)
            { reason = "Этот продукт нельзя нарезать."; return false; }
            if (portion.ChopPresses >= RequiredChopPresses || portion.Preparation != PreparationState.Whole)
            { reason = "Продукт уже нарезан."; return false; }
            portion.ChopPresses++;
            if (portion.ChopPresses == RequiredChopPresses) portion.Preparation = PreparationState.Chopped;
            string action = portion.ChopPresses == 1 ? "cut_started"
                : portion.ChopPresses == RequiredChopPresses ? "preparation_completed" : "cut_progress";
            // Record the state before publishing a value snapshot; callbacks cannot alter the accepted fact.
            Version++; portion.RecordOperation(action, run.Clock.SimulationTime);
            run.Events.Publish(new PreparationChanged(run, portion, action));
            return true;
        }

        public bool TryTakeTray(int index, out string reason) => Take(tray, index, PortionLocation.Tray, out reason);
        private bool Take(List<FoodPortion> source, int index, PortionLocation from, out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (index < 0 || index >= source.Count) { reason = "Здесь нет продукта."; return false; }
            Held = source[index]; origin = from; originIndex = index; source.RemoveAt(index);
            Held.Location = PortionLocation.Hand; Fact("ingredient_transferred", Held); return true;
        }
        public bool TryTakeBasket(int index, out string reason)
        {
            if (Placement == BasketPlacement.Carried) { reason = "Сначала поставьте корзину."; return false; }
            return Take(basket, index, PortionLocation.Basket, out reason);
        }
        public bool TryTakeSocket(int index, out string reason)
        {
            if (!FreeHand(out reason)) return false;
            if (index < 0 || index >= sockets.Length || sockets[index] == null) { reason = "Рабочее место пусто."; return false; }
            Held = sockets[index]; sockets[index] = null; origin = PortionLocation.Station; originIndex = index;
            Held.Location = PortionLocation.Hand; Held.SocketIndex = -1; Fact("ingredient_transferred", Held); return true;
        }
        public bool TryPlaceSocket(int index, out string reason)
        {
            if (!Active(out reason)) return false;
            if (Held == null) { reason = "Сначала возьмите продукт из лотка."; return false; }
            if (index < 0 || index >= sockets.Length) { reason = "Неизвестное рабочее место."; return false; }
            if (sockets[index] != null) { reason = "Рабочее место занято."; return false; }
            if (index == (int)StationSocketKind.Board && !Held.Ingredient.CanUseBoard)
            { reason = "Этот продукт нельзя положить на доску."; return false; }
            var portion = Held; portion.Location = PortionLocation.Station; portion.SocketIndex = index;
            sockets[index] = portion; Held = null; Fact("ingredient_transferred", portion); return true;
        }
        public bool TryPutInTray(out string reason)
        {
            if (!Active(out reason)) return false;
            if (Held == null) { reason = "В руке нет продукта."; return false; }
            if (tray.Count >= TrayCapacity) { reason = "Лоток заполнен."; return false; }
            var portion = Held; portion.Location = PortionLocation.Tray; portion.SocketIndex = -1;
            tray.Add(portion); Held = null; Fact("ingredient_transferred", portion); return true;
        }
        public bool TryCancelHeld(out string reason)
        {
            if (!Active(out reason)) return false;
            if (Held == null) { reason = "В руке нет продукта."; return false; }
            var portion = Held;
            if (origin == PortionLocation.Station) { sockets[originIndex] = portion; portion.SocketIndex = originIndex; }
            else { var source = origin == PortionLocation.Basket ? basket : tray; source.Insert(Math.Min(originIndex, source.Count), portion); }
            portion.Location = origin; Held = null; Fact("ingredient_transferred", portion); return true;
        }
        public bool TryRemove(bool returnToPantry, out string reason)
        {
            if (!Active(out reason)) return false;
            var portion = Held;
            if (portion == null) { reason = "Сначала возьмите продукт в руку."; return false; }
            Held = null;
            portion.Location = returnToPantry ? PortionLocation.Returned : PortionLocation.Trash;
            portion.SocketIndex = -1; Fact(returnToPantry ? "ingredient_returned" : "ingredient_discarded", portion); return true;
        }
    }
}
