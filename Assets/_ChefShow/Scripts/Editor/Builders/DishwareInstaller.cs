using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChefShow.Editor
{
    public static class DishwareInstaller
    {
        [MenuItem("Tools/Chef Show/Install Dishware and Compact Kitchen")]
        public static void Install()=>HandServingInstaller.UpdateScenes(AddToScene,"dishware-compact");
        public static void AddToScene(Scene scene)
        {
            CompactKitchenInstaller.AddToScene(scene);
            var roots=scene.GetRootGameObjects();var b=roots.SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();var arena=roots.Single(r=>r.name=="Arena").transform;
            const string folder="Assets/_ChefShow/Generated/Data/Dishware";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_ChefShow/Generated/Data","Dishware");
            var definitions=new[]{Definition(folder,"small_flat","Маленькая тарелка",6,.58f,.03f,false,new Color(.86f,.89f,.95f)),
                Definition(folder,"large_flat","Большая тарелка",12,.78f,.04f,false,new Color(.63f,.80f,.96f)),
                Definition(folder,"deep_plate","Глубокая тарелка",8,.65f,.12f,true,new Color(.92f,.75f,.42f)),
                Definition(folder,"bowl","Миска",10,.61f,.18f,true,new Color(.47f,.78f,.58f)),
                Definition(folder,"kosushka","Косушка",4,.44f,.13f,true,new Color(.83f,.58f,.83f))};
            const string configPath="Assets/_ChefShow/Generated/Data/DishwareConfig.asset";
            var cfg=AssetDatabase.LoadAssetAtPath<DishwareConfig>(configPath);
            if(cfg==null){cfg=ScriptableObject.CreateInstance<DishwareConfig>();cfg.Types=definitions;AssetDatabase.CreateAsset(cfg,configPath);}
            var controller=b.Dishware??Undo.AddComponent<DishwareController>(b.gameObject);Undo.RecordObject(controller,"Dishware setup");controller.Config=cfg;
            var targets=new System.Collections.Generic.List<DishwareTarget>();
            for(int team=0;team<2;team++)
            {
                float side=team==0?-1:1;string name=team==0?"Dishware Table A":"Dishware Table B";var table=arena.Find(name);
                if(table==null)
                {
                    float first=b.Serving.Stations.OrderBy(s=>s.StationId).First(s=>s.StationId.StartsWith(team==0?"A":"B")).transform.position.z;
                    float last=b.Serving.Stations.OrderBy(s=>s.StationId).Last(s=>s.StationId.StartsWith(team==0?"A":"B")).transform.position.z;
                    table=HandServingInstaller.Node(name,arena,new Vector3(side*b.Layout.DishwareTableX,0,(first+last)/2));
                    var material=HandServingInstaller.Material("Dishware Table",new Color(.48f,.39f,.28f));float length=last-first+3.6f;
                    var top=HandServingInstaller.Shape("Long Counter",table,PrimitiveType.Cube,table.position+Vector3.up*(b.Layout.DishwareTableHeight-.05f),new Vector3(b.Layout.DishwareTableDepth,.1f,length),material);
                    var collision=Undo.AddComponent<BoxCollider>(top);collision.size=Vector3.one;top.layer=0;
                    foreach(float z in new[]{first,last})
                    {
                        var leg=HandServingInstaller.Shape("Support",table,PrimitiveType.Cube,new Vector3(table.position.x,.4f,z),new Vector3(.45f,.8f,.15f),material);leg.layer=0;Undo.AddComponent<BoxCollider>(leg);
                    }
                    for(int station=0;station<6;station++)for(int type=0;type<5;type++)
                    {
                        var point=new Vector3(table.position.x,b.Layout.DishwareTableHeight+.025f,first+station*3.8f+(type-2)*.74f);
                        var node=HandServingInstaller.Node("Supply "+(station+1)+" - "+definitions[type].Id,table,point);
                        var target=Undo.AddComponent<DishwareTarget>(node.gameObject);target.Team=team==0?TeamId.A:TeamId.B;target.Definition=definitions[type];
                        var interaction=Undo.AddComponent<PrototypeInteractable>(node.gameObject);interaction.DisplayName=definitions[type].DisplayName;interaction.InteractionDistanceOverride=4.2f;
                        var collider=Undo.AddComponent<BoxCollider>(node.gameObject);collider.size=new Vector3(.47f,.22f,.52f);collider.center=Vector3.up*.07f;
                        var view=View(node,null);view.Present(definitions[type].Capture());
                        HandServingInstaller.Label(definitions[type].DisplayName+"\nНоминал "+definitions[type].NominalCapacity,node,point+new Vector3(-side*.27f,.22f,0),side,.0038f);
                    }
                }
                targets.AddRange(table.GetComponentsInChildren<DishwareTarget>(true));
            }
            foreach(var target in targets)
            {
                int slot=int.Parse(target.name.Split(' ')[1])-1;int kind=System.Array.IndexOf(definitions,target.Definition);
                var serving=b.Serving.Stations.Single(st=>st.StationId==(target.Team==TeamId.A?"A1":"B1"));
                Undo.RecordObject(target.transform,"Space dishware samples");var point=target.transform.position;point.z=serving.transform.position.z+slot*3.8f+(kind-2)*.74f;target.transform.position=point;
                var mat=HandServingInstaller.Material("Dishware - "+target.Definition.Id,target.Definition.Color);
                var view=target.GetComponent<DishwareView>();
                foreach(var render in new[]{view.Base}.Concat(view.Rim)){Undo.RecordObject(render,"Saved dishware color");render.sharedMaterial=mat;EditorUtility.SetDirty(render);}
                var collider=target.GetComponent<BoxCollider>();Undo.RecordObject(collider,"Dishware pickup surface");
                collider.size=new Vector3(target.Definition.Diameter,target.Definition.Depth+.08f,target.Definition.Diameter);collider.center=Vector3.up*(target.Definition.Depth*.5f);
                view.Present(target.Definition.Capture());EditorUtility.SetDirty(collider);
            }
            controller.Supply=targets.ToArray();
            if(controller.HeldView==null)
            {
                var node=HandServingInstaller.Node("Held Dishware",b.Tools.LeftFoodHand,b.Tools.LeftFoodHand.position);controller.HeldView=View(node,null);controller.HeldView.Present(null);
            }
            foreach(var serving in b.Serving.Stations)
            {
                if(serving.PlateView==null)
                {
                    var content=serving.transform.Find("Plate Contents");var plate=content.Find("Plate").GetComponent<Renderer>();
                    serving.PlateView=View(content,plate);serving.PlateView.Present(cfg.Types.Single(t=>t.Id==cfg.StartingId).Capture());
                    EditorUtility.SetDirty(serving);
                }
            }
            Undo.RecordObject(b,"Dishware controller");b.Dishware=controller;
            Undo.RecordObject(b.Hud.Status.rectTransform,"Dishware hand status");b.Hud.Status.rectTransform.sizeDelta=new Vector2(b.Hud.Status.rectTransform.sizeDelta.x,250);
            Undo.RecordObject(b.Hud.TaskCard,"Dishware instructions");
            if(!b.Hud.TaskCard.text.Contains("ПОСУДА"))b.Hud.TaskCard.text+="\nПОСУДА: длинный стол за участниками. ЛКМ взять слева / поставить на блюдо.\nСмена установленной посуды: еда удаляется, презентабельность −1. Переполнение — горка.\nЛКМ по общему столу / Backspace возвращает посуду; цвет не даёт очков.";
            EditorUtility.SetDirty(b.Hud.TaskCard);EditorUtility.SetDirty(b);EditorUtility.SetDirty(controller);SubmissionRulesInstaller.AddButtonsToScene(scene);Physics.SyncTransforms();
        }
        private static DishwareDefinition Definition(string folder,string id,string name,int capacity,float diameter,float depth,bool liquid,Color color)
        {
            string path=folder+"/"+id+".asset";var d=AssetDatabase.LoadAssetAtPath<DishwareDefinition>(path);if(d!=null)return d;
            d=ScriptableObject.CreateInstance<DishwareDefinition>();d.Id=id;d.DisplayName=name;d.NominalCapacity=capacity;d.Diameter=diameter;d.Depth=depth;d.SupportsLiquid=liquid;d.Color=color;AssetDatabase.CreateAsset(d,path);return d;
        }
        private static DishwareView View(Transform root,Renderer existing)
        {
            var view=Undo.AddComponent<DishwareView>(root.gameObject);
            view.Base=existing??HandServingInstaller.Shape("Dishware Base",root,PrimitiveType.Sphere,root.position,Vector3.one,HandServingInstaller.Material("Dishware",Color.white)).GetComponent<Renderer>();
            view.Rim=Enumerable.Range(0,12).Select(i=>HandServingInstaller.Shape("Dishware Rim "+(i+1),root,PrimitiveType.Cube,root.position,Vector3.one,HandServingInstaller.Material("Dishware",Color.white)).GetComponent<Renderer>()).ToArray();return view;
        }
    }
}
