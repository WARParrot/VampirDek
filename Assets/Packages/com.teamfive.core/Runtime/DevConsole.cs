using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Core
{
    public class DevConsole : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private TextMeshProUGUI _outputText;

        private void Awake()
        {
            _inputField.onSubmit.AddListener(Execute);
            _panel.SetActive(false);
        }

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf) _inputField.ActivateInputField();
        }

        private void Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            var parts = input.Split(' ', 2);
            var command = parts[0].ToLower();
            var arg = parts.Length > 1 ? parts[1] : null;

            switch (command)
            {
                case "help":
                    AppendOutput("Commands: help, echo <msg>");
                    break;
                case "echo":
                    AppendOutput(arg ?? string.Empty);
                    break;
                default:
                    AppendOutput($"Unknown command: {command}");
                    break;
            }
            _inputField.text = "";
            _inputField.ActivateInputField();
        }

        private void AppendOutput(string msg) => _outputText.text += $"{msg}\n";
    }
}
