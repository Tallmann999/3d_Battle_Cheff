using System;

namespace ChefShow.Core
{
    public sealed class GameClock
    {
        public bool Paused { get; set; }
        public float Speed { get; private set; } = 1;
        public float SimulationTime { get; private set; }
        public float Delta { get; private set; }

        public void SetSpeed(float speed)
        {
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed))
                throw new ArgumentOutOfRangeException(nameof(speed));
            Speed = speed;
        }

        public float Tick(float realDelta)
        {
            if (realDelta < 0 || float.IsNaN(realDelta) || float.IsInfinity(realDelta))
                throw new ArgumentOutOfRangeException(nameof(realDelta));
            Delta = Paused ? 0 : realDelta * Speed;
            SimulationTime += Delta;
            return Delta;
        }
    }
}
