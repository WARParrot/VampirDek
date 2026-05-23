using UnityEngine;

namespace Exploration
{
    public class OpenableObject : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private Transform _door;
        [SerializeField] private Vector3 _openRotation = new Vector3(0f, -90f, 0f);
        [SerializeField] private float _animationTime = 0.5f;

        [Header("Contents")]
        [SerializeField] private GameObject _contents;   // шкатулка или другой объект внутри

        [Header("Prompts")]
        [SerializeField] private string _promptOpen = "Open";
        [SerializeField] private string _promptClose = "Close";

        private bool _isOpen;
        private Quaternion _closedRotation;

        public string PromptText => _isOpen ? _promptClose : _promptOpen;

        private void Awake()
        {
            if (_door != null)
                _closedRotation = _door.localRotation;
            if (_contents != null)
                _contents.SetActive(false);   // изначально скрыто
        }

        public void Interact(ExplorationController player)
        {
            if (_door == null) return;
            _isOpen = !_isOpen;

            Quaternion targetRotation = _isOpen
                ? Quaternion.Euler(_openRotation)
                : _closedRotation;

#if DOTWEEN
            _door.DOLocalRotateQuaternion(targetRotation, _animationTime);
#else
            _door.localRotation = targetRotation;
#endif

            if (_contents != null)
                _contents.SetActive(_isOpen);
        }
    }
}