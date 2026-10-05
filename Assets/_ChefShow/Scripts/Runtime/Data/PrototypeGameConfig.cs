using UnityEngine;

namespace ChefShow.Data
{
    public enum TeamId { A, B }

    [CreateAssetMenu(menuName = "Chef Show/Prototype Game Config")]
    public sealed class PrototypeGameConfig : ScriptableObject
    {
        [Header("Первый этап: пробный таймер, не полный раунд")]
        [Min(1)] public float RoundDurationSeconds = 240;
        [Tooltip("На этапе 1 после смены команды заново сгенерируйте сцену и создайте новую рабочую копию.")]
        public TeamId PlayerTeam = TeamId.A;
        public int RunSeed = 1005;
        public bool PrototypeDebugEnabled = true;

        [Header("Движение и обзор")]
        [Range(2, 6)] public float WalkSpeed = 3;
        [Range(3, 8)] public float RunSpeed = 5;
        [Range(0.01f, 0.5f)] public float LookSensitivity = 0.09f;
        [Range(1, 4)] public float InteractionDistance = 2.5f;

        [Header("Арена; изменения применяются явной генерацией")]
        [Min(24)] public float ArenaWidth = 30;
        [Min(20)] public float ArenaDepth = 22;

        public string Validate()
        {
            if (!IsPositive(RoundDurationSeconds) || !IsPositive(WalkSpeed)
                || !IsPositive(RunSpeed) || !IsPositive(LookSensitivity)
                || !IsPositive(InteractionDistance) || !IsPositive(ArenaWidth)
                || !IsPositive(ArenaDepth))
                return "Длительность, размеры, дистанция и скорости должны быть конечными положительными числами.";
            if (ArenaWidth < 24 || ArenaDepth < 20)
                return "Арена должна быть не меньше 24 × 20 м для текущей расстановки.";
            if (PlayerTeam != TeamId.A && PlayerTeam != TeamId.B)
                return "Неизвестная команда игрока.";
            return null;
        }

        private static bool IsPositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
