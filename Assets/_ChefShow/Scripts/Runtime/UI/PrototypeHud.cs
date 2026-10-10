using ChefShow.Core;
using ChefShow.Player;
using ChefShow.Inventory;
using ChefShow.Ingredients;
using UnityEngine;
using UnityEngine.UI;

namespace ChefShow.UI
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public Text Status;
        public Text Context;
        public Text InteractionKey;
        public HandPromptGraphic HandIcon;
        public Text TaskCard;
        public GameObject PausePanel;
        public GameObject DebugPanel;
        public Button Resume;
        public Button Restart;
        public Button DebugRestart;
        public Button Timer30;
        public Button Timer60;
        public Button Timer240;
        public Slider Sensitivity;

        public void Present(PrototypeRun run, FirstPersonRig player, bool paused, bool debug, bool taskVisible, InventoryController inventory = null, ToolDrawerController tools = null, PreparationController preparation = null, ChefShow.Cooking.CookingController cooking = null, ServingController serving = null, ChefShow.Cooking.MixingController mixing = null, DishwareController dishware = null)
        {
            int seconds = Mathf.CeilToInt(run.RemainingSeconds);
            Status.text = $"CHEF SHOW · {(cooking != null ? "КУХНЯ" : inventory == null ? "ЭТАП 1" : preparation == null ? "ПРОДУКТЫ И ПЕРЕНОС" : "ПОДГОТОВКА ПРОДУКТОВ")}\nПробный таймер  {seconds / 60:00}:{seconds % 60:00}     Команда {player.GetComponent<Contestants.PrototypeActor>().Team}\n"
                + (inventory == null ? "Арена и управление. Готовка ещё не реализована." : inventory.Summary);
            if(serving!=null)Status.text+="\nПрезентабельность: −"+run.Inventory.PresentationPenalty+" балл.";
            if(run.Inventory.HeldDishware!=null)Status.text+="\nЛевая рука: "+run.Inventory.HeldDishware.DisplayName;
            if (tools != null) Status.text += "\n" + tools.Summary;
            if (cooking != null) Status.text += "\n" + cooking.Summary;
            PausePanel.SetActive(paused && !debug);
            DebugPanel.SetActive(debug);
            TaskCard.gameObject.SetActive(taskVisible && !paused);
            Context.text = player.Focused ? "Фокус станции · F — вернуться\nОперации готовки появятся на следующем этапе."
                : player.Target == null ? "" : player.Target.IsPlayerStation
                    ? "F — фокус своей станции\nГотовка появится на следующем этапе."
                    : $"{player.Target.DisplayName}\n{player.Target.Description}";
            if (inventory != null) Context.text = inventory.Describe(player.Target);
            if (tools != null)
            {
                string drawer = tools.Describe(player.Target);
                if (drawer != null) Context.text = drawer;

            }
            if (preparation != null)
            {
                string board = preparation.Describe(player.Target);
                if (board != null) Context.text = board;
            }
            if (cooking != null)
            { string appliance = cooking.Describe(player.Target); if (appliance != null) Context.text = appliance; }
            if(mixing!=null){string bowl=mixing.Describe(player.Target);if(bowl!=null)Context.text=bowl;}
            if(dishware!=null){string supply=dishware.Describe(player.Target);if(supply!=null)Context.text=supply;}
            if (serving != null) { string plate=serving.Describe(player.Target); if(plate!=null) Context.text=plate; }
            if (run.RemainingSeconds <= 0 && !paused)
                Context.text = "Пробный таймер завершён. Esc → Начать выпуск заново.";
            if (InteractionKey != null)
            {
                bool rightClick = Context.text.StartsWith("ПКМ — ");
                bool leftClick = Context.text.StartsWith("ЛКМ — ");
                bool show = !paused && run.RemainingSeconds > 0 && (rightClick || leftClick);
                if(HandIcon!=null) {HandIcon.enabled=show;HandIcon.RightHand=rightClick;HandIcon.Taking=Context.text.Contains("Взять");HandIcon.SetVerticesDirty();}
                InteractionKey.enabled = show;
                if (show)
                {
                    InteractionKey.text = rightClick ? "ПКМ" : "ЛКМ";
                    InteractionKey.rectTransform.sizeDelta = new Vector2(60, 26);
                    Context.text = Context.text.Substring(6);
                    InteractionKey.color = new Color(1, .88f, .32f, .45f + .55f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 5)));
                }
            }
        }
    }
}
