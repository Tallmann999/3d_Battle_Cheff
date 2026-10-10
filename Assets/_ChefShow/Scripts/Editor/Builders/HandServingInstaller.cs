using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using ChefShow.Player;
using ChefShow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class HandServingInstaller
    {
        [MenuItem("Tools/Chef Show/Install Mouse Hands and Plates")]
        public static void Install() => UpdateScenes(AddToScene,"hands-plates");
        public static void UpdateScenes(Action<Scene> action,string tag)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || (Selection.activeObject!=null && (Selection.activeGameObject==null || Selection.activeGameObject.transform.root.name!="Lighting")))
                throw new InvalidOperationException("Нужна сохранённая сцена в Edit Mode без активного выделения.");
            var working=SceneManager.GetActiveScene();
            if(working.path!=PrototypeSceneBuilder.EditableScene || working.isDirty)throw new InvalidOperationException("Сохраните рабочую сцену.");
            var generated=SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened=!generated.IsValid() || !generated.isLoaded;
            if(!opened && generated.isDirty)throw new InvalidOperationException("Generated не сохранена.");
            if(opened)generated=EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene,OpenSceneMode.Additive);
            try
            {
                Directory.CreateDirectory("TestResults");string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach(var scene in new[]{working,generated}) File.Copy(scene.path,"TestResults/"+tag+"-before-"+scene.name+"-"+stamp+".unity");
                foreach(var scene in new[]{working,generated})
                {
                    SceneManager.SetActiveScene(scene); action(scene); PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene))throw new IOException("Сохранение не удалось: "+scene.path);
                }
                AssetDatabase.SaveAssets();
            }
            finally{SceneManager.SetActiveScene(working);if(opened)EditorSceneManager.CloseScene(generated,true);}
        }
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            Undo.RecordObject(b.Player,"Reach drawer under the camera");b.Player.MaxDownPitch=89;EditorUtility.SetDirty(b.Player);
            Undo.RecordObject(b.Inventory.HeldDisplay.transform,"Food in left hand");
            b.Inventory.HeldDisplay.transform.SetParent(b.Tools.LeftFoodHand,false);b.Inventory.HeldDisplay.transform.localPosition=Vector3.zero;b.Inventory.HeldDisplay.transform.localRotation=Quaternion.identity;
            var controller=b.Serving??Undo.AddComponent<ServingController>(b.gameObject);
            Undo.RecordObject(b,"Serving controller");b.Serving=controller;Undo.RecordObject(controller,"Serving stations");
            controller.Stations=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<InventoryInteractable>(true))
                .Where(t=>t.Kind==InventoryTargetKind.Socket && t.Index==1).OrderBy(t=>t.StationId).Select(t=>BuildPlate(t,b)).ToArray();
            if(b.Hud.HandIcon==null)
            {
                var node=new GameObject("Interaction Hand",typeof(RectTransform),typeof(CanvasRenderer));Undo.RegisterCreatedObjectUndo(node,"Hand prompt");
                node.transform.SetParent(b.Hud.Context.transform.parent,false);b.Hud.HandIcon=Undo.AddComponent<HandPromptGraphic>(node);
                var rect=b.Hud.HandIcon.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-40,42);rect.sizeDelta=new Vector2(32,36);
                b.Hud.HandIcon.color=new Color(1,.88f,.32f);b.Hud.HandIcon.raycastTarget=false;
            }
            Undo.RecordObject(b.Hud,"Mouse prompt");Undo.RecordObject(b.Hud.InteractionKey,"Mouse label");
            b.Hud.InteractionKey.name="Interaction Button";b.Hud.InteractionKey.text="ЛКМ";b.Hud.InteractionKey.rectTransform.sizeDelta=new Vector2(60,26);
            UpdateHud(b);EditorUtility.SetDirty(b.Hud);EditorUtility.SetDirty(controller);EditorUtility.SetDirty(b);
        }
        private static ServingStation BuildPlate(InventoryInteractable target,GameBootstrap b)
        {
            var root=target.transform;var station=root.GetComponent<ServingStation>();if(station!=null)return station;
            station=Undo.AddComponent<ServingStation>(root.gameObject);station.StationId=target.StationId;
            var st=Undo.AddComponent<ServingTarget>(root.gameObject);st.Station=station;st.Index=-1;
            var p=target.GetComponent<BoxCollider>().bounds.center; p.y=target.GetComponent<BoxCollider>().bounds.max.y+.015f;
            var parent=Node("Plate Contents",root,p);
            Shape("Plate",parent,PrimitiveType.Sphere,p,new Vector3(.58f,.035f,.56f),Material("Serving Plate",new Color(.86f,.89f,.95f)));
            station.Food=new FoodDisplay[48];
            for(int i=0;i<station.Food.Length;i++)
            {
                int layer=i/6;float angle=(i%6)*Mathf.PI/3+layer*.45f;float radius=layer==0?.17f:.13f;
                var point=p+new Vector3(Mathf.Cos(angle)*radius,.055f+layer*.065f,Mathf.Sin(angle)*radius);
                var item=Node("Plated Portion_"+(i+1),parent,point);
                var proto=Undo.AddComponent<PrototypeInteractable>(item.gameObject);proto.DisplayName="Еда на тарелке";
                var cell=Undo.AddComponent<ServingTarget>(item.gameObject);cell.Station=station;cell.Index=i;
                var collider=Undo.AddComponent<BoxCollider>(item.gameObject);collider.size=Vector3.one*.1f;collider.enabled=false;
                station.Food[i]=Food(item,b,.6f);
            }
            float side=target.StationId.StartsWith("A")?-1:1;
            station.Status=Label("ТАРЕЛКА",parent,p+new Vector3(0,.03f,-side*.31f),side,.0045f);
            var submit=Node("Submit Dish",parent,p+new Vector3(side*.37f,.02f,0));
            var t=Undo.AddComponent<ServingTarget>(submit.gameObject);t.Station=station;t.Submit=true;
            var interact=Undo.AddComponent<PrototypeInteractable>(submit.gameObject);interact.DisplayName="Подать блюдо";
            var col=Undo.AddComponent<BoxCollider>(submit.gameObject);col.size=new Vector3(.15f,.07f,.23f);
            Shape("Submit Button",submit,PrimitiveType.Cube,submit.position,new Vector3(.15f,.035f,.23f),Material("Submit Button",new Color(.3f,.68f,.42f)));
            Label("ПОДАТЬ",submit,submit.position+Vector3.up*.04f,side,.0038f);
            EditorUtility.SetDirty(station);return station;
        }
        public static void UpdateHud(GameBootstrap b)
        {
            Undo.RecordObject(b.Hud.Status.rectTransform,"Kitchen status height");
            b.Hud.Status.rectTransform.sizeDelta=new Vector2(b.Hud.Status.rectTransform.sizeDelta.x,220);
            foreach(var text in b.Hud.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Controls"))
            {Undo.RecordObject(text,"Mouse hand controls");text.text="ЛКМ — левая рука: продукты / ящик / приправы\nПКМ — правая рука: инструменты / 6 нарезок / мешать\nTab — корзина · G — уронить инструмент / корзину\nF — фокус / выход · Q — задание · Esc — пауза";EditorUtility.SetDirty(text);}
            Undo.RecordObject(b.Hud.TaskCard,"Mouse task");
            b.Hud.TaskCard.text="ГОТОВКА ДВУМЯ РУКАМИ\nTab — взять / поставить корзину. ЛКМ — набрать / выгрузить продукты.\nЛКМ — взять продукт слева / положить. ЛКМ — открыть ящик.\nПКМ — взять нож справа / вернуть в его ячейку.\nНа доске: 6 отдельных ПКМ ножом; упаковка в лотке: ПКМ открыть.\nЛКМ по ручке — нагрев; ЛКМ — положить / снять еду.\nПКМ лопаткой — мешать. Средний огонь: готово 30 с, горит 60 с.\nЛКМ солью / маслом — доза на еду, доску или собранное блюдо.\nТарелка принимает много продуктов и повторные порции.\nЛКМ по зелёной кнопке — подать; на 00:00 подача автоматическая.";
            EditorUtility.SetDirty(b.Hud.TaskCard);
        }
        public static Transform Node(string name,Transform parent,Vector3 p)
        {
            var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Saved kitchen object");go.transform.SetParent(parent,false);go.transform.position=p;
            var s=parent.lossyScale;go.transform.localScale=new Vector3(1/s.x,1/s.y,1/s.z);return go.transform;
        }
        public static GameObject Shape(string name,Transform parent,PrimitiveType type,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;Undo.RegisterCreatedObjectUndo(go,"Saved kitchen shape");UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer=LayerMask.NameToLayer("Ignore Raycast");go.transform.SetParent(parent,false);go.transform.position=p;var s=parent.lossyScale;
            go.transform.localScale=new Vector3(size.x/s.x,size.y/s.y,size.z/s.z);go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        public static Material Material(string name,Color color)
        {
            var path="Assets/_ChefShow/Generated/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat!=null)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(mat,path);return mat;
        }
        public static FoodDisplay Food(Transform item,GameBootstrap b,float scale)
        {
            var view=Undo.AddComponent<FoodDisplay>(item.gameObject);view.SizeMultiplier=scale;
            var mesh=Node("Mesh",item,item.position);view.Mesh=Undo.AddComponent<MeshFilter>(mesh.gameObject);view.Visual=Undo.AddComponent<MeshRenderer>(mesh.gameObject);
            var def=b.Inventory.Catalog.Ingredients[0];view.Mesh.sharedMesh=def.VisualMesh;view.Visual.sharedMaterial=def.VisualMaterial;view.Visual.enabled=false;
            view.CutPieces=Enumerable.Range(0,7).Select(i=>{var r=Shape("Cut Piece "+(i+1),item,PrimitiveType.Cube,item.position,Vector3.one*.02f,def.VisualMaterial).GetComponent<Renderer>();r.enabled=false;return r;}).ToArray();EditorUtility.SetDirty(view);return view;
        }
        public static TextMesh Label(string text,Transform parent,Vector3 p,float side,float size)
        {
            var node=Node("Label",parent,p);node.rotation=Quaternion.Euler(0,-side*90,0);node.gameObject.layer=LayerMask.NameToLayer("Ignore Raycast");
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");var label=Undo.AddComponent<TextMesh>(node.gameObject);label.text=text;label.font=font;label.fontSize=48;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;node.GetComponent<MeshRenderer>().sharedMaterial=font.material;return label;
        }
    }
}
