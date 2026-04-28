using System;
using System.Collections.Generic;

namespace Core
{
    public static class EventBus
    {
        // Хранит для каждого типа события список подписчиков
        private static readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        // Подписаться
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
                _subscribers[type] = new List<Delegate>();

            _subscribers[type].Add(handler);
        }

        // Отписаться (важно для очистки!)
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var handlers))
                handlers.Remove(handler);
        }

        // Вызвать событие
        public static void Raise<T>(T eventData) where T : struct
        {
            var type = typeof(T);
            if (!_subscribers.TryGetValue(type, out var handlers)) return;

            // Идём с конца — можно безопасно удалять во время итерации
            for (int i = handlers.Count - 1; i >= 0; i--)
            {
                var handler = handlers[i] as Action<T>;
                handler?.Invoke(eventData);
            }
        }

        // Полная очистка (при перезапуске игры, смене сцен)
        public static void Clear()
        {
            _subscribers.Clear();
        }
    }
}