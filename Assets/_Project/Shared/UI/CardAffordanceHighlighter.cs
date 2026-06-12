using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    public enum CardAffordanceState
    {
        None = 0,
        Compatible = 1,
        Incompatible = 2,
        Selected = 3,
        Target = 4,
        Blocked = 5,
        Planned = 6,
        Warning = 7
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class CardAffordanceHighlighter : MonoBehaviour
    {
        private static readonly int ModeId = Shader.PropertyToID("_AffordanceMode");
        private static readonly int AffordanceColorId = Shader.PropertyToID("_AffordanceColor");
        private static readonly int SecondaryColorId = Shader.PropertyToID("_SecondaryColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int BorderWidthId = Shader.PropertyToID("_BorderWidth");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");

        [SerializeField] private Graphic _graphic;
        [SerializeField] private CardAffordanceState _state;
        [SerializeField] private float _intensity = 1f;
        [SerializeField] private float _pulseSpeed = 4f;
        [SerializeField] private float _borderWidth = 0.055f;
        [SerializeField] private float _patternScale = 22f;

        private Material _runtimeMaterial;
        private Material _originalMaterial;
        private bool _capturedOriginal;

        public CardAffordanceState State => _state;

        private void Awake()
        {
            EnsureGraphic();
        }

        private void OnEnable()
        {
            if (_state != CardAffordanceState.None)
                ApplyState();
        }

        private void OnDestroy()
        {
            RestoreOriginalMaterial();
            if (_runtimeMaterial != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMaterial);
                else DestroyImmediate(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        public void SetState(CardAffordanceState state, float intensity = 1f)
        {
            _state = state;
            _intensity = Mathf.Clamp(intensity, 0f, 2f);
            ApplyState();
        }

        public void Clear() => SetState(CardAffordanceState.None, 0f);

        private void ApplyState()
        {
            if (!EnsureGraphic()) return;

            if (_state == CardAffordanceState.None)
            {
                RestoreOriginalMaterial();
                return;
            }

            if (!EnsureMaterial()) return;

            var colors = GetPalette(_state);
            _runtimeMaterial.SetFloat(ModeId, (float)_state);
            _runtimeMaterial.SetColor(AffordanceColorId, colors.primary);
            _runtimeMaterial.SetColor(SecondaryColorId, colors.secondary);
            _runtimeMaterial.SetFloat(IntensityId, _intensity);
            _runtimeMaterial.SetFloat(PulseSpeedId, _pulseSpeed);
            _runtimeMaterial.SetFloat(BorderWidthId, _borderWidth);
            _runtimeMaterial.SetFloat(PatternScaleId, _patternScale);
            _graphic.material = _runtimeMaterial;
            _graphic.SetMaterialDirty();
        }

        private bool EnsureGraphic()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            return _graphic != null;
        }

        private bool EnsureMaterial()
        {
            if (!EnsureGraphic()) return false;
            if (!_capturedOriginal)
            {
                _originalMaterial = _graphic.material;
                _capturedOriginal = true;
            }

            if (_runtimeMaterial != null) return true;

            var shader = Shader.Find("VampirDek/UI/CardAffordance");
            if (shader == null)
            {
                Debug.LogWarning("[CardAffordance] Shader 'VampirDek/UI/CardAffordance' not found. Affordance highlight disabled.", this);
                return false;
            }

            _runtimeMaterial = new Material(shader)
            {
                name = $"{name} CardAffordance (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            return true;
        }

        private void RestoreOriginalMaterial()
        {
            if (_graphic == null || !_capturedOriginal) return;
            if (_graphic.material == _runtimeMaterial)
            {
                _graphic.material = _originalMaterial;
                _graphic.SetMaterialDirty();
            }
        }

        private static (Color primary, Color secondary) GetPalette(CardAffordanceState state)
        {
            return state switch
            {
                CardAffordanceState.Compatible => (new Color(0.10f, 1.00f, 0.70f, 0.95f), new Color(0.70f, 1.00f, 0.25f, 0.95f)),
                CardAffordanceState.Incompatible => (new Color(1.00f, 0.62f, 0.12f, 0.82f), new Color(0.42f, 0.26f, 0.08f, 0.82f)),
                CardAffordanceState.Selected => (new Color(0.12f, 0.88f, 1.00f, 1.00f), new Color(0.70f, 1.00f, 1.00f, 1.00f)),
                CardAffordanceState.Target => (new Color(1.00f, 0.18f, 0.10f, 1.00f), new Color(1.00f, 0.74f, 0.12f, 1.00f)),
                CardAffordanceState.Blocked => (new Color(0.95f, 0.08f, 0.10f, 0.90f), new Color(0.25f, 0.02f, 0.02f, 0.90f)),
                CardAffordanceState.Planned => (new Color(0.12f, 0.55f, 1.00f, 0.95f), new Color(1.00f, 0.84f, 0.20f, 0.95f)),
                CardAffordanceState.Warning => (new Color(1.00f, 0.20f, 0.10f, 1.00f), new Color(1.00f, 0.95f, 0.25f, 1.00f)),
                _ => (Color.clear, Color.clear)
            };
        }
    }
}
