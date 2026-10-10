using System;
using System.Globalization;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace ChefShow.Judging
{
    public sealed class JudgingController : MonoBehaviour
    {
        public JudgingConfig Config;
        public GameObject Panel;
        public Text Total;
        public Text Breakdown;
        public Text Reasons;
        [Tooltip("Optional future round assignment. Empty uses the submitted recipe; independent of book selection. Captured on Restart.")]
        public string AssignedRecipeId;
        public ScoreBreakdown Current { get; private set; }
        public DishSnapshot JudgedDish { get; private set; }
        private GameBootstrap owner;
        private JudgingSettings settings;
        private string assignedRecipeId;

        public string Validate()
        {
            if (Config == null) return "Не назначен конфиг судейства.";
            var error = Config.Validate();
            if (error != null) return error;
            if (!string.IsNullOrWhiteSpace(AssignedRecipeId) && !Config.Profiles.Any(p => p.Reference.Id == AssignedRecipeId.Trim()))
                return "Неизвестный рецепт задания для судейства.";
            if (Panel == null || Total == null || Breakdown == null || Reasons == null)
                return "Не сохранены панель и тексты результата судейства.";
            return null;
        }

        public void Initialize(GameBootstrap bootstrap) { owner = bootstrap; }

        public void ResetRun()
        {
            settings = Config.Capture(owner.Run.Inventory.RecognitionRules, owner.Run.Inventory.CookingRules);
            assignedRecipeId = string.IsNullOrWhiteSpace(AssignedRecipeId) ? null : AssignedRecipeId.Trim();
            JudgedDish = null;
            Current = null;
            Total.text = "ИТОГ: — / 100";
            Breakdown.text = "";
            Reasons.text = "";
            Panel.SetActive(false);
        }

        public void Present()
        {
            if (owner == null || owner.Run == null) return;
            var run = owner.Run;
            var submitted = run.Inventory.SubmittedDish;
            if (submitted == null)
            {
                JudgedDish = null;
                Current = null;
                Panel.SetActive(false);
                return;
            }
            if (!ReferenceEquals(JudgedDish, submitted))
            {
                Current = DishScorer.Evaluate(submitted, settings, assignedRecipeId);
                JudgedDish = submitted;
                Total.text = "ИТОГ: " + Current.Total.ToString("0", CultureInfo.InvariantCulture) + " / 100"
                    + "\n<size=16>" + Current.RecipeName + "</size>";
                Breakdown.text = string.Join("\n", Current.Categories.Select(category =>
                    category.Name + ": " + category.Points.ToString("0.#", CultureInfo.InvariantCulture)
                    + " / " + category.Maximum.ToString("0.#", CultureInfo.InvariantCulture)));
                Reasons.text = string.Join("\n", Current.Reasons.Take(3).Select(reason => "• " + reason));
                run.Events.Publish(new DishJudged(run.RunId, run.Clock.SimulationTime, submitted, Current));
            }
            bool hasCurrentScore = Current != null && ReferenceEquals(JudgedDish, owner.Run.Inventory.SubmittedDish);
            Panel.SetActive(hasCurrentScore && !owner.IsPaused && (owner.RecipeBook == null || !owner.RecipeBook.IsOpen));
        }
    }
}
