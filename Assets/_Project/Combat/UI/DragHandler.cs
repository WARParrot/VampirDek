using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Definitions;

namespace Combat.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private Canvas _canvas;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private ICard _card;
        private HandUIManager _handManager;

        public bool IsDragging { get; private set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
        }

        public void Setup(ICard card, HandUIManager manager)
        {
            _card = card;
            _handManager = manager;
        }

        public ICard GetCard() => _card;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _canvasGroup.alpha = 0.6f;
            _canvasGroup.blocksRaycasts = false;
            IsDragging = true;
            _handManager.OnCardDragStarted(this);
        }

        public void OnDrag(PointerEventData eventData) =>
            _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            IsDragging = false;
            _handManager.OnCardDragEnded(this, eventData);
        }

        public void ReturnToHand(Vector2 position) => _rectTransform.anchoredPosition = position;
    }
}
