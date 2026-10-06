using ChefShow.Core;
using ChefShow.Player;
using ChefShow.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace ChefShow.UI
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public Text Status;
        public Text Context;
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

        public void Present(PrototypeRun run, FirstPersonRig player, bool paused, bool debug, bool taskVisible, InventoryController inventory = null)
        {
            int seconds = Mathf.CeilToInt(run.RemainingSeconds);
            Status.text = $"CHEF SHOW · {(inventory == null ? "ЭТАП 1" : "ПРОДУКТЫ И ПЕРЕНОС")}\nПробный таймер  {seconds / 60:00}:{seconds % 60:00}     Команда {player.GetComponent<Contestants.PrototypeActor>().Team}\n"
                + (inventory == null ? "Арена и управление. Готовка ещё не реализована." : inventory.Summary);
            PausePanel.SetActive(paused && !debug);
            DebugPanel.SetActive(debug);
            TaskCard.gameObject.SetActive(taskVisible && !paused);
            Context.text = player.Focused ? "Фокус станции · RMB — вернуться\nОперации готовки появятся на следующем этапе."
                : player.Target == null ? "" : player.Target.IsPlayerStation
                    ? "E — фокус своей станции\nГотовка появится на следующем этапе."
                    : $"{player.Target.DisplayName}\n{player.Target.Description}";
            if (inventory != null) Context.text = inventory.Describe(player.Target);
            if (run.RemainingSeconds <= 0 && !paused)
                Context.text = "Пробный таймер завершён. Esc → Начать выпуск заново.";
        }
    }
}
