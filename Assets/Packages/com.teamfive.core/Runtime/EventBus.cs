using System;
using System.Collections.Generic;

namespace Core
{
    public interface IGameEvent { }

    public class EventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly object _lock = new();

        public IDisposable Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (!_handlers.ContainsKey(type))
                    _handlers[type] = new List<Delegate>();
                _handlers[type].Add(handler);
            }
            return new EventSubscription<T>(this, handler);
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            var type = typeof(T);
            List<Delegate> handlers;
            lock (_lock)
            {
                if (!_handlers.TryGetValue(type, out handlers)) return;
                handlers = new List<Delegate>(handlers);
            }
            foreach (var handler in handlers)
                (handler as Action<T>)?.Invoke(gameEvent);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (_handlers.TryGetValue(type, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0) _handlers.Remove(type);
                }
            }
        }

        private class EventSubscription<T> : IDisposable where T : IGameEvent
        {
            private readonly EventBus _bus;
            private readonly Action<T> _handler;
            public EventSubscription(EventBus bus, Action<T> handler) { _bus = bus; _handler = handler; }
            public void Dispose() => _bus.Unsubscribe(_handler);
        }
    }
}
