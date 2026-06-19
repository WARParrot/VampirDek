using System.Collections;
using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// A single rotating ring of a combination lock.
    ///
    /// Rotation model: the dial spins around its own LOCAL X axis through its own
    /// transform origin — identical to what the editor does when you grab the
    /// rotation gizmo on the dial in X mode. No pivot offset, no world-space
    /// gymnastics. The mesh's transform origin is assumed to sit at the visual
    /// centre of the gear (Blender / Maya origin-to-geometry default).
    /// </summary>
    public class RotaryDial : MonoBehaviour
    {
        [Tooltip("Symbols on this ring in CCW order. The puzzle compares CurrentSymbol to its target. For a 6-tooth gear keep this at length 6.")]
        public string[] Symbols = { "A", "B", "C", "D", "E", "F" };

        [Tooltip("If true, on Awake / OnValidate the dial trims Symbols and DegreesPerStep to match a 6-tooth gear regardless of what's serialised in the scene. Turn this off only if you intentionally want a non-6 dial.")]
        public bool ForceSixTeeth = true;

        [Tooltip("Visual rotation per index step, in degrees. For a 6-tooth gear this is 60 (= 360 / 6).")]
        public float DegreesPerStep = 60f;

        [Tooltip("Seconds to animate one step.")]
        public float StepDuration = 0.18f;

        [SerializeField, Min(0)] private int _currentIndex;

        private bool _animating;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalEuler;
        private bool _initialised;

        public int CurrentIndex => _currentIndex;
        public string CurrentSymbol => Symbols != null && Symbols.Length > 0 ? Symbols[Mathf.Abs(_currentIndex) % Symbols.Length] : "";
        public bool IsAnimating => _animating;

        private void Awake() { MigrateToSixTeeth(); Initialise(); }

        private void Initialise()
        {
            if (_initialised) return;
            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalEuler = transform.localEulerAngles;
            _initialised = true;
            ApplyAngle(_currentIndex * DegreesPerStep);
        }

        private void OnValidate()
        {
            MigrateToSixTeeth();
            if (Symbols != null && Symbols.Length > 0 && DegreesPerStep == 0f)
                DegreesPerStep = 360f / Symbols.Length;
        }

        private void MigrateToSixTeeth()
        {
            if (!ForceSixTeeth) return;
            const int Teeth = 6;
            if (Symbols == null || Symbols.Length != Teeth)
            {
                var seed = Symbols ?? System.Array.Empty<string>();
                var defaults = new[] { "A", "B", "C", "D", "E", "F" };
                var fresh = new string[Teeth];
                for (int i = 0; i < Teeth; i++)
                    fresh[i] = (i < seed.Length && !string.IsNullOrEmpty(seed[i])) ? seed[i] : defaults[i];
                Symbols = fresh;
            }
            DegreesPerStep = 360f / Teeth; // 60
            if (_currentIndex >= Teeth || _currentIndex < 0)
                _currentIndex = ((_currentIndex % Teeth) + Teeth) % Teeth;
        }

        public void Step(int delta)
        {
            if (_animating) return;
            if (Symbols == null || Symbols.Length == 0) return;
            Initialise();
            int len = Symbols.Length;
            int prevIndex = _currentIndex;
            _currentIndex = ((_currentIndex + delta) % len + len) % len;
            float fromAngle = prevIndex * DegreesPerStep;
            float toAngle = fromAngle + delta * DegreesPerStep;
            StartCoroutine(AnimateAngle(fromAngle, toAngle));
        }

        public void SetIndexInstant(int index)
        {
            if (Symbols == null || Symbols.Length == 0) return;
            Initialise();
            int len = Symbols.Length;
            _currentIndex = ((index % len) + len) % len;
            ApplyAngle(_currentIndex * DegreesPerStep);
        }

        // Identical to typing into the X rotation field in the inspector: add the angle to
        // the initial X euler, keep Y and Z untouched.
        private void ApplyAngle(float angle)
        {
            transform.localEulerAngles = new Vector3(
                _initialLocalEuler.x + angle,
                _initialLocalEuler.y,
                _initialLocalEuler.z);
        }

        private IEnumerator AnimateAngle(float fromAngle, float toAngle)
        {
            _animating = true;
            float t = 0f;
            while (t < StepDuration)
            {
                t += Time.unscaledDeltaTime;
                float n = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / StepDuration));
                float a = Mathf.LerpUnclamped(fromAngle, toAngle, n);
                ApplyAngle(a);
                yield return null;
            }
            ApplyAngle(toAngle);
            _animating = false;
        }

        private void OnDrawGizmos()
        {
            // Just a small marker at the dial origin so you can see where it's spinning from.
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.02f);
            Vector3 up = transform.rotation * Vector3.up;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position - up * 0.15f, transform.position + up * 0.15f);
        }
    }
}
