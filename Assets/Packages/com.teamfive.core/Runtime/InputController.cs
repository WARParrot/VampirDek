using UnityEngine;
using UnityEngine.InputSystem;

namespace Core
{
    public class InputController : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;
        public const string CombatMap = "Combat";
        public const string ExplorationMap = "Exploration";
        public const string UIMap = "UI";

        private PlayerInput _playerInput;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>() ?? gameObject.AddComponent<PlayerInput>();
            _playerInput.actions = _inputActions;
        }

        private void OnEnable()
        {
            try
            {
                GlobalServices.EventBus?.Subscribe<ConsoleToggledEvent>(OnConsoleToggled);
            }
            catch { }
        }

        private void OnDisable()
        {
            try
            {
                GlobalServices.EventBus?.Unsubscribe<ConsoleToggledEvent>(OnConsoleToggled);
            }
            catch { }
        }

        private void OnConsoleToggled(ConsoleToggledEvent evt)
        {
            if (_inputActions == null) return;
            if (evt.IsOpen)
                _inputActions.Disable();
            else
                _inputActions.Enable();
        }

        public void EnableCombatMap() => _playerInput.SwitchCurrentActionMap(CombatMap);
        public void EnableExplorationMap() => _playerInput.SwitchCurrentActionMap(ExplorationMap);
        public void EnableUIMap() => _playerInput.SwitchCurrentActionMap(UIMap);

        public InputAction GetAction(string actionPath)
        {
            var parts = actionPath.Split('/');
            if (parts.Length != 2)
            {
                Debug.LogWarning($"Invalid action path: {actionPath}. Expected 'mapName/actionName'.");
                return null;
            }

            var map = _inputActions?.FindActionMap(parts[0]);
            if (map == null)
            {
                Debug.LogWarning($"Action map '{parts[0]}' not found.");
                return null;
            }

            var action = map.FindAction(parts[1]);
            if (action == null)
                Debug.LogWarning($"Action '{parts[1]}' not found in map '{parts[0]}'.");

            return action;
        }
    }
}