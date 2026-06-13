using UnityEngine;
using UnityEngine.UI;
using Combat.UI;

#if UNITY_EDITOR
using UnityEditor;

namespace Combat.Editor
{
    /// <summary>
    /// Автоматически создает UI для ResourceWarningUI
    /// </summary>
    public static class ResourceWarningUISetup
    {
        [MenuItem("GameObject/UI/VampirDek/Resource Warning UI", false, 10)]
        public static void CreateResourceWarningUI()
        {
            // Найти или создать Canvas
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Создать панель
            GameObject panel = new GameObject("ResourceWarningPanel");
            panel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(400, 100);
            panelRect.anchoredPosition = Vector2.zero;

            // Добавить Image для фона
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            // Добавить CanvasGroup
            CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            // Создать текст
            GameObject textObj = new GameObject("WarningText");
            textObj.transform.SetParent(panel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = "Предупреждение";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.yellow;

            // Добавить компонент ResourceWarningUI
            ResourceWarningUI warningUI = panel.AddComponent<ResourceWarningUI>();

            // Привязать через SerializedObject
            SerializedObject serializedObject = new SerializedObject(warningUI);
            serializedObject.FindProperty("_warningText").objectReferenceValue = text;
            serializedObject.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_displayDuration").floatValue = 2f;
            serializedObject.FindProperty("_fadeDuration").floatValue = 0.3f;
            serializedObject.ApplyModifiedProperties();

            // Деактивировать панель
            panel.SetActive(false);

            Selection.activeGameObject = panel;
            Debug.Log("[Setup] ResourceWarningUI создан! Не забудьте привязать его в HandUIManager.");
        }
    }
}
#endif
