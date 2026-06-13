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
        [SerializeField] private float _intensity = 1.12f;
        [SerializeField] private float _pulseSpeed = 2.8f;
        [SerializeField] private float _borderWidth = 0.07f;
        [SerializeField] private float _patternScale = 18f;

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

        public void SetState(CardAffordanceState state, float intensity = 1.12f)
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
                // VampirDek uses affordance colour as table-language, not debug colour:
                // verdant pact, stale refusal, moonlit selection, blood target, warded block,
                // fate thread, and fever warning.
                CardAffordanceState.Compatible => (new Color(0.22f, 0.95f, 0.54f, 0.92f), new Color(0.96f, 0.82f, 0.34f, 0.86f)),
                CardAffordanceState.Incompatible => (new Color(0.93f, 0.50f, 0.16f, 0.78f), new Color(0.34f, 0.20f, 0.10f, 0.82f)),
                CardAffordanceState.Selected => (new Color(0.18f, 0.78f, 1.00f, 0.94f), new Color(0.74f, 0.92f, 1.00f, 0.90f)),
                CardAffordanceState.Target => (new Color(0.98f, 0.12f, 0.08f, 0.98f), new Color(1.00f, 0.55f, 0.12f, 0.96f)),
                CardAffordanceState.Blocked => (new Color(0.72f, 0.04f, 0.07f, 0.88f), new Color(0.16f, 0.02f, 0.03f, 0.92f)),
                CardAffordanceState.Planned => (new Color(0.18f, 0.45f, 1.00f, 0.88f), new Color(1.00f, 0.78f, 0.22f, 0.90f)),
                CardAffordanceState.Warning => (new Color(1.00f, 0.16f, 0.08f, 0.98f), new Color(1.00f, 0.90f, 0.18f, 0.96f)),
                _ => (Color.clear, Color.clear)
            };
        }
    }
}
