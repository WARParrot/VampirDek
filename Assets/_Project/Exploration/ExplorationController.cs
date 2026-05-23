using Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Exploration
{
    [RequireComponent(typeof(CharacterController))]
    public class ExplorationController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 5f;

        [Header("Camera")]
        [SerializeField] private float _mouseSensitivity = 0.1f;
        [SerializeField] private float _eyeHeight = 0.8f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("Interaction")]
        [SerializeField] private float _interactRange = 4f;
        [SerializeField] private LayerMask _interactMask = -1;

        [Header("Encounter")]
        [SerializeField] private float _encounterStartRange = 3f;
        [SerializeField] private LayerMask _encounterMask = -1;

        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Visuals")]
        [SerializeField] private GameObject _visualRoot;

        private InputController _input;
        private CharacterController _cc;
        private Camera _camera;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _interactAction;
        private InputAction _startDuelAction;

        private Vector2 _moveInput;
        private Vector2 _lookDelta;
        private float _yaw;
        private float _pitch;
        private bool _isActive;

        private void Awake()
        {
            _input = Object.FindAnyObjectByType<InputController>();
            if (_input == null)
            {
                Debug.LogError("[ExplorationController] InputController not found in scene. Disabling.");
                enabled = false;
                return;
            }

            _cc = GetComponent<CharacterController>();
            _camera = Camera.main;

            if (_inputActions == null)
            {
                Debug.LogError("[ExplorationController] InputActionAsset not assigned. Disabling.", this);
                enabled = false;
                return;
            }

            var map = _inputActions.FindActionMap("Exploration");
            if (map == null)
            {
                Debug.LogError("[ExplorationController] Action map 'Exploration' not found.", this);
                enabled = false;
                return;
            }

            _moveAction = map.FindAction("Move");
            _lookAction = map.FindAction("Look");
            _interactAction = map.FindAction("Interact");
            _startDuelAction = map.FindAction("StartDuel");

            if (_moveAction == null) Debug.LogError("[ExplorationController] Action 'Move' missing!", this);
            if (_lookAction == null) Debug.LogError("[ExplorationController] Action 'Look' missing! Mouse look will not work.", this);
            if (_interactAction == null) Debug.LogError("[ExplorationController] Action 'Interact' missing!", this);
            if (_startDuelAction == null) Debug.LogError("[ExplorationController] Action 'StartDuel' missing!", this);

            // Инициализируем углы из текущего поворота камеры
            if (_camera != null)
            {
                Vector3 euler = _camera.transform.rotation.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
            }
        }

        private void LateUpdate()
        {
            if (!_isActive || _camera == null) return;

            // Обновление взгляда
            _yaw += _lookDelta.x * _mouseSensitivity;
            _pitch -= _lookDelta.y * _mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            _lookDelta = Vector2.zero;

            // Поворачиваем персонажа по горизонтали (для корректного направления движения)
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            // Позиция камеры на уровне глаз
            _camera.transform.position = transform.position + new Vector3(0f, _eyeHeight, 0f);
            // Поворот камеры
            _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        public void Activate()
        {
            if (_isActive || !enabled) return;

            // Перестраховка: ищем InputController, если потерялся
            if (_input == null)
            {
                _input = Object.FindAnyObjectByType<InputController>();
                if (_input == null)
                {
                    Debug.LogError("[ExplorationController] InputController not found. Cannot activate.");
                    return;
                }
            }

            // Проверяем, что все экшены найдены
            if (_moveAction == null || _lookAction == null || _interactAction == null || _startDuelAction == null)
            {
                Debug.LogError("[ExplorationController] Some input actions are missing. Check InputActionAsset.");
                return;
            }

            _isActive = true;
            SetVisible(true);

            _input.EnableExplorationMap();

            _moveAction.Enable();
            _lookAction.Enable();
            _interactAction.Enable();
            _startDuelAction.Enable();

            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            _lookAction.performed += OnLook;
            _lookAction.canceled += OnLook;
            _interactAction.performed += OnInteract;
            _startDuelAction.performed += OnStartDuel;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            SetVisible(false);

            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
            _lookAction.performed -= OnLook;
            _lookAction.canceled -= OnLook;
            _interactAction.performed -= OnInteract;
            _startDuelAction.performed -= OnStartDuel;

            _moveAction?.Disable();
            _lookAction?.Disable();
            _interactAction?.Disable();
            _startDuelAction?.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SetVisible(bool visible)
        {
            if (_visualRoot != null)
                _visualRoot.SetActive(visible);
            if (_cc != null)
                _cc.enabled = visible;
        }

        private void OnDisable() { if (_isActive) Deactivate(); }

        private void Update()
        {
            if (!_isActive || GlobalServices.IsMenuOpen) return;
            ApplyMovement();
        }

        private void ApplyMovement()
        {
            Vector3 camForward = _camera.transform.forward;
            Vector3 camRight = _camera.transform.right;
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 desiredMove = camForward * _moveInput.y + camRight * _moveInput.x;
            _cc.Move(desiredMove * (_walkSpeed * Time.deltaTime));
        }

        private void OnMove(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
        private void OnLook(InputAction.CallbackContext ctx) => _lookDelta += ctx.ReadValue<Vector2>();
        private void OnInteract(InputAction.CallbackContext ctx) => TryInteract();

        private async void OnStartDuel(InputAction.CallbackContext ctx)
        {
            if (GlobalServices.IsMenuOpen) return;

            var hits = Physics.OverlapSphere(transform.position, _encounterStartRange, _encounterMask);
            foreach (var hit in hits)
            {
                var point = hit.GetComponent<EncounterPoint>();
                if (point != null)
                {
                    await point.StartDuelAsync();
                    return;
                }
            }
        }

        private void TryInteract()
        {
            if (GlobalServices.IsMenuOpen) return;

            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward,
                out RaycastHit hit, _interactRange, _interactMask))
            {
                Debug.Log($"Hit interactable: {hit.collider.name}.");
                var interactable = hit.collider.GetComponent<IInteractable>();
                interactable?.Interact(this);
            }
            else
            {
                Debug.Log("No interactable hit.");
            }
        }

        public void SetPosition(Vector3 position, Quaternion rotation)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = position;
            transform.rotation = rotation;
            if (cc != null) cc.enabled = true;
        }
    }
}