using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;

namespace ChefShow.Ingredients
{
    public readonly struct PreparationChanged
    {
        public readonly string RunId, RoundId, ActorId, PortionId, IngredientId, Action;
        public readonly TeamId Team;
        public readonly float SimulationTime;
        public readonly int Presses, RequiredPresses, Quantity;
        public readonly PreparationState Preparation;
        public readonly CookState Cooking;
        public PreparationChanged(PrototypeRun run, FoodPortion portion, string action)
        {
            RunId = run.RunId; RoundId = "prototype"; ActorId = run.PlayerTeam + "1"; Team = run.PlayerTeam;
            PortionId = portion.Id; IngredientId = portion.Ingredient.Id; Action = action;
            SimulationTime = run.Clock.SimulationTime; Presses = portion.ChopPresses;
            RequiredPresses = InventoryState.RequiredChopPresses; Quantity = portion.Quantity;
            Preparation = portion.Preparation; Cooking = portion.Cooking;
        }
    }
}
