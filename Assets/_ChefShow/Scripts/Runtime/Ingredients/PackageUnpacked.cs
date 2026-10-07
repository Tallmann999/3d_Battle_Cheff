using System.Collections.Generic;
using System.Linq;
using ChefShow.Core;
using ChefShow.Data;
using ChefShow.Inventory;

namespace ChefShow.Ingredients
{
    public readonly struct PackageUnpacked
    {
        public readonly string RunId, RoundId, ActorId, PackageId, IngredientId;
        public readonly TeamId Team;
        public readonly float SimulationTime;
        public readonly int Quantity;
        public readonly IReadOnlyList<FoodPortionSnapshot> Contents;
        public PackageUnpacked(PrototypeRun run, FoodPortion package, IEnumerable<FoodPortion> contents)
        {
            RunId = run.RunId; RoundId = "prototype"; ActorId = run.PlayerTeam + "1"; Team = run.PlayerTeam;
            PackageId = package.Id; IngredientId = package.PackedIngredient.Id; SimulationTime = run.Clock.SimulationTime;
            var snapshots = contents.Select(p => p.Snapshot()).ToArray();
            Quantity = snapshots.Length; Contents = System.Array.AsReadOnly(snapshots);
        }
    }
}
