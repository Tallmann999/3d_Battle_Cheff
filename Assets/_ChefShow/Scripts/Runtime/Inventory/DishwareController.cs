using System.Collections.Generic;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Player;
using UnityEngine;
using UnityEngine.InputSystem;
namespace ChefShow.Inventory
{
    public sealed class DishwareController : MonoBehaviour
    {
        public DishwareConfig Config;
        public DishwareTarget[] Supply;
        public DishwareView HeldView;
        private GameBootstrap bootstrap;private InputActionAsset input;
        private readonly Dictionary<DishwareTarget,DishwareSnapshot> profiles=new Dictionary<DishwareTarget,DishwareSnapshot>();
        private string message;private PrototypeInteractable messageTarget;private float until;
        private InventoryState State=>bootstrap.Run.Inventory;
        public string Validate()
        {
            if(Config==null || Config.Validate()!=null)return "Не настроен каталог посуды.";
            if(Supply==null || Supply.Length!=60 || HeldView==null || HeldView.Base==null || HeldView.Rim==null || HeldView.Rim.Length!=12)return "Не сохранены столы посуды и визуал левой руки.";
            foreach(var t in Supply)if(t==null || t.Definition==null)return "Не заполнена цель посуды.";
            return null;
        }
        public void Initialize(GameBootstrap b,InputActionAsset actions){bootstrap=b;input=actions;}
        public void ResetPresentation()
        {
            message=null;profiles.Clear();
            foreach(var source in Supply)
            {
                var profile=State.FindDishware(source.Definition.Id);profiles[source]=profile;
                source.GetComponent<DishwareView>().Present(profile);
                var label=source.GetComponentInChildren<TextMesh>();if(label!=null && profile!=null)label.text=profile.DisplayName+"\nНоминал "+profile.NominalCapacity;
            }
            Present();
        }
        public bool Step(bool active,bool consumed)
        {
            bool handled=false;
            if(active && !consumed && input.FindAction((bootstrap.Player.Focused?"Station":"Gameplay")+"/Primary",true).WasPressedThisFrame())
            {
                bootstrap.Player.RefreshTarget();var aimed=bootstrap.Player.Target;var target=aimed==null?null:aimed.GetComponent<DishwareTarget>();
                if(target!=null)
                {
                    handled=true;string reason;bool success=false;
                    if(target.Team!=bootstrap.Run.PlayerTeam)reason="Это стол посуды другой команды.";
                    else if(State.HeldDishware!=null)success=State.TryReturnDishware(out reason);
                    else success=State.TryTakeDishware(profiles[target].Id,out reason);
                    message=success?null:reason;messageTarget=aimed;until=bootstrap.Run.Clock.SimulationTime+2.5f;
                }
            }
            Present();return handled;
        }
        private void Present(){HeldView.Present(State.HeldDishware);}
        public string Describe(PrototypeInteractable aimed)
        {
            var target=aimed==null?null:aimed.GetComponent<DishwareTarget>();if(target==null)return null;
            if(target.Team!=bootstrap.Run.PlayerTeam)return "Посуда другой команды";
            if(message!=null && messageTarget==aimed && bootstrap.Run.Clock.SimulationTime<until)return message;
            if(State.HeldDishware!=null)return "ЛКМ — Вернуть посуду на общий стол";
            var p=profiles[target];return "ЛКМ — Взять "+p.DisplayName+"\nНоминал "+p.NominalCapacity+" · "+(p.SupportsLiquid?"глубокая":"плоская")+" · переполнение с горкой";
        }
    }
}
