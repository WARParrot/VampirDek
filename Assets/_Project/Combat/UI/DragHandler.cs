using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Definitions;
using Shared.UI;

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
        private CardAffordanceHighlighter _affordance;
        private Vector2 _pointerOffset;

        public bool IsDragging { get; private set; }
        public Vector2 LastPointerScreenPosition { get; private set; }

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
                _canvasRect, GetCurrentPointerScreenPosition(eventData), GetDragCamera(), out local);
        }

        private Vector2 GetCurrentPointerScreenPosition(PointerEventData eventData)
        {
            var position = eventData != null ? eventData.position : LastPointerScreenPosition;

            // On some input paths the end-drag PointerEventData can lag behind the real pointer.
            // Prefer the live pointer when available, but keep this best-effort so projects that
            // disable the legacy Input API do not throw and break drag/drop completely.
            try
            {
                if (Input.touchCount > 0)
                {
                    var pointerId = eventData != null ? eventData.pointerId : -1;
                    for (var i = 0; i < Input.touchCount; i++)
                    {
                        var touch = Input.GetTouch(i);
                        if (touch.fingerId == pointerId)
                        {
                            position = touch.position;
                            break;
                        }
                    }

                    if (position == (eventData != null ? eventData.position : LastPointerScreenPosition))
                        position = Input.GetTouch(0).position;
                }
                else if (Input.mousePresent)
                {
                    position = Input.mousePosition;
                }
            }
            catch (System.InvalidOperationException)
            {
                // Legacy Input Manager is disabled; keep EventSystem's last known position.
            }

            LastPointerScreenPosition = position;
            return position;
        }

        public void Setup(ICard card, HandUIManager manager)
        {
            _card = card;
            _handManager = manager;
        }

        public ICard GetCard() => _card;

        public void SetAffordanceState(CardAffordanceState state)
        {
            EnsureAffordance();
            if (_affordance != null)
                _affordance.SetState(state, state == CardAffordanceState.None ? 0f : 1f);
        }

        private void EnsureAffordance()
        {
            if (_affordance != null) return;
            var graphic = GetComponent<Graphic>() ?? GetComponentInChildren<Graphic>(true);
            if (graphic != null)
                _affordance = graphic.GetComponent<CardAffordanceHighlighter>() ?? graphic.gameObject.AddComponent<CardAffordanceHighlighter>();
        }

        public void OnCardTapped(ICard tappedCard)
        {
            if (tappedCard != _card) return;
            _handManager?.OnCardTapped(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            LastPointerScreenPosition = GetCurrentPointerScreenPosition(eventData);
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
            LastPointerScreenPosition = GetCurrentPointerScreenPosition(eventData);
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

            LastPointerScreenPosition = GetCurrentPointerScreenPosition(eventData);
            _canvasGroup.alpha = 1f;
            // Keep the dragged card non-raycastable while HandUIManager raycasts the drop target.
            // Re-enabling blocksRaycasts before RaycastAll can make the dropped card hit itself
            // instead of the BoardSlotUI underneath, which makes placement feel unreliable.
            _canvasGroup.blocksRaycasts = false;
            IsDragging = false;
            if (_layoutElement != null) _layoutElement.ignoreLayout = false;
            _handManager.OnCardDragEnded(this, eventData);
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        }

    }
}
