using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Exploration
{
    /// <summary>
    /// Диагностический скрипт для проверки MovementTutorial
    /// </summary>
    public class MovementTutorialDebug : MonoBehaviour
    {
        private void Start()
        {
            var tutorial = GetComponent<MovementTutorial>();
            if (tutorial == null)
            {
                Debug.LogError("[Debug] MovementTutorial component not found!");
                return;
            }

            Debug.Log("[Debug] MovementTutorial found!");

            // Проверяем UI элементы через рефлексию
            var tutorialType = tutorial.GetType();

            var panelField = tutorialType.GetField("_tutorialPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var textField = tutorialType.GetField("_instructionText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var canvasGroupField = tutorialType.GetField("_canvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var stepsField = tutorialType.GetField("_steps", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var panel = panelField?.GetValue(tutorial) as GameObject;
            var text = textField?.GetValue(tutorial) as Text;
            var canvasGroup = canvasGroupField?.GetValue(tutorial) as CanvasGroup;
            var steps = stepsField?.GetValue(tutorial) as TutorialMovementStep[];

            Debug.Log($"[Debug] Panel: {(panel != null ? panel.name : "NULL")} - Active: {panel?.activeSelf}");
            Debug.Log($"[Debug] Text: {(text != null ? "Found" : "NULL")} - Text value: '{text?.text}'");
            Debug.Log($"[Debug] CanvasGroup: {(canvasGroup != null ? "Found" : "NULL")} - Alpha: {canvasGroup?.alpha}");
            Debug.Log($"[Debug] Steps: {(steps != null ? steps.Length.ToString() : "NULL")}");

            if (steps != null && steps.Length > 0)
            {
                Debug.Log($"[Debug] First step text: '{steps[0].InstructionText}'");
            }

            // Проверяем Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[Debug] No Canvas found in parents!");
            }
            else
            {
                Debug.Log($"[Debug] Canvas found: {canvas.name} - RenderMode: {canvas.renderMode}");
            }

            // Проверяем активность объекта
            Debug.Log($"[Debug] GameObject active: {gameObject.activeSelf}");
            Debug.Log($"[Debug] GameObject activeInHierarchy: {gameObject.activeInHierarchy}");
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                var tutorial = GetComponent<MovementTutorial>();
                if (tutorial != null)
                {
                    Debug.Log("[Debug] F1 pressed - forcing tutorial start");
                    tutorial.StartTutorial();
                }
            }
        }
    }
}
