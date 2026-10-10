using System;
using ChefShow.Inventory;
using ChefShow.Data;

namespace ChefShow.Core
{
    public readonly struct RunStarted
    {
        public readonly string RunId;
        public readonly int Seed;
        public RunStarted(string runId, int seed) { RunId = runId; Seed = seed; }
    }

    public readonly struct PauseChanged
    {
        public readonly string RunId;
        public readonly bool Paused;
        public PauseChanged(string runId, bool paused) { RunId = runId; Paused = paused; }
    }

    public sealed class PrototypeRun : IDisposable
    {
        public string RunId { get; } = Guid.NewGuid().ToString("N");
        public int Seed { get; }
        public TeamId PlayerTeam { get; }
        public GameClock Clock { get; } = new GameClock();
        public GameEventBus Events { get; }
        public InventoryState Inventory { get; }
        public float RemainingSeconds { get; private set; }
        public bool Disposed { get; private set; }

        public PrototypeRun(float duration, int seed, Action<Type, Exception> reportError, int basketCapacity = 10, int trayCapacity = 24, TeamId playerTeam = TeamId.A,
            ChefShow.Cooking.CookingSettings cooking = null, DishwareSettings dishware = null, ChefShow.Recipes.RecipeIdentitySettings recognition = null)
        {
            if (duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));
            RemainingSeconds = duration;
            Seed = seed;
            PlayerTeam = playerTeam;
            Events = new GameEventBus(reportError);
            Inventory = new InventoryState(this, basketCapacity, trayCapacity, cooking, dishware,recognition);
        }

        public void Tick(float realDelta)
        {
            if (Disposed) return;
            if (realDelta < 0 || float.IsNaN(realDelta) || float.IsInfinity(realDelta)) throw new ArgumentOutOfRangeException(nameof(realDelta));
            // The final frame heats only until 00:00, never past the round boundary.
            float accepted = Clock.Paused ? realDelta : Math.Min(realDelta, RemainingSeconds / Clock.Speed);
            float delta = Clock.Tick(accepted);
            Inventory.TickCooking(delta);
            RemainingSeconds = Math.Max(0, RemainingSeconds - delta);
            if (RemainingSeconds <= 0) Inventory.SubmitAtTimeup();
        }

        public void SetPaused(bool paused)
        {
            if (Disposed || Clock.Paused == paused) return;
            Clock.Paused = paused;
            Events.Publish(new PauseChanged(RunId, paused));
        }

        public void SetRemaining(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Disposed) RemainingSeconds = seconds;
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Events.Dispose();
        }
    }
}
