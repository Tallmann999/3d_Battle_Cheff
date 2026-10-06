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

        public PrototypeRun(float duration, int seed, Action<Type, Exception> reportError, int basketCapacity = 10, int trayCapacity = 24, TeamId playerTeam = TeamId.A)
        {
            if (duration <= 0 || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new ArgumentOutOfRangeException(nameof(duration));
            RemainingSeconds = duration;
            Seed = seed;
            PlayerTeam = playerTeam;
            Events = new GameEventBus(reportError);
            Inventory = new InventoryState(this, basketCapacity, trayCapacity);
        }

        public void Tick(float realDelta)
        {
            if (!Disposed) RemainingSeconds = Math.Max(0, RemainingSeconds - Clock.Tick(realDelta));
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
