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
    public sealed class MixingController : MonoBehaviour
    {
        public MixingStation[] Stations; public IngredientDefinition MixtureDefinition;
        private GameBootstrap bootstrap;private InputActionAsset input;private KitchenTool stirring;
        private string message;private MixingTarget messageTarget;private float messageUntil;
        private InventoryState State=>bootstrap.Run.Inventory;
        public string Validate()=>MixtureDefinition==null || MixtureDefinition.Id!="mixture" || Stations==null || Stations.Length!=12 || Stations.Select(s=>s==null?null:s.StationId).Distinct().Count()!=12 || Stations.Any(s=>s==null || s.Food==null || s.Food.Length!=6 || s.Status==null || s.Contact==null)?"Не заполнены миски/смесь.":null;
        public void Initialize(GameBootstrap b,InputActionAsset actions){bootstrap=b;input=actions;}
        public void ResetPresentation(){StopStir();message=null;Present();}
        public bool Step(bool active,bool commandConsumed)
        {
            bool consumed=false;bool keep=false;
            if(active && !commandConsumed)
            {
                var map=input.FindActionMap(bootstrap.Player.Focused?"Station":"Gameplay",true);
                bool left=map.FindAction("Primary",true).WasPressedThisFrame();bool right=map.FindAction("Secondary",true).IsPressed();
                if(left || right)
                {
                    bootstrap.Player.RefreshTarget();var target=bootstrap.Player.Target==null?null:bootstrap.Player.Target.GetComponent<MixingTarget>();
                    if(target!=null)
                    {
                        consumed=true;string reason=null;bool success=false;
                        if(target.Station.StationId!=bootstrap.Inventory.PlayerStationId)reason="Это миска другого участника.";
                        else if(left)
                        {
                            if(State.Held!=null && State.Held.Ingredient.IsDoseContainer)success=State.TrySeasonBowl(target.Index<0?0:target.Index,out reason);
                            else if(State.Held!=null)success=State.TryPlaceBowl(out reason);
                            else if(State.Placement==BasketPlacement.Carried)reason="Сначала поставьте корзину Tab.";
                            else success=State.TryTakeBowl(target.Index<0?0:target.Index,out reason);
                        }
                        else
                        {
                            success=State.TryAdvanceMix(bootstrap.Run.Clock.Delta,bootstrap.Tools.Equipped,MixtureDefinition,out reason);
                            keep=success; if(success){stirring=bootstrap.Tools.EquippedObject;var phase=bootstrap.Run.Clock.SimulationTime*8;
                                stirring.transform.position=target.Station.Contact.position+new Vector3(Mathf.Sin(phase)*.05f,.1f,Mathf.Cos(phase)*.05f);}
                        }
                        message=success?null:reason;messageTarget=target;messageUntil=bootstrap.Run.Clock.SimulationTime+2.5f;
                    }
                }
            }
            if(active && !keep)StopStir();Present();return consumed;
        }
        private void StopStir(){if(stirring!=null && stirring.Placement==KitchenToolPlacement.Held){stirring.transform.localPosition=Vector3.zero;stirring.transform.localRotation=Quaternion.identity;}stirring=null;}
        private void Present(){foreach(var s in Stations)s.Present(State,s.StationId==bootstrap.Inventory.PlayerStationId);}
        public string Describe(PrototypeInteractable aimed)
        {
            var target=aimed==null?null:aimed.GetComponent<MixingTarget>();if(target==null)return null;
            if(target.Station.StationId!=bootstrap.Inventory.PlayerStationId)return "Миска другого участника";
            if(message!=null && messageTarget==target && bootstrap.Run.Clock.SimulationTime<messageUntil)return message;
            if(State.Held!=null && State.Held.Ingredient.IsDoseContainer)return "ЛКМ — Добавить дозу на продукт в миске";
            if(State.Held!=null)return "ЛКМ — Положить "+InventoryController.FoodName(State.Held.Ingredient)+" в миску";
            int index=target.Index<0?0:target.Index;
            if(index>=State.Bowl.Count)return "Миска · положите продукты левой рукой";
            return "ЛКМ — Взять "+InventoryController.FoodName(State.Bowl[index].Ingredient)+"\nПКМ — удерживать для смешивания · ложка/лопатка · "+Mathf.FloorToInt(State.MixProgress/State.CookingRules.MixSeconds*100)+"%";
        }
    }
}
