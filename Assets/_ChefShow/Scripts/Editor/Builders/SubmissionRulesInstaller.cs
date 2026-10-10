using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChefShow.Editor
{
    public static class SubmissionRulesInstaller
    {
        [MenuItem("Tools/Chef Show/Install Room Space and Submission Rules")]
        public static void Install()=>HandServingInstaller.UpdateScenes(AddToScene,"submission-space");
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            Undo.RecordObject(b.Layout,"Double measured clearance");b.Layout.ArenaWidth=37.4f;b.Layout.ArenaDepth=53.1f;b.Layout.ArenaCenterZ=-3.25f;EditorUtility.SetDirty(b.Layout);
            CompactKitchenInstaller.AddToScene(scene);AddButtonsToScene(scene);
        }
        public static void AddButtonsToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var material=HandServingInstaller.Material("Submit Red",new Color(.5f,.015f,.008f));
            Undo.RecordObject(material,"Submission lamp material");material.EnableKeyword("_EMISSION");// Nonzero asset emission keeps URP from stripping _EMISSION on import; runtime overrides it to black before submission.
            material.SetColor("_EmissionColor",Color.red*.01f);material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;EditorUtility.SetDirty(material);
            foreach(var station in b.Serving.Stations)
            {
                // Legacy generated serving sockets were still at 0.53m below the 0.90m table.
                // Correct only this generated zone; keep all authored working-scene poses.
                if(scene.path==PrototypeSceneBuilder.GeneratedScene)
                {
                    var table=station.GetComponentsInParent<BoxCollider>().Single(c=>c.name=="Station_"+station.StationId);
                    float y=table.bounds.max.y+.03f;
                    if(station.transform.position.y<y-.01f){Undo.RecordObject(station.transform,"Serving zone above generated tabletop");var point=station.transform.position;point.y=y;station.transform.position=point;}
                }
                var content=station.transform.Find("Plate Contents");var submit=content.Find("Submit Dish");
                float side=station.StationId.StartsWith("A")?-1:1;Undo.RecordObject(submit,"Button to participant left");submit.position=content.position+new Vector3(0,.02f,-side*.43f);
                var renderer=submit.Find("Submit Button").GetComponent<Renderer>();Undo.RecordObject(renderer,"Red button");renderer.sharedMaterial=material;
                Undo.RecordObject(station,"Red button references");station.SubmitButton=renderer;
                if(station.SubmitLight==null){var lamp=HandServingInstaller.Node("Submission Red Light",submit,submit.position+Vector3.up*.07f);station.SubmitLight=Undo.AddComponent<Light>(lamp.gameObject);}
                Undo.RecordObject(station.SubmitLight,"Submission point light");station.SubmitLight.type=LightType.Point;station.SubmitLight.color=Color.red;station.SubmitLight.range=.6f;station.SubmitLight.intensity=2;station.SubmitLight.enabled=false;
                EditorUtility.SetDirty(station);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(station.SubmitLight);
            }
            Undo.RecordObject(b.Hud.Status.rectTransform,"Presentation penalty status");b.Hud.Status.rectTransform.sizeDelta=new Vector2(b.Hud.Status.rectTransform.sizeDelta.x,300);
            Undo.RecordObject(b.Hud.TaskCard,"Submission rules instructions");b.Hud.TaskCard.text=b.Hud.TaskCard.text.Replace("зелёной кнопке","красной кнопке").Replace("Смена тарелки сохраняет еду и дозы.","Смена установленной посуды: еда удаляется, презентабельность −1.");
            if(!b.Hud.TaskCard.text.Contains("Снимите еду"))b.Hud.TaskCard.text+="\nСнимите еду на доску/лоток перед сменой посуды. ЛКМ по тарелке — снять.\nКрасная кнопка слева: подать и заблокировать блюдо. Restart сбрасывает штраф.";
            EditorUtility.SetDirty(b.Hud.TaskCard);SubmissionResetInstaller.AddToScene(scene);Physics.SyncTransforms();
        }
    }
}
