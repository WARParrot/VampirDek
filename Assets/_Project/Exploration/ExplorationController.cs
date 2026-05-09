using UnityEngine;
using Core;

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

        private CharacterController _cc;
        private Camera _camera;
        private InputController _input;
        private Controls _controls;
        private bool _isActive;

        private Vector2 _moveInput;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _camera = Camera.main;
            _input = GlobalServices.Resolver.Resolve<InputController>();
            _controls = new Controls();
        }

        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            _input.EnableExplorationMap();
            _controls.Exploration.Enable();
            _controls.Exploration.Move.performed += OnMove;
            _controls.Exploration.Move.canceled += OnMove;
            _controls.Exploration.Interact.performed += OnInteract;
        }

        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            _controls.Exploration.Move.performed -= OnMove;
            _controls.Exploration.Move.canceled -= OnMove;
            _controls.Exploration.Interact.performed -= OnInteract;
            _controls.Exploration.Disable();
        }

        private void Update()
        {
            if (!_isActive) return;
            HandleMovement();
        }

        private void HandleMovement()
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
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
        }

        private void OnMove(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            _moveInput = ctx.ReadValue<Vector2>();
        }

        private void OnInteract(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
        {
            TryInteract();
        }

        private void TryInteract()
        {
            if (Physics.Raycast(
                _camera.transform.position,
                _camera.transform.forward,
                out RaycastHit hit,
                _interactRange,
                _interactMask))
            {
                var interactable = hit.collider.GetComponent<IInteractable>();
                interactable?.Interact();
            }
        }
    }
}