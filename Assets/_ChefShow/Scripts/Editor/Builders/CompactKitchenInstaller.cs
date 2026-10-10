using System.Linq;
using ChefShow.Core;
using ChefShow.Cooking;
using ChefShow.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChefShow.Editor
{
    public static class CompactKitchenInstaller
    {
        public static void AddToScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects();var b=roots.SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            const string path="Assets/_ChefShow/Generated/Data/KitchenLayoutConfig.asset";
            var cfg=AssetDatabase.LoadAssetAtPath<KitchenLayoutConfig>(path);
            if(cfg==null){cfg=ScriptableObject.CreateInstance<KitchenLayoutConfig>();AssetDatabase.CreateAsset(cfg,path);}
            Undo.RecordObject(b,"Kitchen layout references");b.Layout=cfg;
            var arena=roots.Single(r=>r.name=="Arena").transform;
            foreach(Transform item in arena)
            {
                if(item.name=="Floor")Scale(item,new Vector3(cfg.ArenaWidth,.3f,cfg.ArenaDepth));
                else if(item.name=="Side Wall")
                {Undo.RecordObject(item,"Move side walls");item.position=new Vector3(Mathf.Sign(item.position.x)*cfg.ArenaWidth/2,item.position.y,0);Scale(item,new Vector3(.3f,5,cfg.ArenaDepth));}
                else if(item.name=="Back Wall" || item.name=="Front Wall")
                {Undo.RecordObject(item,"Move end walls");item.position=new Vector3(0,item.position.y,(item.name=="Back Wall"?1:-1)*cfg.ArenaDepth/2);Scale(item,new Vector3(cfg.ArenaWidth,5,.3f));}
            }
            Undo.RecordObject(b.Config,"Compact arena dimensions");b.Config.ArenaWidth=cfg.ArenaWidth;b.Config.ArenaDepth=cfg.ArenaDepth;EditorUtility.SetDirty(b.Config);
            foreach(var oven in b.Cooking.Stations.Where(c=>c.Kind==CookerKind.Oven))
            {
                Undo.RecordObject(oven.transform,"Enlarge and move oven");float side=oven.StationId.StartsWith("A")?-1:1;
                oven.transform.position=new Vector3(side*(cfg.ArenaWidth/2-.15f-cfg.OvenWallClearance-.37f*cfg.OvenScale),0,oven.transform.position.z);
                Scale(oven.transform,Vector3.one*cfg.OvenScale);
                // The appliance grows, but a logical food portion keeps its original display size.
                Undo.RecordObject(oven.Food[0],"Food scale in larger oven");oven.Food[0].SizeMultiplier=1/cfg.OvenScale;EditorUtility.SetDirty(oven.Food[0]);
            }
            EditorUtility.SetDirty(b);Physics.SyncTransforms();
        }
        private static void Scale(Transform t,Vector3 desired)
        {Undo.RecordObject(t,"Kitchen world dimensions");var s=t.parent==null?Vector3.one:t.parent.lossyScale;t.localScale=new Vector3(desired.x/s.x,desired.y/s.y,desired.z/s.z);}
    }
}
