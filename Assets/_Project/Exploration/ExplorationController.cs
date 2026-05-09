using Core;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

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
        [SerializeField] private InputActionAsset _inputActions;

        private InputController _input;
        private CharacterController _cc;
        private Camera _camera;
        private InputAction _moveAction;
        private InputAction _interactAction;
        private Vector2 _moveInput;
        private bool _isActive;

        [Inject]
        private void Construct(InputController inputController)
        {
            _input = inputController;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _camera = Camera.main;

            var map = _inputActions.FindActionMap("Exploration");
            _moveAction = map.FindAction("Move");
            _interactAction = map.FindAction("Interact");
        }

        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            _input.EnableExplorationMap();
            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMove;
            _interactAction.performed += OnInteract;
            _moveAction.Enable();
            _interactAction.Enable();
        }

        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMove;
            _interactAction.performed -= OnInteract;
            _moveAction.Disable();
            _interactAction.Disable();
        }

        private void OnDisable()
        {
            if (_isActive) Deactivate();
        }

        private void Update()
        {
            if (!_isActive) return;
            ApplyMovement();
        }

        private void ApplyMovement()
        {
            Vector3 forward = _camera.transform.forward;
            Vector3 right = _camera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 desiredMove = forward * _moveInput.y + right * _moveInput.x;
            _cc.Move(desiredMove * (_walkSpeed * Time.deltaTime));

            if (desiredMove.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(desiredMove);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            _moveInput = ctx.ReadValue<Vector2>();
        }

        private void OnInteract(InputAction.CallbackContext ctx)
        {
            TryInteract();
        }

        private void TryInteract()
        {
            if (Physics.Raycast(_camera.transform.position, _camera.transform.forward, out RaycastHit hit, _interactRange, _interactMask))
            {
                var interactable = hit.collider.GetComponent<IInteractable>();
                interactable?.Interact(this);
            }
        }
    }
}