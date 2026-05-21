using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Core;

namespace Core
{
    public class DevConsole : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject consolePanel;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI logText;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button toggleLogsButton;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        private static readonly List<string> _logs = new();
        private const int MaxLogs = 500;
        private Dictionary<string, Action<string[]>> _commands;
        private bool _showGameLogs = false;
        private List<string> _commandLogs = new();
        private List<string> _gameLogs = new();

        private void Awake()
        {
            Debug.Log("DevConsole Awake called");
            DontDestroyOnLoad(gameObject);
            consolePanel.SetActive(false);
            Application.logMessageReceived += OnUnityLog;

            int size = 24;
            logText.fontSize = size;
            inputField.textComponent.fontSize = size;

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

            toggleLogsButton.onClick.AddListener(ToggleLogs);
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

            if (consolePanel.activeSelf && Keyboard.current?.ctrlKey.isPressed == true)
            {
                var scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float newSize = logText.fontSize + Mathf.Sign(scroll) * 2f;
                    newSize = Mathf.Clamp(newSize, 8f, 48f);
                    logText.fontSize = newSize;

                    if (inputField?.textComponent != null)
                        inputField.textComponent.fontSize = newSize;
                }
            }
        }

        private void TogglePanel()
        {
            consolePanel.SetActive(!consolePanel.activeSelf);
            GlobalServices.EventBus.Publish(new ConsoleToggledEvent { IsOpen = consolePanel.activeSelf });
            if (consolePanel.activeSelf)
            {
                inputField?.ActivateInputField();
            }
            SetPlayerInput(!consolePanel.activeSelf);
        }

        private void SetPlayerInput(bool enabled)
        {
            if (inputActions != null)
            {
                if (enabled)
                    inputActions.Enable();
                else
                    inputActions.Disable();
            }
        }

        private void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            
            var commandLine = $"<color=#00CCCC>> {input}</color>";
            _commandLogs.Add(commandLine);
            if (_commandLogs.Count > MaxLogs) _commandLogs.RemoveAt(0);
            
            _captureResponse = true;
            
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLower().TrimStart('/');
            var args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();

            if (_commands.TryGetValue(command, out var handler))
                handler(args);
            else
                Debug.Log($"Unknown command: {command}. Type help for available commands.");
            
            _captureResponse = false;
            UpdateLogDisplay();
        }

        private bool _captureResponse = false;

        private void HelpCommand()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#FFCC00>=== DEV CONSOLE COMMANDS ===</color>");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>INFO</color>");
            sb.AppendLine("  echo <message>     - Print message");
            sb.AppendLine("  clear              - Clear current log tab");
            sb.AppendLine("  time <scale>       - Set Time.timeScale (0-10)");
            sb.AppendLine("  scenes             - List loaded scenes");
            sb.AppendLine("  quit               - Quit application");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>DUEL INFO</color>");
            sb.AppendLine("  duel state         - Turn, phase, Town HP");
            sb.AppendLine("  duel cards         - Cards on player board");
            sb.AppendLine("  duel hand          - Cards in player hand");
            sb.AppendLine("  duel deck          - Cards in player deck");
            sb.AppendLine("  duel mana          - Mana for both sides");
            sb.AppendLine("  duel resources     - Human resources");
            sb.AppendLine("  duel listcards     - List available card names");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>BOARD MANIPULATION</color>");
            sb.AppendLine("  duel kill <slot>   - Kill card in slot (vanguard_0, building_1, town)");
            sb.AppendLine("  duel heal <slot> <amount>  - Heal card");
            sb.AppendLine("  duel damage <slot> <amount> - Damage card");
            sb.AppendLine("  duel buff <slot> <amount>   - Buff card attack");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>HAND MANIPULATION</color>");
            sb.AppendLine("  duel draw <count>  - Draw cards from deck");
            sb.AppendLine("  duel discard <index> - Discard card by hand index");
            sb.AppendLine("  duel discard random - Discard random card");
            sb.AppendLine("  duel addcard <name> - Add card to hand by name");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>DECK MANIPULATION</color>");
            sb.AppendLine("  duel shuffle       - Shuffle deck");
            sb.AppendLine("  duel deckadd <name> - Add card to deck by name");
            sb.AppendLine();
            sb.AppendLine("<color=#00CCFF>RESOURCES</color>");
            sb.AppendLine("  duel manaset <amount>  - Set player mana");
            sb.AppendLine("  duel manaadd <amount>  - Add player mana");
            sb.AppendLine("  duel humanset <amount> - Set player humans");
            sb.AppendLine("  duel humanadd <amount> - Add player humans");
            Debug.Log(sb.ToString());
        }

        private void EchoCommand(string[] args) =>
            Debug.Log(args.Length > 0 ? string.Join(" ", args) : "Echo: nothing to say.");

        private void ClearCommand()
        {
            if (_showGameLogs)
                _gameLogs.Clear();
            else
                _commandLogs.Clear();
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
            if (args.Length == 0)
            {
                Debug.Log("Usage: duel state|cards|hand|deck|mana|resources|kill|heal|damage|buff|draw|discard|addcard|shuffle|deckadd|manaset|manaadd|humanset|humanadd");
                return;
            }

            var sub = args[0].ToLower();

            switch (sub)
            {
                case "state":     Debug.Log(CallCombatCommand("GetDuelStateInfo")); break;
                case "cards":     Debug.Log(CallCombatCommand("GetBoardCardsInfo")); break;
                case "hand":      Debug.Log(CallCombatCommand("GetHandInfo")); break;
                case "deck":      Debug.Log(CallCombatCommand("GetDeckInfo")); break;
                case "mana":      Debug.Log(CallCombatCommand("GetManaInfo")); break;
                case "resources": Debug.Log(CallCombatCommand("GetResourcesInfo")); break;
                case "cardslist": Debug.Log(CallCombatCommand("ListCards")); break;

                case "kill":    Debug.Log(CallCombatCommand("KillCard", args.Length > 1 ? args[1] : "")); break;
                case "heal":    Debug.Log(CallCombatCommand("HealCard", args.Length > 1 ? args[1] : "", IntArg(args, 2))); break;
                case "damage":  Debug.Log(CallCombatCommand("DamageCard", args.Length > 1 ? args[1] : "", IntArg(args, 2))); break;
                case "buff":    Debug.Log(CallCombatCommand("BuffCard", args.Length > 1 ? args[1] : "", IntArg(args, 2))); break;

                case "draw":    Debug.Log(CallCombatCommand("DrawCards", IntArg(args, 1))); break;
                case "discard":
                    if (args.Length > 1 && args[1].ToLower() == "random")
                        Debug.Log(CallCombatCommand("DiscardRandomCard"));
                    else
                        Debug.Log(CallCombatCommand("DiscardCard", IntArg(args, 1)));
                    break;
                case "addcard": Debug.Log(CallCombatCommand("AddCardToHand", args.Length > 1 ? args[1] : "")); break;

                case "shuffle":  Debug.Log(CallCombatCommand("ShuffleDeck")); break;
                case "deckadd":  Debug.Log(CallCombatCommand("AddCardToDeck", args.Length > 1 ? args[1] : "")); break;

                case "manaset":  Debug.Log(CallCombatCommand("SetMana", IntArg(args, 1))); break;
                case "manaadd":  Debug.Log(CallCombatCommand("AddMana", IntArg(args, 1))); break;
                case "humanset": Debug.Log(CallCombatCommand("SetHumans", IntArg(args, 1))); break;
                case "humanadd": Debug.Log(CallCombatCommand("AddHumans", IntArg(args, 1))); break;

                default:
                    Debug.Log($"Unknown subcommand: {sub}");
                    break;
            }
        }

        private int IntArg(string[] args, int index)
        {
            if (index < args.Length && int.TryParse(args[index], out int val)) return val;
            return 0;
        }

        private string CallCombatCommand(string methodName, params object[] parameters)
        {
            var type = System.Type.GetType("Combat.UI.DuelConsoleCommands, Combat");
            if (type == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType("Combat.UI.DuelConsoleCommands");
                    if (type != null) break;
                }
            }
            if (type == null) return $"[DevConsole] Type not found.";
            var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null) return $"[DevConsole] Method '{methodName}' not found.";
            try
            {
                return method.Invoke(null, parameters)?.ToString() ?? "null";
            }
            catch (Exception e)
            {
                return $"Error: {e.InnerException?.Message ?? e.Message}";
            }
        }

        private void QuitCommand()
        {
            Debug.Log("Quitting...");
            Application.Quit();
        }

        private void OnUnityLog(string message, string stackTrace, LogType type)
        {
            if (_captureResponse)
            {
                _commandLogs.Add($"<color=#FFFFFF>  {message}</color>");
                if (_commandLogs.Count > MaxLogs) _commandLogs.RemoveAt(0);
                if (!_showGameLogs) UpdateLogDisplay();
                return;
            }

            string color = type switch
            {
                LogType.Error   => "#FF4444",
                LogType.Warning => "#FFAA00",
                _               => "#FFFFFF"
            };

            var formatted = $"<color={color}>[{type}] {message}</color>";
            _gameLogs.Add(formatted);
            if (_gameLogs.Count > MaxLogs) _gameLogs.RemoveAt(0);
            UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            if (_showGameLogs)
                logText.text = string.Join("\n", _gameLogs);
            else
                logText.text = string.Join("\n", _commandLogs);
            
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void ToggleLogs()
        {
            _showGameLogs = !_showGameLogs;
            UpdateLogDisplay();
        }
    }
}