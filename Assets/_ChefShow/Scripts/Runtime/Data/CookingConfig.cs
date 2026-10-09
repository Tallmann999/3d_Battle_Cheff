using System;
using ChefShow.Cooking;
using UnityEngine;
using UnityEngine.Serialization;

namespace ChefShow.Data
{
    [CreateAssetMenu(menuName = "Chef Show/Cooking Config")]
    public sealed class CookingConfig : ScriptableObject
    {
        [Header("Первый срез D-022; стартовые параметры")]
        [Range(1, 3)] public int Capacity = 3;
        [Min(1)] public float ReadySeconds = 30;
        [Min(1)] public float OvercookedSeconds = 45;
        [Min(1)] public float BurnedSeconds = 60;
        [Min(.01f)] public float LowRate = .55f;
        [Min(.01f)] public float MediumRate = 1;
        [Min(.01f)] public float HighRate = 1.75f;
        [Range(0, 10)] public int PotStirs = 3;
        [Header("Дискретные дозы; контейнер остаётся в руке")]
        public string SaltIngredientId = "salt";
        public string OilIngredientId = "oil";
        [Range(1, 10), Tooltip("Доз на выбранную порцию за отдельный ЛКМ. Применяется при Restart.")]
        public int DosesPerPress = 1;
        [Header("Геометрия и доступ к дальним приборам")]
        [FormerlySerializedAs("TableLength")]
        [Tooltip("Ширина перед участником (вдоль ряда / мировая Z).")]
        [Min(3.2f)] public float TableWidth = 3.6f;
        [Tooltip("Глубина от участника к центру студии (мировая X).")]
        [Min(2.1f)] public float TableDepth = 2.3f;
        [Range(3.1f, 5)] public float ApplianceInteractionDistance = 4.2f;
        public string Validate()
        {
            if (Capacity < 1 || Capacity > 3 || PotStirs < 0 || PotStirs > 10) return "Прибор: 1–3 порции, 0–10 перемешиваний.";
            if (!Positive(ReadySeconds) || !Positive(OvercookedSeconds) || !Positive(BurnedSeconds)
                || !(ReadySeconds < OvercookedSeconds && OvercookedSeconds < BurnedSeconds)) return "Тепловые пороги должны возрастать: готово < переготовка < сгорание.";
            if (!Positive(LowRate) || !Positive(MediumRate) || !Positive(HighRate)
                || !(LowRate < MediumRate && MediumRate < HighRate)) return "Скорости должны возрастать: слабый < средний < сильный.";
            if (!Positive(TableWidth) || TableWidth < 3.2f || !Positive(TableDepth) || TableDepth < 2.1f || !Positive(ApplianceInteractionDistance)
                || ApplianceInteractionDistance > 5) return "Проверьте ширину/глубину стола и дистанцию приборов (до 5 м).";
            if (DosesPerPress < 1 || DosesPerPress > 10 || string.IsNullOrWhiteSpace(SaltIngredientId)
                || string.IsNullOrWhiteSpace(OilIngredientId) || SaltIngredientId == OilIngredientId)
                return "Проверьте разные ID соли/масла и число доз (1–10).";
            return null;
        }
        private static bool Positive(float n) => n > 0 && !float.IsNaN(n) && !float.IsInfinity(n);
        public CookingSettings Capture()
        {
            var error = Validate(); if (error != null) throw new InvalidOperationException(error);
            return new CookingSettings(Capacity, ReadySeconds, OvercookedSeconds, BurnedSeconds,
                LowRate, MediumRate, HighRate, PotStirs,
                DosesPerPress, SaltIngredientId, OilIngredientId);
        }
    }
}
