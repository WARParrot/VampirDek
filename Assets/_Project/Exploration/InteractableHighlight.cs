using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// Добавляет визуальную подсветку интерактивным объектам
    /// </summary>
    [RequireComponent(typeof(IInteractable))]
    public class InteractableHighlight : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 0.5f, 0.3f);
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _minIntensity = 0.5f;
        [SerializeField] private float _maxIntensity = 1f;

        private Renderer[] _renderers;
        private MaterialPropertyBlock _propertyBlock;
        private float _pulseTime = 0f;
        private bool _isHighlighted = false;

        private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (!_isHighlighted) return;

            _pulseTime += Time.deltaTime * _pulseSpeed;
            float intensity = Mathf.Lerp(_minIntensity, _maxIntensity, (Mathf.Sin(_pulseTime) + 1f) * 0.5f);

            Color emissionColor = _highlightColor * intensity;

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorProperty, emissionColor);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>
        /// Включает подсветку объекта
        /// </summary>
        public void EnableHighlight()
        {
            _isHighlighted = true;
            _pulseTime = 0f;

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                foreach (var mat in renderer.materials)
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }

        /// <summary>
        /// Отключает подсветку объекта
        /// </summary>
        public void DisableHighlight()
        {
            _isHighlighted = false;

            foreach (var renderer in _renderers)
            {
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorProperty, Color.black);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void OnEnable()
        {
            EnableHighlight();
        }

        private void OnDisable()
        {
            DisableHighlight();
        }
    }
}
