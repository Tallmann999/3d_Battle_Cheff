using System;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace ChefShow.UI
{
    public sealed class RecipeBookController : MonoBehaviour
    {
        public RecipeBookCatalog Catalog;
        public GameObject BookPanel, IngredientList;
        public Text Title, PageNumber, ReferenceNote, SelectedTitle, SelectedIngredients;
        public Button Previous, Next, Choose, Close;
        public RecipeIconGraphic[] IngredientIcons, StepIcons, StepArrows;
        public Text[] IngredientCaptions, StepCaptions;
        private GameBootstrap owner;
        private RecipePage[] pages;
        private bool open;
        private int page;
        private int releaseFrame;
        private bool waitForRelease;
        public bool IsOpen=>open;
        public int PageIndex=>page;
        public string SelectedRecipeId {get;private set;}
        public RecipePage CurrentPage=>pages==null?null:pages[page];
        public string Validate()
        {
            if(Catalog==null||Catalog.Validate()!=null)return Catalog==null?"Не назначен каталог книги.":Catalog.Validate();
            if(BookPanel==null||IngredientList==null||Title==null||PageNumber==null||ReferenceNote==null||SelectedTitle==null||SelectedIngredients==null||Previous==null||Next==null||Choose==null||Close==null)return "Не назначены элементы книги.";
            if(IngredientIcons==null||IngredientIcons.Length!=7||IngredientCaptions==null||IngredientCaptions.Length!=7||StepIcons==null||StepIcons.Length!=6||StepCaptions==null||StepCaptions.Length!=6||StepArrows==null||StepArrows.Length!=5)return "Не сохранена схема книги.";
            if(IngredientIcons.Any(x=>x==null||x.GetComponent<CanvasRenderer>()==null)||IngredientCaptions.Any(x=>x==null)||StepIcons.Any(x=>x==null)||StepCaptions.Any(x=>x==null)||StepArrows.Any(x=>x==null))return "Схема содержит пустые ссылки.";
            return null;
        }
        public void Initialize(GameBootstrap bootstrap)
        {
            owner=bootstrap;
            Previous.onClick.AddListener(()=>Turn(-1));Next.onClick.AddListener(()=>Turn(1));Choose.onClick.AddListener(SelectCurrent);Close.onClick.AddListener(()=>SetOpen(false));
        }
        public void ResetRun()
        {
            pages=Catalog.Capture();page=0;SelectedRecipeId=null;IngredientList.SetActive(false);SetOpen(false);PresentPage();
        }
        public void Toggle(){if(owner!=null&&!owner.IsPaused)SetOpen(!open);}
        public void SetOpen(bool value)
        {
            if(value && (owner==null||owner.IsPaused||pages==null))return;
            if(open!=value){releaseFrame=Time.frameCount+1;waitForRelease=true;}
            open=value;BookPanel.SetActive(value);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
            if(value)PresentPage();
        }
        public bool BlocksGameplay(bool leftHeld,bool rightHeld)
        {
            if(open)return true;
            if(waitForRelease)
            {
                if(Time.frameCount<=releaseFrame||leftHeld||rightHeld)return true;
                waitForRelease=false;
            }
            return false;
        }
        public void Turn(int delta)
        {
            if(!open||owner.IsPaused)return;
            page=(page+delta%pages.Length+pages.Length)%pages.Length;PresentPage();
        }
        public void SelectCurrent()
        {
            if(!open||owner.IsPaused)return;
            var r=CurrentPage;SelectedRecipeId=r.Id;SelectedTitle.text=r.Title;
            SelectedIngredients.text=string.Join("\n",r.Ingredients.Select(i=>i.Name+"  ×"+i.Amount+(i.Dose?" доз.":"")))+(r.Proposed?"\n\nКоличества — предложение.":"");
            IngredientList.SetActive(true);
        }
        public void PreviewPage(RecipePage r)=>DrawPage(r,0,12);
        public void PresentPage(){var r=CurrentPage;if(r!=null)DrawPage(r,page,pages.Length);}
        private void DrawPage(RecipePage r,int index,int total)
        {
            Title.text=r.Title;PageNumber.text=(index+1).ToString("00")+" / "+total;
            ReferenceNote.text=(r.Proposed?"Количества — стартовое предложение.":"")+(!string.IsNullOrWhiteSpace(r.Note)?"\n"+r.Note:"");
            for(int i=0;i<IngredientIcons.Length;i++)
            {
                bool shown=i<r.Ingredients.Count;IngredientIcons[i].transform.parent.gameObject.SetActive(shown);
                // Seven editable ingredients use two rows rather than covering the footer.
                int columns=r.Ingredients.Count>6?4:3;
                var cell=(RectTransform)IngredientIcons[i].transform.parent;
                cell.anchoredPosition=new Vector2(columns==4?-345+(i%4)*88:-307+(i%3)*102,73-(i/columns)*114);
                IngredientCaptions[i].rectTransform.anchoredPosition=new Vector2(0,-45);
                IngredientCaptions[i].rectTransform.sizeDelta=new Vector2(columns==4?86:100,60);
                IngredientCaptions[i].fontSize=columns==4?12:13;
                if(!shown)continue;var ingredient=r.Ingredients[i];
                IngredientIcons[i].Show(SymbolFor(ingredient.Id));
                IngredientCaptions[i].text=ingredient.Name+"\n×"+ingredient.Amount+(ingredient.Dose?" доз.":"");
            }
            int count=r.Steps.Count+1;
            for(int i=0;i<StepIcons.Length;i++)
            {
                bool shown=i<count;StepIcons[i].transform.parent.gameObject.SetActive(shown);
                if(!shown)continue;
                StepIcons[i].Show(i==count-1?RecipeSymbol.Dish:r.Steps[i].Symbol,r.Id);
                StepCaptions[i].text=i==count-1?"":r.Steps[i].Caption;
            }
            for(int i=0;i<StepArrows.Length;i++)StepArrows[i].gameObject.SetActive(i<count-1);
        }
        public static RecipeSymbol SymbolFor(string id)
        {
            switch(id)
            {
                case "potato":return RecipeSymbol.Potato;case "onion":return RecipeSymbol.Onion;case "beef":return RecipeSymbol.Beef;
                case "carrot":return RecipeSymbol.Carrot;case "egg":return RecipeSymbol.Egg;case "cheese":return RecipeSymbol.Cheese;
                case "flour":return RecipeSymbol.Flour;case "butter":return RecipeSymbol.Butter;case "sugar":return RecipeSymbol.Sugar;
                case "apple":return RecipeSymbol.Apple;case "salt":return RecipeSymbol.Salt;case "oil":return RecipeSymbol.Oil;
                default:throw new ArgumentException("Нет рисунка для продукта: "+id);
            }
        }
    }
}
