using UnityEngine;
using UnityEngine.UI;

namespace Exploration
{
    /// <summary>
    /// Простой тестовый скрипт для проверки UI
    /// </summary>
    public class SimpleUITest : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[SimpleUITest] Script started!");

            // Проверяем Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            Debug.Log($"[SimpleUITest] Canvas: {(canvas != null ? canvas.name : "NULL")}");

            // Проверяем Text компоненты
            Text[] texts = GetComponentsInChildren<Text>(true);
            Debug.Log($"[SimpleUITest] Found {texts.Length} Text components");

            foreach (var text in texts)
            {
                Debug.Log($"[SimpleUITest] Text on '{text.gameObject.name}': '{text.text}' - Active: {text.gameObject.activeSelf}");

                // Принудительно устанавливаем текст
                text.text = "ТЕСТ - Если видите это, UI работает!";
                text.color = Color.yellow;
                text.fontSize = 30;
            }

            // Проверяем CanvasGroup
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                Debug.Log($"[SimpleUITest] CanvasGroup alpha: {cg.alpha}");
                cg.alpha = 1f; // Принудительно делаем видимым
                Debug.Log("[SimpleUITest] Set CanvasGroup alpha to 1");
            }

            // Проверяем активность
            Debug.Log($"[SimpleUITest] GameObject '{gameObject.name}' active: {gameObject.activeSelf}");
            Debug.Log($"[SimpleUITest] GameObject activeInHierarchy: {gameObject.activeInHierarchy}");
        }
    }
}
