using System.Collections;
using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// When the lockbox unlocks, plays the reveal sequence: lid opens (handled by the
    /// OpenableObject already wired), a procedural potion bottle rises out of the box,
    /// hovers for a moment, then the whole lockbox GameObject is hidden from the world.
    /// </summary>
    public class LockboxPotionReveal : MonoBehaviour
    {
        [SerializeField] private RotaryLockPuzzle _puzzle;
        [SerializeField] private Transform _potionSpawn; // where the bottle starts (inside the box)
        [SerializeField] private float _riseHeight = 0.6f;
        [SerializeField] private float _riseDuration = 1.4f;
        [SerializeField] private float _hoverDuration = 0.8f;
        [SerializeField] private float _fadeOutDuration = 0.6f;
        [SerializeField] private float _delayBeforeRise = 0.45f; // wait for lid to swing

        private bool _played;

        private void Awake()
        {
            if (_puzzle != null) _puzzle.OnUnlocked.AddListener(HandleUnlocked);
        }

        private void OnDestroy()
        {
            if (_puzzle != null) _puzzle.OnUnlocked.RemoveListener(HandleUnlocked);
        }

        /// <summary>
        /// Called externally (e.g. from EscapeQuestBootstrap) when the component is added
        /// AFTER Awake has run with a null puzzle reference. Idempotent — won't double-add.
        /// </summary>
        public void BindAndSubscribe(RotaryLockPuzzle puzzle)
        {
            if (_puzzle == puzzle) return;
            if (_puzzle != null) _puzzle.OnUnlocked.RemoveListener(HandleUnlocked);
            _puzzle = puzzle;
            if (_puzzle != null) _puzzle.OnUnlocked.AddListener(HandleUnlocked);
        }

        public void TriggerReveal()
        {
            if (_played) return;
            _played = true;
            StartCoroutine(PlayReveal());
        }

        public void OverrideSpawn(Transform spawn) => _potionSpawn = spawn;

        private void HandleUnlocked() => TriggerReveal();

        private IEnumerator PlayReveal()
        {
            Debug.Log($"[LockboxPotionReveal] Reveal started on '{name}'. Spawn anchor: " +
                      (_potionSpawn != null ? _potionSpawn.position.ToString("F2") : transform.position.ToString("F2")));
            // Let the lid swing open first.
            yield return new WaitForSeconds(_delayBeforeRise);

            // Spawn the potion at the box centre (or _potionSpawn if authored).
            var anchor = _potionSpawn != null ? _potionSpawn.position : transform.position + Vector3.up * 0.1f;
            var bottle = BuildPotionVisual();
            bottle.transform.position = anchor;

            // Rise.
            Vector3 from = anchor;
            Vector3 to = anchor + Vector3.up * _riseHeight;
            float t = 0f;
            while (t < _riseDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / _riseDuration));
                bottle.transform.position = Vector3.LerpUnclamped(from, to, k);
                bottle.transform.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
                yield return null;
            }

            // Hover (slow spin).
            t = 0f;
            while (t < _hoverDuration)
            {
                t += Time.deltaTime;
                bottle.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
                yield return null;
            }

            // Fade out the bottle and the box together. Renderers get an instanced material
            // so we can drop their alpha — leaves shared materials untouched.
            var bottleRenderers = bottle.GetComponentsInChildren<Renderer>();
            var boxRenderers = GetComponentsInChildren<Renderer>(true);
            float fadeT = 0f;
            while (fadeT < _fadeOutDuration)
            {
                fadeT += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(fadeT / _fadeOutDuration);
                ApplyAlpha(bottleRenderers, a);
                ApplyAlpha(boxRenderers, a);
                yield return null;
            }

            Destroy(bottle);
            gameObject.SetActive(false); // chest gone from the world
        }

        private static GameObject BuildPotionVisual()
        {
            var root = new GameObject("PotionVisual");

            // Glass body — capsule pinched into a tall bottle shape.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            body.transform.localScale = new Vector3(0.08f, 0.10f, 0.08f);
            Destroy(body.GetComponent<Collider>());
            var bodyRen = body.GetComponent<Renderer>();
            if (bodyRen != null) bodyRen.material.color = new Color(0.25f, 0.05f, 0.45f, 1f); // deep violet

            // Neck.
            var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Neck";
            neck.transform.SetParent(root.transform, false);
            neck.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            neck.transform.localScale = new Vector3(0.04f, 0.03f, 0.04f);
            Destroy(neck.GetComponent<Collider>());
            var neckRen = neck.GetComponent<Renderer>();
            if (neckRen != null) neckRen.material.color = new Color(0.20f, 0.15f, 0.10f, 1f);

            // Cork.
            var cork = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cork.name = "Cork";
            cork.transform.SetParent(root.transform, false);
            cork.transform.localPosition = new Vector3(0f, 0.21f, 0f);
            cork.transform.localScale = new Vector3(0.05f, 0.025f, 0.05f);
            Destroy(cork.GetComponent<Collider>());
            var corkRen = cork.GetComponent<Renderer>();
            if (corkRen != null) corkRen.material.color = new Color(0.45f, 0.30f, 0.15f, 1f);

            // Soft glow point so it reads as magical even without a fancy shader.
            var glowGo = new GameObject("Glow", typeof(Light));
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            var light = glowGo.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.6f, 0.3f, 1f, 1f);
            light.intensity = 1.6f;
            light.range = 1.6f;
            return root;
        }

        private static void ApplyAlpha(Renderer[] renderers, float alpha)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var m = r.material; // instanced
                if (m == null) continue;
                if (m.HasProperty("_BaseColor"))
                {
                    var c = m.GetColor("_BaseColor"); c.a = alpha;
                    m.SetColor("_BaseColor", c);
                }
                if (m.HasProperty("_Color"))
                {
                    var c = m.GetColor("_Color"); c.a = alpha;
                    m.SetColor("_Color", c);
                }
            }
        }
    }
}
