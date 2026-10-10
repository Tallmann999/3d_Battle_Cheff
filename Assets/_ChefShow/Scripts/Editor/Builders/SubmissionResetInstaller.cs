using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChefShow.Editor
{
    public static class SubmissionResetInstaller
    {
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            foreach(var station in b.Serving.Stations)
            {
                var content=station.transform.Find("Plate Contents");var button=content.Find("Reset Submission");
                float side=station.StationId.StartsWith("A")?-1:1;
                var position=content.position+new Vector3(-side*.24f,.02f,-side*.43f);
                if(button==null)
                {
                    button=HandServingInstaller.Node("Reset Submission",content,position);
                    var target=Undo.AddComponent<ServingTarget>(button.gameObject);target.Station=station;target.ResetSubmission=true;
                    var interact=Undo.AddComponent<PrototypeInteractable>(button.gameObject);interact.DisplayName="Отменить подачу (тест)";
                    var col=Undo.AddComponent<BoxCollider>(button.gameObject);col.size=new Vector3(.15f,.07f,.23f);
                    HandServingInstaller.Shape("Blue Test Button",button,PrimitiveType.Cube,position,new Vector3(.15f,.035f,.23f),HandServingInstaller.Material("Submission Reset Blue",new Color(.025f,.25f,1)));
                    HandServingInstaller.Label("ОТМЕНА\nТЕСТ",button,position+Vector3.up*.05f,side,.0035f);
                }
                else {Undo.RecordObject(button,"Reset button position");button.position=position;}
            }
            Undo.RecordObject(b.Hud.TaskCard,"Test submission instructions");
            if(!b.Hud.TaskCard.text.Contains("Синяя кнопка"))b.Hud.TaskCard.text+="\nСиняя кнопка (тест): отменить подачу до 00:00; еда и штраф сохраняются.";
            EditorUtility.SetDirty(b.Hud.TaskCard);Physics.SyncTransforms();
        }
    }
}
