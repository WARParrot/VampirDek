using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

namespace Exploration
{
    [RequireComponent(typeof(CharacterController))]
    public class ExplorationController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;

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

        private Renderer[] _renderers;
        private InputController _input;
        private CharacterController _cc;
        private Camera _camera;

        private InputAction _moveAction;
        private InputAction _interactAction;
        private InputAction _startDuelAction;

        private Vector2 _moveInput;
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
            _interactAction = map.FindAction("Interact");
            _startDuelAction = map.FindAction("StartDuel");

            if (_moveAction == null) Debug.LogError("[ExplorationController] Action 'Move' missing!", this);
            if (_interactAction == null) Debug.LogError("[ExplorationController] Action 'Interact' missing!", this);
            if (_startDuelAction == null) Debug.LogError("[ExplorationController] Action 'StartDuel' missing!", this);
        }

        private void LateUpdate()
        {
            if (!_isActive || _camera == null) return;
            _camera.transform.position = transform.position + Vector3.up * 0.9f;
            _camera.transform.rotation = transform.rotation;
        }

        public void Activate()
        {
            if (_isActive || !enabled) return;
            _isActive = true;

            SetVisible(true);

            _input.EnableExplorationMap();
            _moveAction?.Enable();
            _interactAction?.Enable();
            _startDuelAction?.Enable();

            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            _interactAction.performed += OnInteract;
            _startDuelAction.performed += OnStartDuel;
        }

        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            SetVisible(false);

            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
            _interactAction.performed -= OnInteract;
            _startDuelAction.performed -= OnStartDuel;

            _moveAction?.Disable();
            _interactAction?.Disable();
            _startDuelAction?.Disable();
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
            Vector3 forward = _camera.transform.forward;
            Vector3 right = _camera.transform.right;
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();

            Vector3 desiredMove = forward * _moveInput.y + right * _moveInput.x;
            _cc.Move(desiredMove * (_walkSpeed * Time.deltaTime));

            if (desiredMove.sqrMagnitude > 0.01f && _moveInput.y >= 0)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMove);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            if (desiredMove.sqrMagnitude > 0.01f)
                RuntimeManager.PlayOneShot("event:/Exploration/Player/Footsteps", _camera.transform.position);
        }

        private void OnMove(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();

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