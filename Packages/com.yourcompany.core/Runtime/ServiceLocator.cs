using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        // Зарегистрировать
        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] {type.Name} уже зарегистрирован — перезаписываю");
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
            }
        }

        // Получить
        public static T Get<T>()
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
                return (T)service;

            Debug.LogError($"[ServiceLocator] {type.Name} не найден!");
            return default;
        }

        // Сброс при перезапуске сцены
        public static void Clear()
        {
            _services.Clear();
        }
    }
}