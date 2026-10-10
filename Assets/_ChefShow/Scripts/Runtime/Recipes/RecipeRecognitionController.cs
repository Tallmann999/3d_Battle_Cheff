using ChefShow.Core;
using ChefShow.Data;
using UnityEngine;
using UnityEngine.UI;
namespace ChefShow.Recipes
{
    public sealed class RecipeRecognitionController : MonoBehaviour
    {
        public RecipeRecognitionCatalog Config;
        public GameObject Panel;
        public Text Label;
        public DishRecognition Current {get;private set;}
        private GameBootstrap owner;private string runId;private int version=-1;
        public string Validate()=>Config==null?"Не назначен каталог распознавания.":Config.Validate()??(Panel==null||Label==null?"Не сохранён результат распознавания.":null);
        public void Initialize(GameBootstrap bootstrap){owner=bootstrap;}
        public void Present()
        {
            var run=owner.Run;if(run==null)return;
            if(runId!=run.RunId||version!=run.Inventory.Version)
            {
                runId=run.RunId;version=run.Inventory.Version;
                Current=(run.Inventory.SubmittedDish??run.Inventory.CaptureDish()).Recognition;
                Label.text=(run.Inventory.SubmittedDish==null?"Блюдо: ":"Подано: ")+Current.Name;
                if(!string.IsNullOrEmpty(Current.Reason))Label.text+="\n"+Current.Reason;
                if(Current.Issues.Count>0)Label.text+="\n"+string.Join(" · ",Current.Issues);
            }
            Panel.SetActive(!owner.IsPaused && (owner.RecipeBook==null||!owner.RecipeBook.IsOpen));
        }
    }
}
