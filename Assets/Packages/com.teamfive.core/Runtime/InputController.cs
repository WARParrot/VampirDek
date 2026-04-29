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

        public void EnableCombatMap() => _playerInput.SwitchCurrentActionMap(CombatMap);
        public void EnableExplorationMap() => _playerInput.SwitchCurrentActionMap(ExplorationMap);
        public void EnableUIMap() => _playerInput.SwitchCurrentActionMap(UIMap);
    }
}
