using TMPro;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// UI компонент для отображения сообщений обучения
    /// </summary>
    public class TutorialMessageUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 5f;

        private float _targetAlpha = 0f;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            if (_canvasGroup.alpha != _targetAlpha)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            }
        }

        /// <summary>
        /// Показывает сообщение обучения
        /// </summary>
        public void ShowMessage(string message)
        {
            Debug.Log($"[TutorialMessageUI] ShowMessage called: {message}");

            if (_messageText != null)
            {
                _messageText.text = message;
                Debug.Log($"[TutorialMessageUI] Text set to: {message}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _messageText is NULL!");
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f; // Мгновенный показ вместо fade
                _targetAlpha = 1f;
                Debug.Log($"[TutorialMessageUI] CanvasGroup alpha set to 1, gameObject active: {gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _canvasGroup is NULL!");
            }
        }

        /// <summary>
        /// Скрывает сообщение
        /// </summary>
        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _targetAlpha = 0f;
            }
        }
    }
}
