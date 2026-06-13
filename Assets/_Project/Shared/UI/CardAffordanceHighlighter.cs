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
        private static readonly int AspectRatioId = Shader.PropertyToID("_AspectRatio");

        [SerializeField] private Graphic _graphic;
        [SerializeField] private CardAffordanceState _state;
        [SerializeField] private float _intensity = 0.95f;
        [SerializeField] private float _pulseSpeed = 2.4f;
        [SerializeField] private float _borderWidth = 0.055f;
        [SerializeField] private float _patternScale = 14f;

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

        public void SetState(CardAffordanceState state, float intensity = 0.95f)
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
            _runtimeMaterial.SetFloat(AspectRatioId, GetGraphicAspectRatio());
            _graphic.material = _runtimeMaterial;
            _graphic.SetMaterialDirty();
        }

        private bool EnsureGraphic()
        {
            if (_graphic == null) _graphic = GetComponent<Graphic>();
            return _graphic != null;
        }

        private float GetGraphicAspectRatio()
        {
            if (!EnsureGraphic()) return 1f;

            Rect rect = _graphic.rectTransform.rect;
            if (rect.width <= 0.001f || rect.height <= 0.001f) return 1f;

            return Mathf.Clamp(rect.width / rect.height, 0.1f, 10f);
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
                // VampirDek uses affordance colour as table-language, not debug colour:
                // verdant pact, stale refusal, moonlit selection, blood target, warded block,
                // fate thread, and fever warning.
                CardAffordanceState.Compatible => (new Color(0.30f, 0.82f, 0.46f, 0.82f), new Color(0.88f, 0.70f, 0.28f, 0.72f)),
                CardAffordanceState.Incompatible => (new Color(0.78f, 0.36f, 0.14f, 0.76f), new Color(0.30f, 0.20f, 0.12f, 0.76f)),
                CardAffordanceState.Selected => (new Color(0.26f, 0.68f, 0.94f, 0.86f), new Color(0.72f, 0.88f, 1.00f, 0.78f)),
                CardAffordanceState.Target => (new Color(0.94f, 0.10f, 0.08f, 0.92f), new Color(0.95f, 0.46f, 0.14f, 0.82f)),
                CardAffordanceState.Blocked => (new Color(0.66f, 0.05f, 0.08f, 0.86f), new Color(0.20f, 0.03f, 0.04f, 0.82f)),
                CardAffordanceState.Planned => (new Color(0.22f, 0.42f, 0.88f, 0.78f), new Color(0.86f, 0.68f, 0.26f, 0.72f)),
                CardAffordanceState.Warning => (new Color(0.96f, 0.18f, 0.08f, 0.92f), new Color(0.96f, 0.78f, 0.16f, 0.82f)),
                _ => (Color.clear, Color.clear)
            };
        }
    }
}
