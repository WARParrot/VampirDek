using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private Image _backgroundImage;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _backgroundImage = GetComponent<Image>();
            if (_backgroundImage == null)
                _backgroundImage = gameObject.AddComponent<Image>();
            _backgroundImage.color = new Color(0.02f, 0.018f, 0.012f, 0.92f);
            _backgroundImage.raycastTarget = false;

            EnsureReadableTextArea();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
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
                EnsureReadableTextArea();
                _messageText.text = message;
                Debug.Log($"[TutorialMessageUI] Text set to: {message}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _messageText is NULL!");
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _targetAlpha = 1f;
                Debug.Log($"[TutorialMessageUI] CanvasGroup alpha set to 1, gameObject active: {gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _canvasGroup is NULL!");
            }
        }

        private void EnsureReadableTextArea()
        {
            if (_messageText == null)
                _messageText = GetComponentInChildren<TextMeshProUGUI>(true);

            var panelRect = transform as RectTransform;
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0f);
                panelRect.anchorMax = new Vector2(0.5f, 0f);
                panelRect.pivot = new Vector2(0.5f, 0f);
                panelRect.anchoredPosition = new Vector2(0f, 28f);
                panelRect.sizeDelta = new Vector2(980f, Mathf.Max(panelRect.sizeDelta.y, 320f));
            }

            if (_messageText == null) return;

            var textRect = _messageText.transform as RectTransform;
            if (textRect != null)
            {
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(36f, 30f);
                textRect.offsetMax = new Vector2(-36f, -30f);
            }

            _messageText.raycastTarget = false;
            _messageText.enableWordWrapping = true;
            _messageText.overflowMode = TextOverflowModes.Overflow;
            _messageText.enableAutoSizing = true;
            _messageText.fontSizeMin = 24f;
            _messageText.fontSizeMax = 36f;
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.color = Color.white;
        }

        /// <summary>
        /// Скрывает сообщение
        /// </summary>
        public void Hide()
        {
            if (_canvasGroup != null)
            {
                _targetAlpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }
    }
}
