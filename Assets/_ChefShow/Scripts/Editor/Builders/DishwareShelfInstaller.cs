using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Cooking;
using ChefShow.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChefShow.Editor
{
    public static class DishwareShelfInstaller
    {
        [MenuItem("Tools/Chef Show/Install Dishware Shelves and Test Submission Reset")]
        public static void Install()=>HandServingInstaller.UpdateScenes(scene=>{AddToScene(scene);SubmissionResetInstaller.AddToScene(scene);},"shelves-reset");
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var arena=scene.GetRootGameObjects().Single(r=>r.name=="Arena").transform;
            var cfg=b.Layout;var material=HandServingInstaller.Material("Dishware Table",new Color(.48f,.39f,.28f));
            var ids=new[]{"small_flat","large_flat","deep_plate","bowl","kosushka"};
            foreach(var station in b.Serving.Stations.OrderBy(s=>s.StationId))
            {
                bool a=station.StationId.StartsWith("A");float side=a?-1:1;var team=a?TeamId.A:TeamId.B;
                var oven=b.Cooking.Stations.Single(s=>s.StationId==station.StationId && s.Kind==CookerKind.Oven);
                var parent=oven.transform.parent;var shelf=parent.Find("Dishware Shelf");
                var p=oven.transform.position+new Vector3(0,0,cfg.DishwareShelfOvenOffset);
                if(shelf==null)
                {
                    var old=station.StationId.EndsWith("1")?arena.Find(a?"Dishware Table A":"Dishware Table B"):null;
                    if(old!=null){shelf=old;Undo.SetTransformParent(shelf,parent,"Repurpose dishware bench");shelf.name="Dishware Shelf";}
                    else shelf=HandServingInstaller.Node("Dishware Shelf",parent,p);
                }
                Undo.RecordObject(shelf,"Shelf beside own oven");shelf.position=p;shelf.rotation=Quaternion.identity;
                var scale=parent.lossyScale;shelf.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
                var legacy=shelf.Cast<Transform>().Where(t=>t.name=="Long Counter" || t.name=="Support").ToArray();
                for(int level=0;level<3;level++)
                {
                    string name="Shelf Level "+(level+1);var board=shelf.Find(name);
                    if(board==null && level<legacy.Length){board=legacy[level];Undo.RecordObject(board,"Repurpose bench into shelf");board.name=name;}
                    var point=p+Vector3.up*(cfg.DishwareShelfFirstLevel+level*cfg.DishwareShelfLevelStep-.04f);
                    if(board==null)board=HandServingInstaller.Shape(name,shelf,PrimitiveType.Cube,point,Vector3.one,material).transform;
                    Resize(board,point,new Vector3(cfg.DishwareShelfDepth,.08f,cfg.DishwareShelfWidth));
                }
                foreach(float z in new[]{-1f,1f})
                {
                    string name=z<0?"Shelf Left Upright":"Shelf Right Upright";var post=shelf.Find(name);
                    float height=cfg.DishwareShelfFirstLevel+2*cfg.DishwareShelfLevelStep+.25f;
                    var point=p+new Vector3(side*(cfg.DishwareShelfDepth*.5f-.04f),height*.5f,z*(cfg.DishwareShelfWidth*.5f-.04f));
                    if(post==null)post=HandServingInstaller.Shape(name,shelf,PrimitiveType.Cube,point,Vector3.one,material).transform;
                    Resize(post,point,new Vector3(.08f,height,.08f));
                }
                int slot=int.Parse(station.StationId.Substring(1));
                foreach(var target in b.Dishware.Supply.Where(t=>t.Team==team && t.name.StartsWith("Supply "+slot+" - ")))
                {
                    int kind=System.Array.IndexOf(ids,target.Definition.Id);int level=kind/2;
                    float z=kind==4?0:(kind%2==0?-.40f:.40f);
                    Undo.SetTransformParent(target.transform,shelf,"Move saved dishware sample");Undo.RecordObject(target.transform,"Dishware shelf level");
                    target.transform.position=p+new Vector3(0,cfg.DishwareShelfFirstLevel+level*cfg.DishwareShelfLevelStep+.025f,z);
                    target.transform.rotation=Quaternion.identity;target.transform.localScale=Vector3.one;
                    foreach(var label in target.GetComponentsInChildren<TextMesh>(true))
                    {Undo.RecordObject(label.transform,"Shelf dishware label");label.transform.position=target.transform.position+new Vector3(-side*cfg.DishwareShelfDepth*.5f,.11f,0);label.transform.rotation=Quaternion.Euler(0,-side*90,0);}
                }
            }
            Undo.RecordObject(b.Hud.TaskCard,"Dishware shelf instructions");
            b.Hud.TaskCard.text=b.Hud.TaskCard.text.Replace("длинный стол за участниками","полка рядом с каждой духовкой").Replace("по общему столу","по посуде на полке");
            EditorUtility.SetDirty(b.Hud.TaskCard);Physics.SyncTransforms();
        }
        private static void Resize(Transform item,Vector3 position,Vector3 size)
        {
            Undo.RecordObject(item,"Saved shelf geometry");item.position=position;item.rotation=Quaternion.identity;
            var scale=item.parent.lossyScale;item.localScale=new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z);item.gameObject.layer=0;
            var collision=item.GetComponent<BoxCollider>();if(collision==null)collision=Undo.AddComponent<BoxCollider>(item.gameObject);
            Undo.RecordObject(collision,"Shelf collision");collision.size=Vector3.one;collision.center=Vector3.zero;collision.enabled=true;EditorUtility.SetDirty(collision);
        }
    }
}
