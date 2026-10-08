using ChefShow.Data;
using UnityEngine;

namespace ChefShow.Player
{
    public sealed class PrototypeInteractable : MonoBehaviour
    {
        public string DisplayName;
        public bool IsPlayerStation;
        public TeamId Team;
        public Transform FocusPoint;
        [Tooltip("0 — обычная дистанция; дальние приборы используют свой предел.")]
        public float InteractionDistanceOverride;
        [TextArea] public string Description = "Готовка появится на следующем этапе.";

        public bool CanFocus(TeamId playerTeam) => IsPlayerStation && Team == playerTeam;
    }
}
