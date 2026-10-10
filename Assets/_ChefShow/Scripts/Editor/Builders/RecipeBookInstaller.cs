using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace ChefShow.Editor
{
    public static class RecipeBookInstaller
    {
        private static readonly Color Ink=new Color(.22f,.17f,.12f);
        [MenuItem("Tools/Chef Show/Install Recipe Book")]
        public static void Install()=>HandServingInstaller.UpdateScenes(AddToScene,"recipe-book");
        public static void AddToScene(Scene scene)
        {
            var b=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<GameBootstrap>(true)).Single();
            var catalog=CreateCatalog(b);
            var input=b.InputDefinition;var ui=input.FindActionMap("UI",true);
            bool changed=false;
            if(ui.FindAction("RecipeBook")==null){ui.AddAction("RecipeBook",InputActionType.Button,"<Keyboard>/i");changed=true;}
            if(ui.FindAction("RecipeRightHeld")==null){ui.AddAction("RecipeRightHeld",InputActionType.Button,"<Mouse>/rightButton");changed=true;}
            if(changed){System.IO.File.WriteAllText(AssetDatabase.GetAssetPath(input),input.ToJson());AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(input));}
            var existing=b.Hud.transform.Find("Recipe Book");
            if(existing!=null)
            {
                if(b.RecipeBook==null)throw new InvalidOperationException("Recipe Book exists without bootstrap reference.");
                return;
            }
            var root=Rect("Recipe Book",b.Hud.transform,Vector2.zero,new Vector2(1280,720));
            Stretch(root);
            var book=Undo.AddComponent<RecipeBookController>(root.gameObject);book.Catalog=catalog;
            book.BookPanel=Image("Book Modal",root,Vector2.zero,new Vector2(1280,720),new Color(.04f,.04f,.05f,.73f),true).gameObject;
            Stretch(book.BookPanel.GetComponent<RectTransform>());
            var modal=book.BookPanel.transform;
            Image("Book Shadow",modal,new Vector2(7,-10),new Vector2(865,562),new Color(0,0,0,.45f));
            Image("Leather Cover",modal,Vector2.zero,new Vector2(852,548),new Color(.30f,.18f,.10f));
            Image("Cover Inlay",modal,Vector2.zero,new Vector2(840,536),new Color(.69f,.48f,.22f));
            Image("Page Edges",modal,new Vector2(0,-4),new Vector2(826,524),new Color(.85f,.79f,.65f));
            Image("Left Page",modal,new Vector2(-204,1),new Vector2(405,512),new Color(1,.966f,.86f));
            Image("Right Page",modal,new Vector2(204,1),new Vector2(405,512),new Color(.99f,.95f,.83f));
            Image("Spine Shade Left",modal,new Vector2(-5,1),new Vector2(8,508),new Color(.65f,.51f,.33f,.23f));
            Image("Spine",modal,new Vector2(0,1),new Vector2(2,509),new Color(.64f,.48f,.30f,.40f));
            Image("Spine Shade Right",modal,new Vector2(5,1),new Vector2(8,508),new Color(.65f,.51f,.33f,.15f));
            Text("Book Label",modal,new Vector2(0,239),new Vector2(660,20),12,"К Н И Г А  П О В А Р А",b);
            book.Title=Text("Recipe Title",modal,new Vector2(0,204),new Vector2(734,52),26,catalog.Recipes[0].DisplayName,b);book.Title.resizeTextForBestFit=true;book.Title.resizeTextMinSize=20;book.Title.resizeTextMaxSize=26;
            Image("Title Rule",modal,new Vector2(0,173),new Vector2(740,1),new Color(.62f,.43f,.22f,.5f));
            Text("Ingredients Heading",modal,new Vector2(-204,150),new Vector2(355,24),15,"ПРОДУКТЫ",b);
            Text("Process Heading",modal,new Vector2(204,150),new Vector2(355,24),15,"ПРИГОТОВЛЕНИЕ",b);
            book.IngredientIcons=new RecipeIconGraphic[7];book.IngredientCaptions=new Text[7];
            for(int i=0;i<7;i++)
            {
                var position=new Vector2(-307+(i%3)*102,73-(i/3)*114);
                var cell=Rect("Ingredient "+(i+1),modal,position,new Vector2(100,108));
                book.IngredientIcons[i]=Icon("Drawing",cell,new Vector2(0,10),new Vector2(65,65),RecipeSymbol.Potato);
                book.IngredientCaptions[i]=Text("Amount",cell,new Vector2(0,-45),new Vector2(100,60),13,"",b);
            }
            Icon("Ingredients to Process",modal,new Vector2(-24,38),new Vector2(43,43),RecipeSymbol.Arrow).color=Ink;
            book.StepIcons=new RecipeIconGraphic[6];book.StepCaptions=new Text[6];
            var points=new[]{new Vector2(67,77),new Vector2(190,77),new Vector2(313,77),new Vector2(313,-61),new Vector2(190,-61),new Vector2(67,-61)};
            for(int i=0;i<6;i++)
            {
                var cell=Rect("Step "+(i+1),modal,points[i],new Vector2(108,114));
                book.StepIcons[i]=Icon("Process Drawing",cell,new Vector2(0,8),new Vector2(80,80),RecipeSymbol.Pan);
                book.StepCaptions[i]=Text("Grouping",cell,new Vector2(0,-47),new Vector2(111,36),12,"",b);
            }
            var arrows=new[]{new Vector2(128,88),new Vector2(251,88),new Vector2(313,3),new Vector2(251,-51),new Vector2(128,-51)};
            book.StepArrows=new RecipeIconGraphic[5];
            for(int i=0;i<5;i++)
            {
                var arrow=Icon("Step Arrow "+(i+1),modal,arrows[i],new Vector2(33,33),RecipeSymbol.Arrow);arrow.color=Ink;
                if(i==2)arrow.rectTransform.localRotation=Quaternion.Euler(0,0,-90);if(i>2)arrow.LeftArrow=true;book.StepArrows[i]=arrow;
            }
            book.ReferenceNote=Text("Reference Note",modal,new Vector2(0,-143),new Vector2(730,40),12,"Количества — стартовое предложение.",b);
            book.Choose=Button("Choose Recipe",modal,new Vector2(0,-193),new Vector2(176,44),new Color(1,.81f,.08f));
            Text("Choose Label",book.Choose.transform,Vector2.zero,new Vector2(170,42),20,"Выбрать",b);
            book.Previous=Button("Previous Page",modal,new Vector2(-330,-217),new Vector2(62,40),new Color(.25f,1,.06f));
            var previous=Icon("Previous Arrow",book.Previous.transform,Vector2.zero,new Vector2(36,31),RecipeSymbol.Arrow);previous.LeftArrow=true;previous.color=Ink;
            book.Next=Button("Next Page",modal,new Vector2(330,-217),new Vector2(62,40),new Color(.25f,1,.06f));
            Icon("Next Arrow",book.Next.transform,Vector2.zero,new Vector2(36,31),RecipeSymbol.Arrow).color=Ink;
            book.PageNumber=Text("Page Number",modal,new Vector2(0,-235),new Vector2(130,22),14,"01 / 12",b);
            book.Close=Button("Close Book",modal,new Vector2(369,238),new Vector2(32,25),new Color(.88f,.81f,.66f));
            Text("Close Label",book.Close.transform,Vector2.zero,new Vector2(30,24),19,"×",b);
            Text("Close Hint",modal,new Vector2(281,239),new Vector2(118,22),12,"I — закрыть",b);
            var list=Image("Selected Ingredients",root,Vector2.zero,new Vector2(218,302),Color.white);
            var listRect=list.rectTransform;listRect.anchorMin=listRect.anchorMax=listRect.pivot=Vector2.one;listRect.anchoredPosition=new Vector2(-12,-16);
            var border=Undo.AddComponent<Outline>(list.gameObject);border.effectColor=Ink;border.effectDistance=new Vector2(2,-2);
            book.IngredientList=list.gameObject;
            Text("Shopping Label",list.transform,new Vector2(-109,-17),new Vector2(194,24),13,"СПИСОК ПРОДУКТОВ",b).rectTransform.anchorMin=Vector2.one;
            // All list children use top-right origin so they remain in the white frame.
            foreach(Transform child in list.transform){var r=child.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=Vector2.one;}
            book.SelectedTitle=Text("Selected Recipe",list.transform,new Vector2(-109,-51),new Vector2(192,48),16,"",b);book.SelectedTitle.rectTransform.anchorMin=book.SelectedTitle.rectTransform.anchorMax=Vector2.one;
            book.SelectedTitle.resizeTextForBestFit=true;book.SelectedTitle.resizeTextMinSize=13;book.SelectedTitle.resizeTextMaxSize=16;
            book.SelectedIngredients=Text("Selected Ingredient Lines",list.transform,new Vector2(-109,-178),new Vector2(190,194),14,"",b);book.SelectedIngredients.alignment=TextAnchor.UpperLeft;book.SelectedIngredients.rectTransform.anchorMin=book.SelectedIngredients.rectTransform.anchorMax=Vector2.one;
            var first=catalog.Recipes[0].Capture();book.PreviewPage(first);
            book.BookPanel.SetActive(false);book.IngredientList.SetActive(false);
            Undo.RecordObject(b,"Recipe book reference");b.RecipeBook=book;EditorUtility.SetDirty(b);EditorUtility.SetDirty(book);
            foreach(var controls in b.Hud.GetComponentsInChildren<Text>(true).Where(t=>t.name=="Controls"))
            {Undo.RecordObject(controls,"Recipe key hint");if(!controls.text.Contains("I —"))controls.text+=" · I — рецепты";EditorUtility.SetDirty(controls);}
        }
        private static RecipeBookCatalog CreateCatalog(GameBootstrap b)
        {
            const string folder="Assets/_ChefShow/Data/Recipes";
            if(!AssetDatabase.IsValidFolder("Assets/_ChefShow/Data"))AssetDatabase.CreateFolder("Assets/_ChefShow","Data");
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_ChefShow/Data","Recipes");
            RecipeDefinition Make(string id,string title,string ingredients,RecipeStep[] steps,string note="")
            {
                string path=folder+"/"+id+".asset";var d=AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);if(d!=null)return d;
                d=ScriptableObject.CreateInstance<RecipeDefinition>();d.Id=id;d.DisplayName=title;d.QuantitiesProposed=true;d.ProcessNote=note;d.Steps=steps;
                d.Ingredients=ingredients.Split(',').Select(x=>{var pair=x.Split(':');return new RecipeIngredient{Ingredient=b.Inventory.Catalog.Ingredients.Single(k=>k.Id==pair[0]),Amount=int.Parse(pair[1])};}).ToArray();
                if(d.Validate()!=null)throw new InvalidOperationException(d.Validate());AssetDatabase.CreateAsset(d,path);return d;
            }
            RecipeStep S(RecipeSymbol icon,string caption="",bool future=false)=>new RecipeStep{Symbol=icon,Caption=caption,FutureProcess=future};
            var recipes=new[]{
                Make("fried_potatoes","Жареная картошка с луком","potato:2,onion:1,oil:1,salt:1",new[]{S(RecipeSymbol.Knife,"Картофель • лук"),S(RecipeSymbol.Pan)}),
                Make("boiled_potatoes","Варёная картошка со сливочным маслом","potato:2,butter:1,salt:1",new[]{S(RecipeSymbol.Pot,"Картофель"),S(RecipeSymbol.Butter,"После нагрева")}),
                Make("fried_egg","Глазунья","egg:1,oil:1,salt:1",new[]{S(RecipeSymbol.Pan,"Целое яйцо")}),
                Make("cheese_omelet","Омлет с сыром","egg:2,cheese:1,oil:1,salt:1",new[]{S(RecipeSymbol.Mix,"Яйца • сыр"),S(RecipeSymbol.Pan)}),
                Make("scrambled_eggs","Скрэмбл","egg:2,butter:1,salt:1",new[]{S(RecipeSymbol.Mix,"Яйца"),S(RecipeSymbol.Pan,"Со сливочным маслом"),S(RecipeSymbol.Mix,"Мешать при нагреве",true)},"Перемешивание на сковороде пока недоступно."),
                Make("potato_pancakes","Картофельные оладьи","potato:2,egg:1,flour:1,oil:1,salt:1",new[]{S(RecipeSymbol.Knife,"Картофель"),S(RecipeSymbol.Mix,"Картофель • яйцо • мука"),S(RecipeSymbol.Pan)}),
                Make("steak_vegetables","Стейк с овощами","beef:1,potato:1,carrot:1,oil:1,salt:1",new[]{S(RecipeSymbol.Knife,"Только овощи"),S(RecipeSymbol.Pan,"Говядина • морковь"),S(RecipeSymbol.Pot,"Отдельно картофель")},"Главное и гарнир готовьте отдельно."),
                Make("buttered_carrots","Морковь со сливочным маслом","carrot:2,butter:1,salt:1",new[]{S(RecipeSymbol.Pot,"Морковь"),S(RecipeSymbol.Butter,"После нагрева")}),
                Make("caramel_apples","Карамелизированные яблоки","apple:1,sugar:1,butter:1",new[]{S(RecipeSymbol.Knife,"Яблоко"),S(RecipeSymbol.Pan,"Сахар • сливочное масло")}),
                Make("apple_tart","Яблочный тарт","apple:1,flour:1,egg:1,butter:1,sugar:1",new[]{S(RecipeSymbol.Knife,"Яблоко"),S(RecipeSymbol.Mix,"Яблоко • тесто"),S(RecipeSymbol.Oven,"Форма • закрытая дверца")}),
                Make("beef_steak","Говяжий стейк","beef:1,oil:1,salt:1",new[]{S(RecipeSymbol.Beef,"Целый кусок"),S(RecipeSymbol.Pan)}),
                Make("braised_beef_onion","Тушёная говядина с луком","beef:1,onion:1,oil:1,salt:1",new[]{S(RecipeSymbol.Knife,"Говядина • лук"),S(RecipeSymbol.Pan),S(RecipeSymbol.Braise,"Слабый огонь • жидкость",true)},"Тушение с жидкостью пока недоступно. Количество жидкости ещё не задано.")
            };
            string catalogPath=folder+"/RecipeBookCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<RecipeBookCatalog>(catalogPath);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<RecipeBookCatalog>();catalog.Recipes=recipes;AssetDatabase.CreateAsset(catalog,catalogPath);}
            if(catalog.Validate()!=null)throw new InvalidOperationException(catalog.Validate());return catalog;
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 point,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));Undo.RegisterCreatedObjectUndo(go,"Recipe book object");go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=point;rect.sizeDelta=size;return rect;
        }
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static Image Image(string name,Transform parent,Vector2 p,Vector2 size,Color color,bool hit=false)
        {var rect=Rect(name,parent,p,size);var image=Undo.AddComponent<Image>(rect.gameObject);image.color=color;image.raycastTarget=hit;return image;}
        private static Text Text(string name,Transform parent,Vector2 p,Vector2 size,int fontSize,string value,GameBootstrap b)
        {var rect=Rect(name,parent,p,size);var text=Undo.AddComponent<Text>(rect.gameObject);text.font=b.Hud.Status.font;text.fontSize=fontSize;text.color=Ink;text.alignment=TextAnchor.MiddleCenter;text.text=value;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
        private static RecipeIconGraphic Icon(string name,Transform parent,Vector2 p,Vector2 size,RecipeSymbol symbol)
        {var rect=Rect(name,parent,p,size);Undo.AddComponent<CanvasRenderer>(rect.gameObject);var icon=Undo.AddComponent<RecipeIconGraphic>(rect.gameObject);icon.Symbol=symbol;icon.color=Ink;icon.raycastTarget=false;return icon;}
        private static Button Button(string name,Transform parent,Vector2 p,Vector2 size,Color color)
        {
            var image=Image(name,parent,p,size,color,true);var button=Undo.AddComponent<Button>(image.gameObject);button.targetGraphic=image;
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,1,.65f);colors.pressedColor=new Color(.75f,.75f,.75f);button.colors=colors;
            var navigation=button.navigation;navigation.mode=Navigation.Mode.None;button.navigation=navigation;return button;
        }
    }
}
