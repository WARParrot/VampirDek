using UnityEngine;
using UnityEngine.UI;

namespace Combat
{
    /// <summary>
    /// UI компонент для отображения стрелки-указателя в обучении
    /// </summary>
    public class TutorialArrowUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _arrowTransform;
        [SerializeField] private Image _arrowImage;
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _minScale = 0.9f;
        [SerializeField] private float _maxScale = 1.1f;
        [SerializeField] private float _offset = 50f;

        private GameObject _targetObject;
        private RectTransform _targetUIElement;
        private Camera _camera;
        private Canvas _canvas;
        private float _pulseTime = 0f;
        private bool _isVisible = false;

        private void Awake()
        {
            _camera = Camera.main;
            _canvas = GetComponentInParent<Canvas>();

            if (_arrowImage != null)
            {
                _arrowImage.raycastTarget = false;
                Hide();
            }
        }

        private void Update()
        {
            if (!_isVisible) return;
            if (_arrowTransform == null || _arrowImage == null) return;

            Vector2 screenPos = Vector2.zero;
            bool hasValidTarget = false;

            if (_targetUIElement != null)
            {
                var srcCanvas = _targetUIElement.GetComponentInParent<Canvas>();
                Camera srcCam = (srcCanvas != null && srcCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? srcCanvas.worldCamera : null;
                screenPos = RectTransformUtility.WorldToScreenPoint(srcCam, _targetUIElement.position);
                screenPos += Vector2.up * _offset;
                hasValidTarget = true;
            }
            else if (_targetObject != null && _camera != null)
            {
                Vector3 sp = _camera.WorldToScreenPoint(_targetObject.transform.position);
                if (sp.z > 0)
                {
                    screenPos = new Vector2(sp.x, sp.y) + Vector2.up * _offset;
                    hasValidTarget = true;
                }
            }

            if (hasValidTarget)
            {
                var parentRect = _arrowTransform.parent as RectTransform;
                Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
                if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, uiCam, out var localPoint))
                {
                    _arrowTransform.anchoredPosition = localPoint;
                }

                _pulseTime += Time.deltaTime * _pulseSpeed;
                float scale = Mathf.Lerp(_minScale, _maxScale, (Mathf.Sin(_pulseTime) + 1f) * 0.5f);
                _arrowTransform.localScale = Vector3.one * scale;

                _arrowImage.enabled = true;
            }
            else
            {
                _arrowImage.enabled = false;
            }
        }

        /// <summary>
        /// Направляет стрелку на целевой GameObject в мире
        /// </summary>
        public void PointTo(GameObject target)
        {
            _targetObject = target;
            _targetUIElement = null;
            _isVisible = true;
            _pulseTime = 0f;
            if (_arrowImage != null)
            {
                _arrowImage.raycastTarget = false;
                _arrowImage.enabled = true;
            }
        }

        /// <summary>
        /// Направляет стрелку на UI-элемент
        /// </summary>
        public void PointToUI(RectTransform target)
        {
            _targetUIElement = target;
            _targetObject = null;
            _isVisible = true;
            _pulseTime = 0f;
            if (_arrowImage != null)
            {
                _arrowImage.raycastTarget = false;
                _arrowImage.enabled = true;
            }
            Debug.Log($"[TutorialArrowUI] PointToUI: target={target?.name ?? "NULL"}, _arrowTransform={(_arrowTransform != null ? "OK" : "NULL")}, _arrowImage={(_arrowImage != null ? "OK" : "NULL")}");
        }

        /// <summary>
        /// Скрывает стрелку
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetObject = null;
            _targetUIElement = null;

            if (_arrowImage != null)
            {
                _arrowImage.enabled = false;
            }
        }
    }
}
