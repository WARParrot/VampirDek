using System.Collections;
using UnityEngine;
using FMODUnity;
using Shared.Localization;

namespace Exploration
{
    /// <summary>
    /// Door / lid you can open by interacting. Smooth coroutine-driven swing
    /// with an AnimationCurve so the motion has weight (slow start, slight overshoot,
    /// settle) — replaces the previous DOTween path that fell back to an instant snap.
    /// </summary>
    public class OpenableObject : MonoBehaviour, IInteractable
    {
        [Header("Door")]
        [SerializeField] private Transform _door;
        [SerializeField] private Vector3 _openLocalEuler = new Vector3(0f, -100f, 0f);
        [SerializeField] private float _openDuration = 0.7f;
        [SerializeField] private float _closeDuration = 0.5f;
        [Tooltip("X=0..1 time, Y=0..1+ angle. A curve like (0,0)(0.6,1.05)(1,1) gives a satisfying swing-and-settle.")]
        [SerializeField] private AnimationCurve _ease = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2f),
            new Keyframe(0.65f, 1.06f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Contents")]
        [SerializeField] private GameObject _contents;
        [Tooltip("Delay before showing/hiding contents during the swing (0..1 normalized).")]
        [SerializeField, Range(0f, 1f)] private float _contentsRevealAt = 0.4f;

        [Header("Audio (FMOD events, optional)")]
        [SerializeField] private string _openEvent;
        [SerializeField] private string _closeEvent;

        [Header("Locking")]
        [Tooltip("If false, Interact does nothing. Wire an external puzzle to enable it (e.g. ItemUseTarget.OnUsed → SetLocked false).")]
        [SerializeField] private bool _isLocked;
        [SerializeField] private string _lockedKey = "openable.locked";
        [SerializeField] private string _lockedFallback = "Заперто.";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        [Header("Prompts")]
        [SerializeField] private string _promptOpenKey = "interaction.open";
        [SerializeField] private string _promptOpen = "Открыть";
        [SerializeField] private string _promptCloseKey = "interaction.close";
        [SerializeField] private string _promptClose = "Закрыть";

        private bool _isOpen;
        private bool _animating;
        private Quaternion _closedRotation;
        private Quaternion _openRotation;

        public string PromptText
        {
            get
            {
                if (_isLocked) return LocalizationService.T(_lockedKey, _lockedFallback);
                return _isOpen
                    ? LocalizationService.T(_promptCloseKey, _promptClose)
                    : LocalizationService.T(_promptOpenKey, _promptOpen);
            }
        }

        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;

        public void SetLocked(bool locked) => _isLocked = locked;

        private void Awake()
        {
            if (_door != null)
            {
                _closedRotation = _door.localRotation;
                _openRotation = Quaternion.Euler(_closedRotation.eulerAngles + _openLocalEuler);
            }
            if (_contents != null)
                _contents.SetActive(false);
        }

        public void Interact(ExplorationController player)
        {
            if (_animating) return;
            if (_door == null) return;

            if (_isLocked && !_isOpen)
            {
                var msg = LocalizationService.T(_lockedKey, _lockedFallback);
                if (_messageOverridePrompt != null) _messageOverridePrompt.Show(msg);
                return;
            }

            StartCoroutine(AnimateSwing(!_isOpen));
        }

        public void OpenInstant()
        {
            if (_door == null) return;
            _door.localRotation = _openRotation;
            _isOpen = true;
            if (_contents != null) _contents.SetActive(true);
        }

        public void Open()
        {
            if (_animating || _isOpen || _door == null) return;
            StartCoroutine(AnimateSwing(true));
        }

        public void Close()
        {
            if (_animating || !_isOpen || _door == null) return;
            StartCoroutine(AnimateSwing(false));
        }

        private IEnumerator AnimateSwing(bool open)
        {
            _animating = true;
            Quaternion from = _door.localRotation;
            Quaternion to = open ? _openRotation : _closedRotation;
            float duration = open ? _openDuration : _closeDuration;
            if (duration <= 0f) duration = 0.01f;

            var evt = open ? _openEvent : _closeEvent;
            if (!string.IsNullOrEmpty(evt))
                RuntimeManager.PlayOneShot(evt, _door.position);

            bool contentsToggled = false;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / duration);
                float k = _ease.Evaluate(n);
                _door.localRotation = Quaternion.SlerpUnclamped(from, to, k);

                if (!contentsToggled && n >= _contentsRevealAt && _contents != null)
                {
                    _contents.SetActive(open);
                    contentsToggled = true;
                }
                yield return null;
            }
            _door.localRotation = to;
            if (!contentsToggled && _contents != null) _contents.SetActive(open);

            _isOpen = open;
            _animating = false;
        }
    }
}
