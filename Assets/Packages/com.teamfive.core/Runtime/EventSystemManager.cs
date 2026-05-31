using UnityEngine;
using UnityEngine.EventSystems;

namespace Core
{
    /// <summary>
    /// Автоматически удаляет дублирующиеся EventSystem в сцене
    /// Оставляет только один активный EventSystem
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class EventSystemManager : MonoBehaviour
    {
        private static EventSystem _primaryEventSystem;
        private static bool _isChecking = false;

        private void Awake()
        {
            RemoveDuplicateEventSystems();
        }

        private void Update()
        {
            // Проверяем каждый кадр, но не рекурсивно
            if (!_isChecking)
            {
                RemoveDuplicateEventSystems();
            }
        }

        /// <summary>
        /// Проверяет и удаляет дублирующиеся EventSystem
        /// </summary>
        private static void RemoveDuplicateEventSystems()
        {
            _isChecking = true;

            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();

            if (eventSystems.Length > 1)
            {
                // Если есть сохраненный primary, используем его
                if (_primaryEventSystem != null && _primaryEventSystem.gameObject != null)
                {
                    foreach (var es in eventSystems)
                    {
                        if (es != _primaryEventSystem)
                        {
                            Debug.Log($"[EventSystemManager] Destroying duplicate EventSystem on '{es.gameObject.name}'");
                            Destroy(es.gameObject);
                        }
                    }
                }
                else
                {
                    // Иначе оставляем первый активный
                    _primaryEventSystem = eventSystems[0];
                    for (int i = 1; i < eventSystems.Length; i++)
                    {
                        Debug.Log($"[EventSystemManager] Destroying duplicate EventSystem on '{eventSystems[i].gameObject.name}'");
                        Destroy(eventSystems[i].gameObject);
                    }
                }
            }
            else if (eventSystems.Length == 1)
            {
                _primaryEventSystem = eventSystems[0];
            }

            _isChecking = false;
        }
    }
}
