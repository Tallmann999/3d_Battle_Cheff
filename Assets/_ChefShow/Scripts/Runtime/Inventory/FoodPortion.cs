using System.Collections.Generic;
using System.Linq;
using ChefShow.Data;

namespace ChefShow.Inventory
{
    public enum PreparationState { Whole, RoughCut, Chopped, Mixed }
    public enum CookState { Raw, Cooking, Cooked, Overcooked, Burned }

    public readonly struct FoodOrigin
    {
        public readonly string SourcePortionId, IngredientId;
        public readonly int Quantity;
        public FoodOrigin(string source, string ingredient, int quantity)
        { SourcePortionId = source; IngredientId = ingredient; Quantity = quantity; }
    }

    public readonly struct FoodOperation
    {
        public readonly string Action;
        public readonly float SimulationTime;
        public readonly PortionLocation Location;
        public readonly PreparationState Preparation;
        public FoodOperation(string action, float time, PortionLocation location, PreparationState preparation)
        { Action = action; SimulationTime = time; Location = location; Preparation = preparation; }
    }

    // Snapshot owns copies of values. Presentation and later transfers cannot alter it.
    public sealed class FoodPortionSnapshot
    {
        public string Id { get; }
        public string IngredientId { get; }
        public string PackedIngredientId { get; }
        public int PackedQuantity { get; }
        public int Quantity { get; }
        public PreparationState Preparation { get; }
        public int ChopPresses { get; }
        public CookState Cooking { get; }
        public float HeatProgress { get; }
        public ChefShow.Cooking.CookerKind? LastCooker { get; }
        public int StirPresses { get; }
        public int RequiredStirs { get; }
        public int SaltDoses { get; }
        public int OilDoses { get; }
        public bool Contaminated { get; }
        public PortionLocation Location { get; }
        public IReadOnlyList<FoodOrigin> OriginComponents { get; }
        public IReadOnlyList<FoodOperation> Operations { get; }
        internal FoodPortionSnapshot(FoodPortion portion)
        {
            Id = portion.Id; IngredientId = portion.Ingredient.Id; Quantity = portion.Quantity;
            PackedIngredientId = portion.PackedIngredient == null ? null : portion.PackedIngredient.Id;
            PackedQuantity = portion.PackedQuantity;
            Preparation = portion.Preparation; ChopPresses = portion.ChopPresses; Cooking = portion.Cooking; HeatProgress = portion.HeatProgress;
            SaltDoses = portion.SaltDoses; OilDoses = portion.OilDoses; Contaminated = portion.Contaminated;
            LastCooker = portion.LastCooker; StirPresses = portion.StirPresses; RequiredStirs = portion.RequiredStirs;
            Location = portion.Location;
            OriginComponents = System.Array.AsReadOnly(portion.OriginComponents.ToArray());
            Operations = System.Array.AsReadOnly(portion.Operations.ToArray());
        }
    }

    public sealed class FoodPortion
    {
        private readonly List<FoodOrigin> origins = new List<FoodOrigin>();
        private readonly List<FoodOperation> operations = new List<FoodOperation>();
        public string Id { get; }
        public IngredientDefinition Ingredient { get; }
        public IngredientDefinition PackedIngredient { get; }
        public int PackedQuantity { get; }
        // One collected item is one unit. A package remains one container until unpacked.
        public int Quantity { get; internal set; } = 1;
        public PreparationState Preparation { get; internal set; } = PreparationState.Whole;
        public int ChopPresses { get; internal set; }
        public CookState Cooking { get; internal set; } = CookState.Raw;
        public float HeatProgress { get; internal set; }
        public ChefShow.Cooking.CookerKind? LastCooker { get; internal set; }
        public int StirPresses { get; internal set; }
        public int RequiredStirs { get; internal set; }
        public int SaltDoses { get; internal set; }
        public int OilDoses { get; internal set; }
        public bool Contaminated { get; internal set; }
        public PortionLocation Location { get; internal set; }
        public int SocketIndex { get; internal set; } = -1;
        public IReadOnlyList<FoodOrigin> OriginComponents => origins.AsReadOnly();
        public IReadOnlyList<FoodOperation> Operations => operations.AsReadOnly();
        internal FoodPortion(string id, IngredientDefinition ingredient)
        {
            Id = id; Ingredient = ingredient;
            PackedIngredient = ingredient.Contents;
            PackedQuantity = PackedIngredient == null ? 0 : ingredient.ContentsQuantity;
            origins.Add(new FoodOrigin(id, ingredient.Id, 1));
        }
        internal void InheritPackage(FoodPortion package)
        {
            origins.Clear(); origins.Add(new FoodOrigin(package.Id, Ingredient.Id, Quantity));
            operations.AddRange(package.operations);
            Contaminated = package.Contaminated;
        }
        internal void RecordOperation(string action, float time)
            => operations.Add(new FoodOperation(action, time, Location, Preparation));
        public FoodPortionSnapshot Snapshot() => new FoodPortionSnapshot(this);
    }
}
