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
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;
        private LayoutElement _layoutElement;
        private ICard _card;
        private HandUIManager _handManager;
        private Vector2 _pointerOffset;

        public bool IsDragging { get; private set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
                _canvasRect = _canvas.transform as RectTransform;

            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
                _layoutElement = gameObject.AddComponent<LayoutElement>();
        }

        private Camera GetDragCamera()
        {
            if (_canvas == null) return null;
            return _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        }

        private bool TryGetLocalPointer(PointerEventData eventData, out Vector2 local)
        {
            local = Vector2.zero;
            if (_canvasRect == null) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, eventData.position, GetDragCamera(), out local);
        }

        public void Setup(ICard card, HandUIManager manager)
        {
            _card = card;
            _handManager = manager;
        }

        public ICard GetCard() => _card;

        public void OnCardTapped(ICard tappedCard)
        {
            if (tappedCard != _card) return;
            _handManager?.OnCardTapped(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_handManager != null && !_handManager.CanStartCardDrag())
            {
                IsDragging = false;
                return;
            }

            _canvasGroup.alpha = 0.75f;
            _canvasGroup.blocksRaycasts = false;
            IsDragging = true;
            if (_layoutElement != null) _layoutElement.ignoreLayout = true;

            // Free the card from layout BEFORE measuring its pointer offset so the
            // recorded grab point matches where the player actually clicked.
            if (TryGetLocalPointer(eventData, out var localPointer))
            {
                _pointerOffset = (Vector2)_rectTransform.localPosition - localPointer;
            }
            else
            {
                _pointerOffset = Vector2.zero;
            }

            _handManager.OnCardDragStarted(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;
            if (TryGetLocalPointer(eventData, out var localPointer))
            {
                _rectTransform.localPosition = (Vector3)(localPointer + _pointerOffset);
            }
            else
            {
                _rectTransform.anchoredPosition += eventData.delta / Mathf.Max(_canvas.scaleFactor, 0.0001f);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!IsDragging) return;

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            IsDragging = false;
            if (_layoutElement != null) _layoutElement.ignoreLayout = false;
            _handManager.OnCardDragEnded(this, eventData);
        }

    }
}
