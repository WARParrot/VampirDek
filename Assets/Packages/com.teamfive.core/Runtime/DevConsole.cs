using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Core
{
    public class DevConsole : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject consolePanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect scrollRect;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private static readonly List<string> _logs = new();
        private const int MaxLogs = 500;
        private Dictionary<string, Action<string[]>> _commands;

        private void Awake()
        {
            Debug.Log("DevConsole Awake called");
            DontDestroyOnLoad(gameObject);
            consolePanel.SetActive(false);
            Application.logMessageReceived += OnUnityLog;

            _commands = new Dictionary<string, Action<string[]>>
            {
                { "help", _ => HelpCommand() },
                { "echo", args => EchoCommand(args) },
                { "clear", _ => ClearCommand() },
                { "time", args => TimeCommand(args) },
                { "scenes", _ => ScenesCommand() },
                { "duel", args => DuelCommand(args) },
                { "quit", _ => QuitCommand() }
            };
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        private void OnEnable()
        {
            if (inputActions != null)
            {
                var toggleAction = inputActions.FindAction("DevConsole/ToggleConsole");
                if (toggleAction != null)
                {
                    toggleAction.Enable();
                    toggleAction.performed += OnToggleConsole;
                }
                else
                {
                    Debug.LogError("ToggleConsole action not found!");
                }
            }
        }

        private void OnDisable()
        {
            if (inputActions != null)
            {
                var toggleAction = inputActions.FindAction("DevConsole/ToggleConsole");
                if (toggleAction != null)
                {
                    toggleAction.performed -= OnToggleConsole;
                    toggleAction.Disable();
                }
            }
        }

        private void OnToggleConsole(InputAction.CallbackContext ctx)
        {
            TogglePanel();
        }

        private void Update()
        {
            if (Keyboard.current?.f1Key.wasPressedThisFrame ?? false)
            {
                TogglePanel();
            }

            if (!consolePanel.activeSelf) return;

            if (Keyboard.current?.enterKey.wasPressedThisFrame ?? false)
            {
                if (!string.IsNullOrWhiteSpace(inputField?.text))
                {
                    Execute(inputField.text);
                    inputField.text = "";
                    inputField?.ActivateInputField();
                }
            }
        }

        private void TogglePanel()
        {
            consolePanel.SetActive(!consolePanel.activeSelf);
            if (consolePanel.activeSelf)
            {
                inputField?.ActivateInputField();
            }
            SetPlayerInput(!consolePanel.activeSelf);
        }

        private void SetPlayerInput(bool enabled)
        {
            var pi = FindObjectOfType<PlayerInput>();
            if (pi != null) pi.enabled = enabled;
        }

        private void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            Debug.Log($"<color=#00CCCC>> {input}</color>");

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower().TrimStart('/');
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (_commands.TryGetValue(command, out var handler))
            {
                handler(args);
            }
            else
            {
                Debug.Log($"Unknown command: {command}. Type help for available commands.");
            }
        }

        private void HelpCommand()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available commands:");
            sb.AppendLine("  help               - Show this help");
            sb.AppendLine("  echo <message>     - Print message");
            sb.AppendLine("  clear              - Clear console output");
            sb.AppendLine("  time <scale>       - Set Time.timeScale (0-10)");
            sb.AppendLine("  scenes             - List loaded scenes");
            sb.AppendLine("  duel state         - Duel turn, phase, HP");
            sb.AppendLine("  duel cards         - Cards on player board");
            sb.AppendLine("  duel hand          - Cards in player hand");
            sb.AppendLine("  duel deck          - Cards in player deck");
            sb.AppendLine("  duel mana          - Mana for both sides");
            sb.AppendLine("  duel resources     - Human resources");
            sb.AppendLine("  quit               - Quit application");
            Debug.Log(sb.ToString());
        }

        private void EchoCommand(string[] args) =>
            Debug.Log(args.Length > 0 ? string.Join(" ", args) : "Echo: nothing to say.");

        private void ClearCommand()
        {
            _logs.Clear();
            UpdateLogDisplay();
        }

        private void TimeCommand(string[] args)
        {
            if (args.Length > 0 && float.TryParse(args[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float t))
                Time.timeScale = Mathf.Clamp(t, 0f, 10f);
            Debug.Log($"Time.timeScale = {Time.timeScale}");
        }

        private void ScenesCommand()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                Debug.Log($"Scene [{i}]: {scene.name} (loaded: {scene.isLoaded})");
            }
        }

        private void DuelCommand(string[] args)
        {
            var sub = args.Length > 0 ? args[0].ToLower() : "help";
            switch (sub)
            {
                case "state":     Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetDuelStateInfo")); break;
                case "cards":     Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetBoardCardsInfo")); break;
                case "hand":      Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetHandInfo")); break;
                case "deck":      Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetDeckInfo")); break;
                case "mana":      Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetManaInfo")); break;
                case "resources": Debug.Log(CallCombatCommand("Combat.UI.DuelConsoleCommands", "GetResourcesInfo")); break;
                default:
                    Debug.Log("Usage: duel state|cards|hand|deck|mana|resources");
                    break;
            }
        }

        private string CallCombatCommand(string typeName, string methodName)
        {
            var type = System.Type.GetType(typeName + ", Combat");
            if (type == null) return $"[DevConsole] Type '{typeName}' not found.";
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null) return $"[DevConsole] Method '{methodName}' not found.";
            return method.Invoke(null, null)?.ToString() ?? "null";
        }

        private void QuitCommand()
        {
            Debug.Log("Quitting...");
            Application.Quit();
        }

        private void OnUnityLog(string message, string stackTrace, LogType type)
        {
            string color = type switch
            {
                LogType.Error   => "#FF4444",
                LogType.Warning => "#FFAA00",
                _               => "#FFFFFF"
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
    }
}