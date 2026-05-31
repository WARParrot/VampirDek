using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;

namespace Core.Editor
{
    /// <summary>
    /// Утилита для исправления проблем с EventSystem
    /// </summary>
    public static class EventSystemFixer
    {
        [MenuItem("Tools/VampirDek/Fix Event Systems", false, 100)]
        public static void FixEventSystems()
        {
            EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>();

            if (eventSystems.Length == 0)
            {
                Debug.Log("[EventSystemFixer] No EventSystem found. Creating one...");
                CreateEventSystem();
            }
            else if (eventSystems.Length == 1)
            {
                Debug.Log("[EventSystemFixer] Only one EventSystem found. Everything is OK!");

                // Добавляем EventSystemSingleton если его нет
                EventSystemSingleton singleton = eventSystems[0].GetComponent<EventSystemSingleton>();
                if (singleton == null)
                {
                    eventSystems[0].gameObject.AddComponent<EventSystemSingleton>();
                    Debug.Log("[EventSystemFixer] Added EventSystemSingleton to prevent duplicates.");
                }
            }
            else
            {
                Debug.LogWarning($"[EventSystemFixer] Found {eventSystems.Length} EventSystems. Removing duplicates...");

                // Оставляем первый, остальные удаляем
                for (int i = 1; i < eventSystems.Length; i++)
                {
                    Debug.Log($"[EventSystemFixer] Removing EventSystem from '{eventSystems[i].gameObject.name}'");
                    Object.DestroyImmediate(eventSystems[i].gameObject);
                }

                // Добавляем EventSystemSingleton на оставшийся
                EventSystemSingleton singleton = eventSystems[0].GetComponent<EventSystemSingleton>();
                if (singleton == null)
                {
                    eventSystems[0].gameObject.AddComponent<EventSystemSingleton>();
                    Debug.Log("[EventSystemFixer] Added EventSystemSingleton to prevent future duplicates.");
                }

                Debug.Log("[EventSystemFixer] Fixed! Only one EventSystem remains.");
            }
        }

        [MenuItem("Tools/VampirDek/Create Event System", false, 101)]
        public static void CreateEventSystem()
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();

            Selection.activeGameObject = eventSystemObj;
            Debug.Log("[EventSystemFixer] EventSystem created!");
        }

        [MenuItem("Tools/VampirDek/Add Event System Manager", false, 102)]
        public static void AddEventSystemManager()
        {
            EventSystemManager existing = Object.FindObjectOfType<EventSystemManager>();
            if (existing != null)
            {
                Debug.Log("[EventSystemFixer] EventSystemManager already exists!");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            GameObject managerObj = new GameObject("EventSystemManager");
            managerObj.AddComponent<EventSystemManager>();

            Selection.activeGameObject = managerObj;
            Debug.Log("[EventSystemFixer] EventSystemManager created!");
        }
    }
}
#endif
