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
        private Canvas _targetCanvas;
        private RectTransform _parentRect;
        private Camera _uiCamera;
        private float _pulseTime = 0f;
        private bool _isVisible = false;
        private float _nextCameraLookupAt;
        private const float CameraLookupInterval = 0.5f;

        private void Awake()
        {
            _camera = Camera.main;
            _canvas = GetComponentInParent<Canvas>();
            _parentRect = _arrowTransform != null ? _arrowTransform.parent as RectTransform : null;
            _uiCamera = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;

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
                Camera srcCam = (_targetCanvas != null && _targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _targetCanvas.worldCamera : null;
                screenPos = RectTransformUtility.WorldToScreenPoint(srcCam, _targetUIElement.position);
                screenPos += Vector2.up * _offset;
                hasValidTarget = true;
            }
            else if (_targetObject != null)
            {
                if (_camera == null && Time.unscaledTime >= _nextCameraLookupAt)
                {
                    _nextCameraLookupAt = Time.unscaledTime + CameraLookupInterval;
                    _camera = Camera.main;
                }
                if (_camera == null) return;
                Vector3 sp = _camera.WorldToScreenPoint(_targetObject.transform.position);
                if (sp.z > 0)
                {
                    screenPos = new Vector2(sp.x, sp.y) + Vector2.up * _offset;
                    hasValidTarget = true;
                }
            }

            if (hasValidTarget)
            {
                if (_parentRect == null) _parentRect = _arrowTransform.parent as RectTransform;
                if (_uiCamera == null && _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) _uiCamera = _canvas.worldCamera;
                if (_parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, screenPos, _uiCamera, out var localPoint))
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
            _targetCanvas = null;
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
            _targetCanvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            _targetObject = null;
            _isVisible = true;
            _pulseTime = 0f;
            if (_arrowImage != null)
            {
                _arrowImage.raycastTarget = false;
                _arrowImage.enabled = true;
            }
            //Debug.Log($"[TutorialArrowUI] PointToUI: target={target?.name ?? "NULL"}, _arrowTransform={(_arrowTransform != null ? "OK" : "NULL")}, _arrowImage={(_arrowImage != null ? "OK" : "NULL")}");
        }

        /// <summary>
        /// Скрывает стрелку
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
            _targetObject = null;
            _targetUIElement = null;
            _targetCanvas = null;

            if (_arrowImage != null)
            {
                _arrowImage.enabled = false;
            }
        }
    }
}
