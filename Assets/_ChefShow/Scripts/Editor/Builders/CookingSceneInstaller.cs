using System;
using System.IO;
using System.Linq;
using ChefShow.Core;
using ChefShow.Cooking;
using ChefShow.Data;
using ChefShow.Ingredients;
using ChefShow.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChefShow.Editor
{
    public static class CookingSceneInstaller
    {
        public const string ConfigPath = "Assets/_ChefShow/Generated/Data/CookingConfig.asset";
        [MenuItem("Tools/Chef Show/Install Pan and Pot Stage")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Нужен Edit Mode после компиляции.");
            var working = SceneManager.GetActiveScene();
            if (working.path != PrototypeSceneBuilder.EditableScene || working.isDirty) throw new InvalidOperationException("Сохраните рабочую сцену ChefShow_Prototype.");
            var generated = SceneManager.GetSceneByPath(PrototypeSceneBuilder.GeneratedScene);
            bool opened = !generated.IsValid() || !generated.isLoaded;
            if (!opened && generated.isDirty) throw new InvalidOperationException("Generated-сцена не сохранена.");
            if (opened) generated = EditorSceneManager.OpenScene(PrototypeSceneBuilder.GeneratedScene, OpenSceneMode.Additive);
            try
            {
                Directory.CreateDirectory("TestResults"); string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                foreach (var scene in new[] { working, generated }) File.Copy(scene.path, "TestResults/cooking-before-" + scene.name + "-" + stamp + ".unity", false);
                foreach (var scene in new[] { working, generated })
                {
                    SceneManager.SetActiveScene(scene); AddToScene(scene); PrototypeValidator.ValidateScene(scene);
                    Undo.FlushUndoRecordObjects(); EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Не удалось сохранить " + scene.path);
                }
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<CookingConfig>(ConfigPath));
            }
            finally { SceneManager.SetActiveScene(working); if (opened) EditorSceneManager.CloseScene(generated,true); }
        }
        public static void AddToScene(Scene scene)
        {
            var bootstrap = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameBootstrap>(true)).Single();
            if (bootstrap.Inventory == null || bootstrap.Preparation == null || bootstrap.Tools == null) throw new InvalidOperationException("Сначала нужен F-005.");
            var config = AssetDatabase.LoadAssetAtPath<CookingConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CookingConfig>(); config.name = "CookingConfig";
                AssetDatabase.CreateAsset(config,ConfigPath);
            }
            var error = config.Validate(); if (error != null) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(config);
            var cooking = bootstrap.Cooking;
            if (cooking == null) { cooking = Undo.AddComponent<CookingController>(bootstrap.gameObject); Undo.RecordObject(bootstrap,"Cooking reference"); bootstrap.Cooking = cooking; }
            Undo.RecordObject(cooking,"Cooking settings"); cooking.Config = config;
            var tables = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrototypeInteractable>(true))
                .Where(t => t.name.StartsWith("Station_",StringComparison.Ordinal)).OrderBy(t => t.name).ToArray();
            ArrangeRows(scene,tables,config.TableWidth);
            var stations = new System.Collections.Generic.List<CookingStation>();
            foreach (var table in tables)
            {
                string id = table.name.Substring("Station_".Length); float side = id.StartsWith("A",StringComparison.Ordinal) ? -1 : 1;
                ResizeTable(table.transform,config.TableWidth,config.TableDepth,side);
                var bounds = table.GetComponent<BoxCollider>().bounds;
                var focus=table.FocusPoint;
                if(focus!=null)
                {
                    Undo.RecordObject(focus,"Focus the wide workspace");
                    float inner=side<0?bounds.max.x:bounds.min.x;
                    // Aim through the free gap between preparation and cooking, even with a full tray.
                    focus.position=new Vector3(inner+side*.4f,bounds.max.y+.08f,bounds.center.z+side*.62f); EditorUtility.SetDirty(focus);
                }
                float rightZ = bounds.center.z + side * (bounds.extents.z - .55f);
                var parent = table.transform.Find("Cooking"); if (parent == null) parent = Node("Cooking",table.transform,table.transform.position);
                foreach (CookerKind kind in new[]{CookerKind.Pan,CookerKind.Pot})
                {
                    string name = kind == CookerKind.Pan ? "Pan" : "Pot";
                    float x = bounds.center.x + side * (kind==CookerKind.Pan ? bounds.extents.x-.55f : -bounds.extents.x+.55f);
                    var point = new Vector3(x,bounds.max.y+.06f,rightZ);
                    var root = parent.Find(name); CookingStation station;
                    if (root == null) station = BuildStation(parent,point,id,kind,side,bootstrap);
                    else
                    {
                        station = root.GetComponent<CookingStation>();
                        Undo.RecordObject(root,"Right column cooking appliances"); root.position=point; EditorUtility.SetDirty(root);
                    }
                    foreach(var target in station.GetComponentsInChildren<PrototypeInteractable>(true))
                    { Undo.RecordObject(target,"Appliance reach"); target.InteractionDistanceOverride = config.ApplianceInteractionDistance; EditorUtility.SetDirty(target); }
                    stations.Add(station);
                }
            }
            cooking.Stations = stations.ToArray();
            InstallHud(bootstrap);
            EditorUtility.SetDirty(cooking); EditorUtility.SetDirty(bootstrap);
            HandServingInstaller.AddToScene(scene); MixingOvenInstaller.AddToScene(scene);
            DishwareInstaller.AddToScene(scene);
            Physics.SyncTransforms(); error=cooking.Validate(bootstrap.Inventory); if(error!=null) throw new InvalidOperationException(error);
        }
        public static void InstallHud(GameBootstrap bootstrap)
        {
            HandServingInstaller.UpdateHud(bootstrap);
        }

        private static void ArrangeRows(Scene scene,PrototypeInteractable[] tables,float width)
        {
            // Keep station 1 and each actor's offset; retain the existing 20 cm gaps.
            var actors=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ChefShow.Contestants.PrototypeActor>(true)).ToArray();
            foreach(string team in new[]{"A","B"})
            {
                var row=tables.Where(t=>t.name.StartsWith("Station_"+team,StringComparison.Ordinal)).OrderBy(t=>t.name).ToArray();
                if(row.Length!=6) throw new InvalidOperationException("Нужны шесть станций команды "+team);
                float first=row[0].transform.position.z;
                for(int i=0;i<row.Length;i++)
                {
                    var table=row[i].transform; float delta=first+i*(width+.2f)-table.position.z;
                    if(Mathf.Abs(delta)<.0001f) continue;
                    Undo.RecordObject(table,"Wider station rows"); table.position+=Vector3.forward*delta; EditorUtility.SetDirty(table);
                    foreach(var actor in actors.Where(a=>a.StableId==table.name.Substring("Station_".Length)))
                    { Undo.RecordObject(actor.transform,"Keep participant at own station"); actor.transform.position+=Vector3.forward*delta; EditorUtility.SetDirty(actor.transform); }
                }
            }
            Physics.SyncTransforms();
        }
        private static void ResizeTable(Transform table,float width,float depth,float side)
        {
            // The original two columns move left to make room for cooking on the right.
            // Stored world scales/rotations and relative workspace arrangement remain intact.
            var children=table.Cast<Transform>().ToArray(); var positions=children.Select(t=>t.position).ToArray();
            var rotations=children.Select(t=>t.rotation).ToArray(); var scales=children.Select(t=>t.lossyScale).ToArray();
            var bounds=table.GetComponent<BoxCollider>().bounds;
            float edge=side<0?bounds.min.x:bounds.max.x;
            float workspaceShift=-side*(width-bounds.size.z)/2;
            Undo.RecordObject(table,"Rectangular cooking station");
            table.position=new Vector3(edge-side*depth/2,table.position.y,table.position.z);
            table.localScale=new Vector3(depth/table.parent.lossyScale.x,table.localScale.y,width/table.parent.lossyScale.z);
            for(int i=0;i<children.Length;i++)
            {
                Undo.RecordObject(children[i],"Preserve existing station workspace");
                var shift=children[i].name=="Cooking"||children[i].name=="Tool Drawer Contents" ? Vector3.zero : Vector3.forward*workspaceShift;
                children[i].SetPositionAndRotation(positions[i]+shift,rotations[i]);
                var parentScale=children[i].parent.lossyScale;
                children[i].localScale=new Vector3(scales[i].x/parentScale.x,scales[i].y/parentScale.y,scales[i].z/parentScale.z);
                EditorUtility.SetDirty(children[i]);
            }
            EditorUtility.SetDirty(table); Physics.SyncTransforms();
        }
        private static CookingStation BuildStation(Transform parent,Vector3 position,string id,CookerKind kind,float side,GameBootstrap bootstrap)
        {
            var root=Node(kind==CookerKind.Pan?"Pan":"Pot",parent,position);
            var station=Undo.AddComponent<CookingStation>(root.gameObject); station.StationId=id; station.Kind=kind;
            var target=Target(root,station,CookingTargetKind.Vessel,0,new Vector3(.86f,.06f,.94f));
            Shape("Pad",root,PrimitiveType.Cube,position,new Vector3(.86f,.06f,.94f),Material("Cooking Pad",new Color(.23f,.29f,.32f)));
            var metal=Material("Cooking Metal",new Color(.27f,.28f,.30f));
            station.HeatIndicator=Shape("Heat Indicator",root,PrimitiveType.Cylinder,position+Vector3.up*.04f,new Vector3(.76f,.01f,.72f),metal).GetComponent<Renderer>();
            Shape("Bottom",root,PrimitiveType.Cylinder,position+Vector3.up*.09f,new Vector3(.64f,.022f,.64f),metal);
            if(kind==CookerKind.Pot)
            {
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    var wall=Shape("Pot Wall "+(i+1),root,PrimitiveType.Cube,position+new Vector3(Mathf.Cos(a)*.29f,.18f,Mathf.Sin(a)*.29f),new Vector3(.06f,.19f,.17f),metal);
                    wall.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);
                }
                Shape("Water",root,PrimitiveType.Cylinder,position+Vector3.up*.19f,new Vector3(.51f,.006f,.51f),Material("Pot Water",new Color(.25f,.55f,.61f)));
            }
            else Shape("Pan Handle",root,PrimitiveType.Cube,position+new Vector3(side*.30f,.12f,0),new Vector3(.18f,.055f,.08f),metal);
            float foodY=kind==CookerKind.Pan?.17f:.26f;
            station.StirPoint=Node("Stir Contact Point",root,position+Vector3.up*foodY);
            station.Food=new FoodDisplay[3];
            for(int i=0;i<3;i++)
            {
                var p=position+new Vector3(0,foodY,(i-1)*.16f);
                var item=Node("Food Slot_"+(i+1),root,p); Target(item,station,CookingTargetKind.Food,i,new Vector3(.2f,.11f,.14f));
                var view=Undo.AddComponent<FoodDisplay>(item.gameObject); view.SizeMultiplier=.6f;
                var mesh=Node("Mesh",item,p).gameObject; view.Mesh=Undo.AddComponent<MeshFilter>(mesh); view.Visual=Undo.AddComponent<MeshRenderer>(mesh);
                view.Mesh.sharedMesh=bootstrap.Inventory.Catalog.Ingredients[0].VisualMesh;
                view.Visual.sharedMaterial=bootstrap.Inventory.Catalog.Ingredients[0].VisualMaterial; view.Visual.enabled=false;
                view.CutPieces=Enumerable.Range(0,7).Select(n=>
                {
                    var piece=Shape("Cut Piece "+(n+1),item,PrimitiveType.Cube,p,new Vector3(.02f,.03f,.03f),view.Visual.sharedMaterial);
                    var r=piece.GetComponent<Renderer>(); r.enabled=false; return r;
                }).ToArray(); station.Food[i]=view; item.GetComponent<BoxCollider>().enabled=false; EditorUtility.SetDirty(view);
            }
            var knob=Node("Heat Knob",root,position+new Vector3(side*.32f,.10f,-.32f)); Target(knob,station,CookingTargetKind.HeatKnob,0,new Vector3(.19f,.11f,.19f));
            Shape("Knob Mesh",knob,PrimitiveType.Cylinder,knob.position,new Vector3(.16f,.05f,.16f),Material("Cooking Knob",new Color(.88f,.64f,.23f)));
            Label(kind==CookerKind.Pan?"СКОВОРОДКА":"КАСТРЮЛЯ",root,position+new Vector3(side*.36f,.06f,0),side,.009f);
            station.Status=Label("Выкл.",knob,knob.position+Vector3.up*.07f,side,.006f);
            var smoke=Material("Cooking Smoke",new Color(.38f,.38f,.38f));
            station.Smoke=Enumerable.Range(0,3).Select(i=>
            {
                var puff=Shape("Smoke "+(i+1),root,PrimitiveType.Sphere,position+new Vector3((i-1)*.06f,.6f+i*.12f,0),Vector3.one*(.12f+i*.05f),smoke);
                var r=puff.GetComponent<Renderer>(); r.enabled=false; return r;
            }).ToArray();
            EditorUtility.SetDirty(station); return station;
        }
        private static CookingTarget Target(Transform node,CookingStation station,CookingTargetKind kind,int index,Vector3 size)
        {
            var proto=Undo.AddComponent<PrototypeInteractable>(node.gameObject); proto.DisplayName=CookingController.CookerName(station.Kind);
            var collider=Undo.AddComponent<BoxCollider>(node.gameObject); collider.size=size;
            var target=Undo.AddComponent<CookingTarget>(node.gameObject); target.Station=station; target.Kind=kind; target.Index=index; return target;
        }
        private static Transform Node(string name,Transform parent,Vector3 point)
        {
            var node=new GameObject(name); Undo.RegisterCreatedObjectUndo(node,"Saved cooking object"); node.transform.SetParent(parent,false);
            node.transform.position=point; var scale=parent.lossyScale;
            node.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z); return node.transform;
        }
        private static GameObject Shape(string name,Transform parent,PrimitiveType type,Vector3 point,Vector3 size,Material material)
        {
            var shape=GameObject.CreatePrimitive(type); shape.name=name; Undo.RegisterCreatedObjectUndo(shape,"Saved cooking mesh");
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>()); shape.layer=LayerMask.NameToLayer("Ignore Raycast");
            shape.transform.SetParent(parent,false); shape.transform.position=point; var scale=parent.lossyScale;
            shape.transform.localScale=new Vector3(size.x/scale.x,size.y/scale.y,size.z/scale.z); shape.GetComponent<Renderer>().sharedMaterial=material; return shape;
        }
        private static TextMesh Label(string text,Transform parent,Vector3 point,float side,float size)
        {
            var node=Node("Label",parent,point); node.rotation=Quaternion.Euler(0,-side*90,0);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); var label=Undo.AddComponent<TextMesh>(node.gameObject);
            label.text=text; label.font=font; label.fontSize=48; label.characterSize=size; label.anchor=TextAnchor.MiddleCenter;
            node.GetComponent<MeshRenderer>().sharedMaterial=font.material; node.gameObject.layer=LayerMask.NameToLayer("Ignore Raycast"); return label;
        }
        private static Material Material(string name,Color color)
        {
            string path="Assets/_ChefShow/Generated/Materials/"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null) return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color}; AssetDatabase.CreateAsset(material,path); return material;
        }
    }
}
