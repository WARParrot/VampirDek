using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Core
{
    public class DevConsole : MonoBehaviour
    {
        [SerializeField] private GameObject consolePanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect scrollRect;

        private static readonly List<string> _logs = new();
        private const int MaxLogs = 200;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            consolePanel.SetActive(false);
            Application.logMessageReceived += OnUnityLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        private void Update()
        {
            if (Keyboard.current?.backquoteKey.wasPressedThisFrame ?? false)
            {
                consolePanel.SetActive(!consolePanel.activeSelf);
                if (consolePanel.activeSelf)
                    inputField.ActivateInputField();
            }

            if (consolePanel.activeSelf && (Keyboard.current?.enterKey.wasPressedThisFrame ?? false))
            {
                Execute(inputField.text);
                inputField.text = "";
                inputField.ActivateInputField();
            }
        }

        private void OnUnityLog(string message, string stackTrace, LogType type)
        {
            string color = type switch
            {
                LogType.Error => "#FF4444",
                LogType.Warning => "#FFAA00",
                _ => "#FFFFFF"
            };

            _logs.Add($"<color={color}>[{type}] {message}</color>");
            if (_logs.Count > MaxLogs) _logs.RemoveAt(0);
            UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            logText.text = string.Join("\n", _logs);
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void Execute(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            Debug.Log($"<color=#00CCCC>> {command}</color>");

            var parts = command.Split(' ');
            switch (parts[0].ToLower())
            {
                case "help":
                    Debug.Log("Commands: help, clear, time, quit");
                    break;
                case "clear":
                    _logs.Clear();
                    UpdateLogDisplay();
                    break;
                case "time":
                    if (parts.Length > 1 && float.TryParse(parts[1], out float t))
                        Time.timeScale = t;
                    Debug.Log($"Time.timeScale = {Time.timeScale}");
                    break;
                case "quit":
                    Application.Quit();
                    break;
                default:
                    Debug.Log($"Unknown command: {parts[0]}");
                    break;
            }
        }
    }
}