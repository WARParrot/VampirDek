using UnityEngine;
using UnityEngine.UI;

namespace Exploration
{
    /// <summary>
    /// Отображает подсказку при наведении на интерактивные объекты
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private Text _promptText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 10f;

        private bool _isVisible = false;
        private bool _isFading = false;
        private float _targetAlpha = 0f;
        private string _lastPromptText;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
            enabled = false;
        }

        private void Update()
        {
            if (!_isFading || _canvasGroup == null) return;

            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            if (Mathf.Approximately(_canvasGroup.alpha, _targetAlpha))
            {
                _canvasGroup.alpha = _targetAlpha;
                _isFading = false;
                enabled = false;
            }
        }

        /// <summary>
        /// Показывает подсказку с указанным текстом
        /// </summary>
        public void Show(string promptText)
        {
            if (_promptText != null && promptText != _lastPromptText)
            {
                _promptText.text = promptText;
                _lastPromptText = promptText;
            }

            if (_isVisible && Mathf.Approximately(_targetAlpha, 1f)) return;

            _targetAlpha = 1f;
            _isVisible = true;
            _isFading = true;
            enabled = true;
        }

        /// <summary>
        /// Скрывает подсказку
        /// </summary>
        public void Hide()
        {
            // Guard against being called from another component's OnDisable AFTER this UI has
            // already been destroyed (Unity tears scene objects down in unspecified order on
            // scene unload — ExplorationController.OnDisable used to hit this MRE every reload).
            if (this == null) return;
            if (!_isVisible && _targetAlpha <= 0f) return;

            _targetAlpha = 0f;
            _isVisible = false;
            _isFading = true;
            enabled = true;
        }

        public bool IsVisible => _isVisible;
    }
}
