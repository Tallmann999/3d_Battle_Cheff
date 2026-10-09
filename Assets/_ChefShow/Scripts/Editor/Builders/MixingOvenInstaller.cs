using System.Linq;
using ChefShow.Core;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class MixingOvenInstaller
    {
        [MenuItem("Tools/Chef Show/Install Bowl and Wall Ovens")]
        public static void Install()=>HandServingInstaller.UpdateScenes(AddToScene,"bowl-oven");
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var mixing=b.Mixing??Undo.AddComponent<MixingController>(b.gameObject);Undo.RecordObject(b,"Mixing controller");b.Mixing=mixing;Undo.RecordObject(mixing,"Mixing stations");
            const string path="Assets/_ChefShow/Generated/Data/Ingredients/mixture.asset";
            var definition=AssetDatabase.LoadAssetAtPath<IngredientDefinition>(path);
            if(definition==null)
            {
                definition=ScriptableObject.CreateInstance<IngredientDefinition>();definition.name="mixture";definition.Id="mixture";definition.DisplayName="Смесь";
                definition.VisualMesh=b.Inventory.Catalog.Ingredients.First(d=>d.Id=="egg").VisualMesh;
                definition.VisualMaterial=HandServingInstaller.Material("Mixture",new Color(.85f,.68f,.34f));definition.VisualScale=new Vector3(.3f,.11f,.3f);
                AssetDatabase.CreateAsset(definition,path);
            }
            mixing.MixtureDefinition=definition;
            var tables=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PrototypeInteractable>(true)).Where(t=>t.name.StartsWith("Station_")).OrderBy(t=>t.name).ToArray();
            var bowls=new System.Collections.Generic.List<MixingStation>();
            foreach(var table in tables)
            {
                string id=table.name.Substring(8);float side=id.StartsWith("A")?-1:1;var bounds=table.GetComponent<BoxCollider>().bounds;
                var group=table.transform.Find("Cooking");
                var bowl=group.Find("Mixing Bowl");
                bowls.Add(bowl==null?BuildBowl(group,new Vector3(bounds.center.x,bounds.max.y+.09f,bounds.center.z-side*.55f),id,side,b):bowl.GetComponent<MixingStation>());
                if(group.Find("Oven")==null)BuildOven(group,new Vector3(side*28.7f,0,bounds.center.z),id,side,b);
            }
            mixing.Stations=bowls.ToArray();Undo.RecordObject(b.Cooking,"Include saved ovens");
            b.Cooking.Stations=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CookingStation>(true)).OrderBy(s=>s.StationId).ThenBy(s=>s.Kind).ToArray();
            EditorUtility.SetDirty(b.Cooking.Config);EditorUtility.SetDirty(b.Cooking);EditorUtility.SetDirty(mixing);EditorUtility.SetDirty(b);
            HandServingInstaller.UpdateHud(b);Undo.RecordObject(b.Hud.TaskCard,"Bowl oven task");
            b.Hud.TaskCard.text+="\nМиска: ЛКМ добавить продукты, ПКМ удерживать 3 с ложкой/лопаткой.\nСмесь: ЛКМ взять, отнести в духовку у стены за участником.\nЛКМ открыть / положить в форму / закрыть; нагрев отдельной ручкой.\nСредний: выпечка 45 с, переготовка 60 с, сгорание 75 с.\nОткрытая дверца останавливает нагрев. Снять еду — ЛКМ при открытой дверце.";
            EditorUtility.SetDirty(b.Hud.TaskCard);Physics.SyncTransforms();
            var error=mixing.Validate()??b.Cooking.Validate(b.Inventory);if(error!=null)throw new System.InvalidOperationException(error);
        }
        private static MixingStation BuildBowl(Transform parent,Vector3 p,string id,float side,GameBootstrap b)
        {
            var root=HandServingInstaller.Node("Mixing Bowl",parent,p);var station=Undo.AddComponent<MixingStation>(root.gameObject);station.StationId=id;
            MixTarget(root,station,-1,new Vector3(.44f,.10f,.44f));
            var material=HandServingInstaller.Material("Mixing Bowl",new Color(.27f,.68f,.74f));
            HandServingInstaller.Shape("Bowl Bottom",root,PrimitiveType.Cylinder,p,new Vector3(.4f,.012f,.4f),material);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;var wall=HandServingInstaller.Shape("Bowl Rim "+(i+1),root,PrimitiveType.Cube,p+new Vector3(Mathf.Cos(a)*.19f,.035f,Mathf.Sin(a)*.19f),new Vector3(.045f,.08f,.115f),material);wall.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
            }
            station.Contact=HandServingInstaller.Node("Mix Contact",root,p+Vector3.up*.07f);station.Food=new ChefShow.Ingredients.FoodDisplay[6];
            for(int i=0;i<6;i++)
            {
                var item=HandServingInstaller.Node("Bowl Food_"+(i+1),root,p+new Vector3((i%3-1)*.11f,.11f,(i/3-.5f)*.13f));MixTarget(item,station,i,new Vector3(.1f,.09f,.11f));
                station.Food[i]=HandServingInstaller.Food(item,b,.45f);item.GetComponent<BoxCollider>().enabled=false;
            }
            station.Status=HandServingInstaller.Label("МИСКА",root,p+new Vector3(side*.19f,.10f,0),side,.0038f);EditorUtility.SetDirty(station);return station;
        }
        private static void MixTarget(Transform root,MixingStation station,int index,Vector3 size)
        {
            var t=Undo.AddComponent<MixingTarget>(root.gameObject);t.Station=station;t.Index=index;
            var proto=Undo.AddComponent<PrototypeInteractable>(root.gameObject);proto.DisplayName="Миска";proto.InteractionDistanceOverride=4.2f;
            var collider=Undo.AddComponent<BoxCollider>(root.gameObject);collider.size=size;
        }
        private static CookingStation BuildOven(Transform parent,Vector3 p,string id,float side,GameBootstrap b)
        {
            var root=HandServingInstaller.Node("Oven",parent,p);var station=Undo.AddComponent<CookingStation>(root.gameObject);station.StationId=id;station.Kind=CookerKind.Oven;
            var metal=HandServingInstaller.Material("Oven Metal",new Color(.18f,.22f,.25f));
            var body=HandServingInstaller.Node("Body Collision",root,p+Vector3.up*.4f);var bodyCollider=Undo.AddComponent<BoxCollider>(body.gameObject);bodyCollider.size=new Vector3(.72f,.8f,.94f);
            HandServingInstaller.Shape("Body Base",root,PrimitiveType.Cube,p+Vector3.up*.4f,new Vector3(.72f,.8f,.94f),metal);
            HandServingInstaller.Shape("Back",root,PrimitiveType.Cube,p+new Vector3(side*.32f,1.05f,0),new Vector3(.09f,.6f,.94f),metal);
            HandServingInstaller.Shape("Top",root,PrimitiveType.Cube,p+Vector3.up*1.36f,new Vector3(.74f,.12f,.94f),metal);
            foreach(float z in new[]{-.44f,.44f})HandServingInstaller.Shape("Side",root,PrimitiveType.Cube,p+new Vector3(0,1.05f,z),new Vector3(.72f,.6f,.07f),metal);
            var rack=HandServingInstaller.Node("Bake Form",root,p+new Vector3(-side*.12f,.96f,0));CookTarget(rack,station,CookingTargetKind.Vessel,0,new Vector3(.48f,.04f,.7f));
            HandServingInstaller.Shape("Form Base",rack,PrimitiveType.Cube,rack.position,new Vector3(.48f,.04f,.7f),HandServingInstaller.Material("Bake Form",new Color(.41f,.43f,.48f)));
            var item=HandServingInstaller.Node("Food Slot_1",rack,rack.position+new Vector3(-side*.1f,.09f,0));CookTarget(item,station,CookingTargetKind.Food,0,new Vector3(.35f,.16f,.35f));
            station.Food=new[]{HandServingInstaller.Food(item,b,1)};item.GetComponent<BoxCollider>().enabled=false;station.StirPoint=HandServingInstaller.Node("Form Center",rack,item.position);
            var hinge=HandServingInstaller.Node("Door Hinge",root,p+new Vector3(-side*.39f,.82f,0));station.Door=hinge;
            station.DoorClosedRotation=Quaternion.identity;station.DoorOpenRotation=Quaternion.Euler(0,0,side*80);hinge.localRotation=station.DoorOpenRotation;
            var door=HandServingInstaller.Shape("Door Panel",hinge,PrimitiveType.Cube,hinge.position+Vector3.up*.24f,new Vector3(.05f,.48f,.78f),HandServingInstaller.Material("Oven Door",new Color(.17f,.27f,.36f)));
            // Build in the closed local pose, then show the saved open preview.
            door.transform.localPosition=new Vector3(0,.24f,0);door.transform.localRotation=Quaternion.identity;
            var handle=HandServingInstaller.Node("Door Handle",root,p+new Vector3(-side*.43f,1.31f,.28f));CookTarget(handle,station,CookingTargetKind.Door,0,new Vector3(.19f,.12f,.2f));
            HandServingInstaller.Shape("Handle",handle,PrimitiveType.Cylinder,handle.position,new Vector3(.14f,.05f,.14f),HandServingInstaller.Material("Oven Handle",new Color(.74f,.78f,.82f)));
            var knob=HandServingInstaller.Node("Heat Knob",root,p+new Vector3(-side*.43f,1.39f,-.27f));CookTarget(knob,station,CookingTargetKind.HeatKnob,0,new Vector3(.18f,.14f,.18f));
            station.HeatIndicator=HandServingInstaller.Shape("Knob",knob,PrimitiveType.Cylinder,knob.position,new Vector3(.15f,.045f,.15f),HandServingInstaller.Material("Oven Knob",new Color(.9f,.64f,.2f))).GetComponent<Renderer>();
            station.Status=HandServingInstaller.Label("Выкл. · открыта",root,p+new Vector3(-side*.43f,1.55f,0),side,.005f);
            HandServingInstaller.Label("ДУХОВКА "+id,root,p+new Vector3(-side*.43f,.60f,0),side,.007f);
            var smoke=HandServingInstaller.Material("Cooking Smoke",new Color(.38f,.38f,.38f));station.Smoke=Enumerable.Range(0,3).Select(i=>{var r=HandServingInstaller.Shape("Smoke "+(i+1),root,PrimitiveType.Sphere,p+new Vector3((i-1)*.06f,1.8f+i*.12f,0),Vector3.one*(.12f+i*.05f),smoke).GetComponent<Renderer>();r.enabled=false;return r;}).ToArray();
            EditorUtility.SetDirty(station);return station;
        }
        private static void CookTarget(Transform root,CookingStation station,CookingTargetKind kind,int index,Vector3 size)
        {
            var t=Undo.AddComponent<CookingTarget>(root.gameObject);t.Station=station;t.Kind=kind;t.Index=index;
            var proto=Undo.AddComponent<PrototypeInteractable>(root.gameObject);proto.DisplayName=CookingController.CookerName(station.Kind);proto.InteractionDistanceOverride=4.2f;
            var collider=Undo.AddComponent<BoxCollider>(root.gameObject);collider.size=size;
        }
    }
}
