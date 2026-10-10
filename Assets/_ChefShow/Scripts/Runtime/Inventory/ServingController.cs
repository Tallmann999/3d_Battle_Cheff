using System.Linq;
using ChefShow.Core;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ChefShow.Inventory
{
    public sealed class ServingController : MonoBehaviour
    {
        public ServingStation[] Stations;
        [Tooltip("Temporary prototype helper. Disable for ordinary show gameplay.")]
        public bool EnableSubmissionReset = true;
        private GameBootstrap bootstrap;
        private InputActionAsset input;
        private string message; private PrototypeInteractable messageTarget; private float messageUntil;
        private InventoryState State => bootstrap.Run.Inventory;
        public string Validate() => Stations==null || Stations.Length!=12 || Stations.Select(s=>s==null?null:s.StationId).Distinct().Count()!=12 || Stations.Any(s=>s==null || s.Food==null || s.Food.Length!=48 || s.Status==null)?"Не заполнены 12 тарелок и их сохранённые порции.":null;
        public void Initialize(GameBootstrap owner,InputActionAsset actions) {bootstrap=owner;input=actions;}
        public void ResetPresentation() {message=null;Present();}
        public bool Step(bool acceptInput,bool commandConsumed)
        {
            bool consumed=false;
            if(acceptInput && !commandConsumed)
            {
                var map=input.FindActionMap(bootstrap.Player.Focused?"Station":"Gameplay",true);
                if(map.FindAction("Primary",true).WasPressedThisFrame())
                {
                    bootstrap.Player.RefreshTarget();var aimed=bootstrap.Player.Target;
                    var serving=aimed==null?null:aimed.GetComponent<ServingTarget>();
                    var socket=aimed==null?null:aimed.GetComponent<InventoryInteractable>();
                    bool board=socket!=null && socket.Kind==InventoryTargetKind.Socket && socket.Index==0;
                    bool dose=State.Held!=null && State.Held.Ingredient.IsDoseContainer;
                    if(serving!=null || (board && dose))
                    {
                        consumed=true;string reason=null;bool success=false;
                        string station=serving!=null?serving.Station.StationId:socket.StationId;
                        if(station!=bootstrap.Inventory.PlayerStationId) reason="Это станция другого участника.";
                        else if(board) success=State.TrySeasonBoard(out reason);
                        else if(serving.ResetSubmission)
                        {if(EnableSubmissionReset)success=State.TryResetSubmission(out reason);else reason="Тестовая отмена отключена.";}
                        else if(serving.Submit) success=State.TrySubmitDish(out reason);
                        else if(State.HeldDishware!=null)success=State.TryPlaceDishware(out reason);
                        else if(dose) success=State.TrySeasonPlate(out reason);
                        else if(State.Held!=null) success=State.TryPlaceServing(out reason);
                        else if(State.Placement==BasketPlacement.Carried) reason="Сначала поставьте корзину Tab.";
                        else if(serving.Index<0)success=State.TryTakePlacedDishware(out reason);
                        else success=State.TryTakeServing(serving.Index,out reason);
                        message=success?null:reason;messageTarget=aimed;messageUntil=bootstrap.Run.Clock.SimulationTime+2.5f;
                    }
                }
            }
            Present();return consumed;
        }
        private void Present()
        {
            var heldMount=bootstrap.Dishware==null || bootstrap.Dishware.HeldView==null?null:bootstrap.Dishware.HeldView.transform;
            foreach(var s in Stations)s.Present(State,s.StationId==bootstrap.Inventory.PlayerStationId,heldMount);
        }
        public string Describe(PrototypeInteractable aimed)
        {
            var target=aimed==null?null:aimed.GetComponent<ServingTarget>();
            var socket=aimed==null?null:aimed.GetComponent<InventoryInteractable>();
            bool board=socket!=null && socket.Kind==InventoryTargetKind.Socket && socket.Index==0;
            bool dose=State.Held!=null && State.Held.Ingredient.IsDoseContainer;
            if(target==null && !(board && dose))return null;
            string station=target!=null?target.Station.StationId:socket.StationId;
            if(station!=bootstrap.Inventory.PlayerStationId)return "Станция другого участника";
            if(message!=null && messageTarget==aimed && bootstrap.Run.Clock.SimulationTime<messageUntil)return message;
            if(target!=null && target.ResetSubmission)return !EnableSubmissionReset?"Тестовая отмена отключена":bootstrap.Run.RemainingSeconds<=0?"Время вышло · отмена недоступна":State.SubmittedDish==null?"ТЕСТ · блюдо ещё не подано":"ЛКМ — Отменить подачу (тест)";
            if(target!=null && State.SubmittedDish!=null)return "Блюдо подано · "+State.SubmittedDish.Quantity+" порций";
            if(target!=null && target.Submit)return "ЛКМ — Подать блюдо · "+State.Served.Count+" порций";
            if(State.HeldDishware!=null)
            {
                if(State.HeldDishwareFromStation)return State.CurrentDishware==null?"ЛКМ — Вернуть "+State.HeldDishware.DisplayName+" вместе с едой · "+State.HeldServed.Count+" порций":"Место блюда занято · возврат тарелки недоступен";
                return "ЛКМ — Поставить "+State.HeldDishware.DisplayName+(State.CurrentDishware==null?"":" · оставшаяся еда удалится · −1 балл");
            }
            if(target!=null && target.Index<0 && !dose && State.Held==null)return State.CurrentDishware==null?"Место блюда · сначала поставьте посуду":"ЛКМ — Взять "+State.CurrentDishware.DisplayName+" вместе с едой · −1 балл";
            if(dose)return "ЛКМ — Добавить дозу "+(State.Held.Ingredient.Id==State.CookingRules.SaltIngredientId?"соли":"масла")+(board?" на продукт":" на блюдо");
            if(State.Held!=null)return "ЛКМ — Положить "+InventoryController.FoodName(State.Held.Ingredient)+" на тарелку";
            int index=target.Index<0?State.Served.Count-1:target.Index;
            return index>=0 && index<State.Served.Count?"ЛКМ — Взять "+InventoryController.FoodName(State.Served[index].Ingredient)+" · "+ChefShow.Cooking.CookingController.FoodState(State.Served[index]):"Тарелка · положите еду левой рукой";
        }
    }
}
