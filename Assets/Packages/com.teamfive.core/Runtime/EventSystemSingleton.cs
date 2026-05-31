using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Добавьте этот компонент на каждый EventSystem
/// Он автоматически удалит дубликаты, оставив только первый
/// </summary>
[RequireComponent(typeof(EventSystem))]
[DefaultExecutionOrder(-100)]
public class EventSystemSingleton : MonoBehaviour
{
    private static EventSystem _instance;

    private void Awake()
    {
        EventSystem eventSystem = GetComponent<EventSystem>();

        if (_instance == null)
        {
            _instance = eventSystem;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != eventSystem)
        {
            Debug.Log($"[EventSystemSingleton] Destroying duplicate EventSystem on '{gameObject.name}'");
            Destroy(gameObject);
        }
    }
}
