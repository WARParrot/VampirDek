using Definitions;
using Shared.UI;
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
        private GameObject _previewRoot;

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

            EnsureReadableTextArea(false);

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
            ShowMessage(message, null, null, null);
        }

        public void ShowMessage(string message, CardDef previewCard, GameObject cardPrefab, string previewCaption)
        {
            Debug.Log($"[TutorialMessageUI] ShowMessage called: {message}");

            ClearPreview();
            bool hasPreview = previewCard != null && cardPrefab != null;

            if (_messageText != null)
            {
                EnsureReadableTextArea(hasPreview);
                _messageText.text = message;
                Debug.Log($"[TutorialMessageUI] Text set to: {message}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _messageText is NULL!");
            }

            if (hasPreview)
            {
                ShowCardPreview(previewCard, cardPrefab, previewCaption);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = hasPreview;
                _canvasGroup.interactable = hasPreview;
                _targetAlpha = 1f;
                Debug.Log($"[TutorialMessageUI] CanvasGroup alpha set to 1, gameObject active: {gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[TutorialMessageUI] _canvasGroup is NULL!");
            }
        }

        private void ShowCardPreview(CardDef cardDef, GameObject cardPrefab, string previewCaption)
        {
            _previewRoot = new GameObject("TutorialCardPreview", typeof(RectTransform));
            _previewRoot.transform.SetParent(transform, false);

            var rootRect = _previewRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(1f, 0.5f);
                rootRect.anchorMax = new Vector2(1f, 0.5f);
                rootRect.pivot = new Vector2(1f, 0.5f);
                rootRect.anchoredPosition = new Vector2(-40f, 8f);
                rootRect.sizeDelta = new Vector2(320f, 420f);
            }

            var cardInstance = Instantiate(cardPrefab, _previewRoot.transform, false);
            cardInstance.name = $"TutorialPreview_{cardDef.CardName}";
            var cardRect = cardInstance.transform as RectTransform;
            if (cardRect != null)
            {
                cardRect.anchorMin = new Vector2(0.5f, 1f);
                cardRect.anchorMax = new Vector2(0.5f, 1f);
                cardRect.pivot = new Vector2(0.5f, 1f);
                cardRect.anchoredPosition = new Vector2(0f, -8f);
                cardRect.localScale = Vector3.one;
            }

            var dragHandler = cardInstance.GetComponent<Combat.UI.DragHandler>();
            if (dragHandler != null) dragHandler.enabled = false;
            var button = cardInstance.GetComponent<Button>();
            if (button != null) button.interactable = true;
            var canvasGroup = cardInstance.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                canvasGroup.alpha = 1f;
            }

            var cardView = cardInstance.GetComponent<CardView>();
            if (cardView == null) cardView = cardInstance.AddComponent<CardView>();
            cardView.Bind(new Card(cardDef, -900001));

            if (!string.IsNullOrWhiteSpace(previewCaption))
            {
                var labelObj = new GameObject("PreviewCaption", typeof(RectTransform));
                labelObj.transform.SetParent(_previewRoot.transform, false);
                var labelRect = labelObj.transform as RectTransform;
                if (labelRect != null)
                {
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(1f, 0f);
                    labelRect.pivot = new Vector2(0.5f, 0f);
                    labelRect.anchoredPosition = new Vector2(0f, 10f);
                    labelRect.sizeDelta = new Vector2(0f, 82f);
                }
                var label = labelObj.AddComponent<TextMeshProUGUI>();
                label.text = previewCaption;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TMPro.TextWrappingModes.Normal;
                label.fontSize = 18f;
                label.color = new Color(1f, 0.92f, 0.65f, 1f);
                label.raycastTarget = false;
            }
        }

        private void ClearPreview()
        {
            if (_previewRoot != null)
            {
                Destroy(_previewRoot);
                _previewRoot = null;
            }

            EnsureReadableTextArea(false);
        }

        private void EnsureReadableTextArea(bool reservePreviewSpace)
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
                textRect.offsetMax = reservePreviewSpace ? new Vector2(-350f, -30f) : new Vector2(-36f, -30f);
            }

            _messageText.raycastTarget = false;
            _messageText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _messageText.overflowMode = TextOverflowModes.Overflow;
            _messageText.enableAutoSizing = true;
            _messageText.fontSizeMin = 24f;
            _messageText.fontSizeMax = 36f;
            _messageText.alignment = reservePreviewSpace ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            _messageText.color = Color.white;
        }

        /// <summary>
        /// Скрывает сообщение
        /// </summary>
        public void Hide()
        {
            ClearPreview();
            if (_canvasGroup != null)
            {
                _targetAlpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }
    }
}
