using System;
using System.Collections.Generic;

namespace ChefShow.Core
{
    public sealed class GameEventBus : IDisposable
    {
        private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();
        private readonly Queue<Action> pending = new Queue<Action>();
        private readonly Action<Type, Exception> reportError;
        private bool delivering;
        private bool disposed;

        public GameEventBus(Action<Type, Exception> reportError) => this.reportError = reportError;

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GameEventBus));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(typeof(T), out var list))
                handlers.Add(typeof(T), list = new List<Delegate>());
            list.Add(handler);
            return new Subscription(() => list.Remove(handler));
        }

        public void Publish<T>(T fact)
        {
            if (disposed) return;
            pending.Enqueue(() =>
            {
                if (!handlers.TryGetValue(typeof(T), out var list)) return;
                foreach (var handler in list.ToArray())
                {
                    if (disposed) break;
                    try { ((Action<T>)handler)(fact); }
                    catch (Exception error) { reportError?.Invoke(typeof(T), error); }
                }
            });
            if (delivering) return;
            delivering = true;
            try { while (!disposed && pending.Count > 0) pending.Dequeue()(); }
            finally { delivering = false; }
        }

        public void Dispose()
        {
            disposed = true;
            pending.Clear();
            handlers.Clear();
        }

        private sealed class Subscription : IDisposable
        {
            private Action unsubscribe;
            public Subscription(Action unsubscribe) => this.unsubscribe = unsubscribe;
            public void Dispose() { unsubscribe?.Invoke(); unsubscribe = null; }
        }
    }
}
