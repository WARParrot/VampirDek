using UnityEngine;
using UnityEngine.UI;
using Exploration;

#if UNITY_EDITOR
using UnityEditor;

namespace Exploration.Editor
{
    /// <summary>
    /// Автоматически создает UI для MovementTutorial
    /// </summary>
    public static class MovementTutorialSetup
    {
        [MenuItem("GameObject/UI/VampirDek/Movement Tutorial UI", false, 12)]
        public static void CreateMovementTutorialUI()
        {
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
            GameObject panel = new GameObject("MovementTutorialPanel");
            panel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(620, 220);
            panelRect.anchoredPosition = new Vector2(0, -130);

            // Фон
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0, 0, 0, 0.8f);

            // CanvasGroup
            CanvasGroup canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;

            // Текст инструкции
            GameObject textObj = new GameObject("InstructionText");
            textObj.transform.SetParent(panel.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.3f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.sizeDelta = new Vector2(-20, -10);
            textRect.anchoredPosition = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.text = "Инструкция";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 22;
            text.color = Color.white;

            // Изображение клавиши
            GameObject imageObj = new GameObject("KeyHintImage");
            imageObj.transform.SetParent(panel.transform, false);

            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0f);
            imageRect.anchorMax = new Vector2(0.5f, 0.3f);
            imageRect.sizeDelta = new Vector2(60, 60);
            imageRect.anchoredPosition = Vector2.zero;

            Image keyImage = imageObj.AddComponent<Image>();
            keyImage.color = Color.white;

            // Компонент
            MovementTutorial tutorial = panel.AddComponent<MovementTutorial>();

            SerializedObject serializedObject = new SerializedObject(tutorial);
            serializedObject.FindProperty("_tutorialPanel").objectReferenceValue = panel;
            serializedObject.FindProperty("_instructionText").objectReferenceValue = text;
            serializedObject.FindProperty("_keyHintImage").objectReferenceValue = keyImage;
            serializedObject.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            serializedObject.FindProperty("_fadeSpeed").floatValue = 2f;
            serializedObject.FindProperty("_startOnAwake").boolValue = true;
            serializedObject.ApplyModifiedProperties();

            // Активируем панель, чтобы обучение запустилось
            panel.SetActive(true);

            Selection.activeGameObject = panel;
            Debug.Log("[Setup] MovementTutorialUI создан! Настройте массив Steps в Inspector.");
        }
    }
}
#endif
