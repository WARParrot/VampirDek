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
        private float _targetAlpha = 0f;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            _canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (_canvasGroup.alpha != _targetAlpha)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Показывает подсказку с указанным текстом
        /// </summary>
        public void Show(string promptText)
        {
            _promptText.text = promptText;
            _targetAlpha = 1f;
            _isVisible = true;
        }

        /// <summary>
        /// Скрывает подсказку
        /// </summary>
        public void Hide()
        {
            _targetAlpha = 0f;
            _isVisible = false;
        }

        public bool IsVisible => _isVisible;
    }
}
