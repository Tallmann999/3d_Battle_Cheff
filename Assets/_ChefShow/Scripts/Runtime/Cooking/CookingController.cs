using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChefShow.Cooking
{
    public sealed class CookingController : MonoBehaviour
    {
        public CookingConfig Config;
        public CookingStation[] Stations;
        private GameBootstrap bootstrap;
        private InputActionAsset input;
        private string message;
        private CookingTarget messageTarget;
        private float messageUntil;
        private KitchenTool strokeTool;
        private Transform strokePoint;
        private Vector3 strokeStart;
        private Quaternion strokeRotation;
        private float strokeTime = -1;
        private InventoryState State => bootstrap.Run.Inventory;
        public string Validate(InventoryController inventory)
        {
            if (inventory == null || Config == null) return "Готовке нужны инвентарь и конфиг.";
            var error = Config.Validate(); if (error != null) return error;
            if (Stations == null || Stations.Length != 24 || Stations.Any(s => s == null)
                || Stations.Select(s => s.StationId + "/" + s.Kind).Distinct().Count() != 24) return "Нужны 24 уникальных прибора на 12 станциях.";
            foreach (var station in Stations)
                if (station.Food == null || station.Food.Length != 3 || station.Status == null || station.StirPoint == null
                    || station.HeatIndicator == null || station.Smoke == null || station.Smoke.Length != 3
                    || station.Food.Any(v => v == null || v.Visual == null || v.CutPieces == null || v.CutPieces.Length != 7)) return "Не заполнены ссылки прибора " + station.StationId;
            if (Stations.Count(s => s.StationId == inventory.PlayerStationId) != 2) return "Нет двух приборов участника игрока.";
            return null;
        }
        public void Initialize(GameBootstrap owner, InputActionAsset actions)
        { bootstrap = owner; input = actions; bootstrap.Player.ExtendedInteractionDistance = Config.ApplianceInteractionDistance; }
        public void ResetPresentation()
        { StopStroke(); message = null; messageTarget = null; Present(); }
        public bool Step(bool acceptInput, bool commandConsumed)
        {
            if (strokeTool != null && (strokeTool != bootstrap.Tools.EquippedObject || strokeTool.Placement != KitchenToolPlacement.Held))
            { strokeTool = null; strokeTime = -1; }
            bool consumed = false;
            if (acceptInput && !commandConsumed)
            {
                var map = input.FindActionMap(bootstrap.Player.Focused ? "Station" : "Gameplay", true);
                bool interact = map.FindAction("Interact", true).WasPressedThisFrame();
                bool primary = map.FindAction("Primary", true).WasPressedThisFrame();
                if (interact || primary)
                {
                    bootstrap.Player.RefreshTarget();
                    var target = bootstrap.Player.Target == null ? null : bootstrap.Player.Target.GetComponent<CookingTarget>();
                    if (target != null)
                    {
                        consumed = true; string reason = null; bool success = false;
                        if (target.Station.StationId != bootstrap.Inventory.PlayerStationId) reason = "Это прибор другого участника.";
                        else if (interact)
                        {
                            if (target.Kind == CookingTargetKind.HeatKnob) success = State.TryCycleHeat(target.Station.Kind, out reason);
                            else if (State.Held != null) success = State.TryPlaceCooker(target.Station.Kind, out reason);
                            else if (bootstrap.Tools.FoodInLeftHand && State.Placement == BasketPlacement.Carried) reason = "Сначала поставьте корзину Tab.";
                            else success = State.TryTakeCooker(target.Station.Kind, target.Kind == CookingTargetKind.Food ? target.Index : 0, out reason);
                        }
                        else if (target.Kind != CookingTargetKind.HeatKnob)
                        {
                            if (State.Held != null && State.Held.Ingredient.IsDoseContainer)
                                success = State.TryApplySeasoning(target.Station.Kind,
                                    target.Kind == CookingTargetKind.Food ? target.Index : -1, out reason);
                            else
                            {
                                success = State.TryStir(target.Station.Kind, bootstrap.Tools.Equipped, out reason);
                                if (success) StartStroke(target.Station);
                            }
                        }
                        message = success ? null : reason; messageTarget = target; messageUntil = bootstrap.Run.Clock.SimulationTime + 2.5f;
                    }
                }
            }
            if (acceptInput) AnimateStroke(bootstrap.Run.Clock.Delta);
            Present(); return consumed;
        }
        private void Present()
        { foreach (var station in Stations) station.Present(State, station.StationId == bootstrap.Inventory.PlayerStationId); }
        private void StartStroke(CookingStation station)
        {
            StopStroke(); strokeTool = bootstrap.Tools.EquippedObject; strokePoint = station.StirPoint; strokeTime = 0;
            strokeStart = strokeTool.transform.position; strokeRotation = strokeTool.transform.rotation;
        }
        private void AnimateStroke(float delta)
        {
            if (strokeTool == null || strokeTime < 0) return;
            strokeTime += delta; float t = Mathf.Clamp01(strokeTime / .45f);
            var contact = strokePoint.position + new Vector3(Mathf.Sin(t * Mathf.PI * 2) * .08f, .09f, Mathf.Cos(t * Mathf.PI * 2) * .08f);
            float blend = Mathf.Sin(t * Mathf.PI);
            strokeTool.transform.SetPositionAndRotation(Vector3.Lerp(strokeStart, contact, blend),
                Quaternion.Slerp(strokeRotation, Quaternion.Euler(70,0,30), blend));
            if (t >= 1) StopStroke();
        }
        private void StopStroke()
        {
            if (strokeTool != null && strokeTool.Placement == KitchenToolPlacement.Held)
            { strokeTool.transform.localPosition = Vector3.zero; strokeTool.transform.localRotation = Quaternion.identity; }
            strokeTool = null; strokeTime = -1;
        }
        public static string HeatName(HeatLevel level) => level == HeatLevel.Off ? "Выкл." : level == HeatLevel.Low ? "Слабый" : level == HeatLevel.Medium ? "Средний" : "Сильный";
        public static string CookerName(CookerKind kind) => kind == CookerKind.Pan ? "Сковорода" : "Кастрюля";
        public static string FoodState(FoodPortion food) => food.Cooking == CookState.Burned ? "СГОРЕЛО" : food.Cooking == CookState.Overcooked ? "Переготовлено"
            : food.Cooking == CookState.Cooked ? "Готово" : food.Cooking == CookState.Raw ? "Сырое" : "Готовится";
        public string Describe(PrototypeInteractable aimed)
        {
            var target = aimed == null ? null : aimed.GetComponent<CookingTarget>(); if (target == null) return null;
            if (target.Station.StationId != bootstrap.Inventory.PlayerStationId) return "Прибор другого участника";
            if (message != null && messageTarget == target && bootstrap.Run.Clock.SimulationTime < messageUntil) return message;
            var kind = target.Station.Kind; var foods = State.Cooker(kind); var heat = State.Heat(kind);
            if (target.Kind == CookingTargetKind.HeatKnob) return "E — Нагрев: " + HeatName(heat) + " → " + HeatName((HeatLevel)(((int)heat + 1) % 4));
            string state = CookerName(kind) + " · " + HeatName(heat) + " · " + foods.Count + "/" + State.CookingRules.Capacity;
            if (State.Held != null && State.Held.Ingredient.IsDoseContainer)
            {
                SeasoningKind seasoning;
                if (!State.CookingRules.TrySeasoning(State.Held.Ingredient, out seasoning)) return "Этот контейнер не настроен для дозирования.";
                if (seasoning == SeasoningKind.Oil && kind != CookerKind.Pan) return "Масло добавляется только в сковороду.";
                if (target.Kind != CookingTargetKind.Food || target.Index >= foods.Count) return "ЛКМ — добавить дозу: наведите на конкретную еду\n" + state;
                var selected = foods[target.Index];
                return "ЛКМ — Добавить дозу " + (seasoning == SeasoningKind.Salt ? "соли" : "масла")
                    + " (" + State.CookingRules.DosesPerPress + ") · " + InventoryController.FoodName(selected.Ingredient) + "\n" + Doses(selected);
            }
            if (State.Held != null) return "E — Положить " + InventoryController.FoodName(State.Held.Ingredient) + " в прибор\n" + state;
            int index = target.Kind == CookingTargetKind.Food ? target.Index : 0;
            if (index >= foods.Count) return state + "\nE по ручке — переключить нагрев";
            var food = foods[index];
            return "E — Взять " + InventoryController.FoodName(food.Ingredient) + " · " + FoodState(food) + "\n"
                + state + " · " + Mathf.FloorToInt(Mathf.Min(100, food.HeatProgress / State.CookingRules.Ready * 100)) + "%"
                + " · " + Doses(food)
                + (kind == CookerKind.Pot ? " · ЛКМ лопаткой: " + food.StirPresses + "/" + food.RequiredStirs : "")
                + (kind == CookerKind.Pan && food.HeatProgress >= State.CookingRules.Ready * .85f && food.Cooking != CookState.Burned ? " · следите за нагревом" : "");
        }
        private static string Doses(FoodPortion food) => "Соль: " + food.SaltDoses + " · Масло: " + food.OilDoses;
        public string Summary => "Сковорода: " + HeatName(State.Heat(CookerKind.Pan)) + " · " + State.Cooker(CookerKind.Pan).Count + "/" + State.CookingRules.Capacity
            + "   Кастрюля: " + HeatName(State.Heat(CookerKind.Pot)) + " · " + State.Cooker(CookerKind.Pot).Count + "/" + State.CookingRules.Capacity;
    }
}
