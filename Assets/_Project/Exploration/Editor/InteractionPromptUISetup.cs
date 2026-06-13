using UnityEngine;
using UnityEngine.UI;
using Exploration;

#if UNITY_EDITOR
using UnityEditor;

namespace Exploration.Editor
{
    /// <summary>
    /// Автоматически создает UI для InteractionPromptUI
    /// </summary>
    public static class InteractionPromptUISetup
    {
        [MenuItem("GameObject/UI/VampirDek/Interaction Prompt UI", false, 11)]
        public static void CreateInteractionPromptUI()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Создать панель
            GameObject panel = new GameObject("InteractionPromptPanel");
            panel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.sizeDelta = new Vector2(300, 60);
            panelRect.anchoredPosition = new Vector2(0, 120);

            // Фон
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.7f);

            // CanvasGroup
            CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            // Текст
            GameObject textObj = new GameObject("PromptText");
            textObj.transform.SetParent(panel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-20, -10);
            textRect.anchoredPosition = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = "Нажмите [E] для взаимодействия";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            // Компонент
            InteractionPromptUI promptUI = panel.AddComponent<InteractionPromptUI>();

            SerializedObject serializedObject = new SerializedObject(promptUI);
            serializedObject.FindProperty("_promptText").objectReferenceValue = text;
            serializedObject.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_fadeSpeed").floatValue = 10f;
            serializedObject.ApplyModifiedProperties();

            Selection.activeGameObject = panel;
            Debug.Log("[Setup] InteractionPromptUI создан! Привяжите его в ExplorationController.");
        }
    }
}
#endif
