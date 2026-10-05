using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Player
{
    public sealed class PrototypeInteractable : MonoBehaviour
    {
        public string DisplayName;
        public bool IsPlayerStation;
        public TeamId Team;
        [TextArea] public string Description = "Готовка появится на следующем этапе.";

        public bool CanFocus(TeamId playerTeam) => IsPlayerStation && Team == playerTeam;
    }
}
