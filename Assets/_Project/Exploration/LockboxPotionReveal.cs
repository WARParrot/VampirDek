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
        [SerializeField] private Transform _existingPotion; // real potion mesh in the box, if any
        [SerializeField] private Transform _lid; // chest lid to swing back
        [SerializeField] private float _lidOpenAngle = -110f;
        [SerializeField] private float _lidOpenDuration = 1.1f;
        [SerializeField] private float _riseHeight = 1.2f;
        [SerializeField] private float _riseDuration = 2.4f;
        [SerializeField] private float _hoverDuration = 1.4f;
        [SerializeField] private float _fadeOutDuration = 1.0f;
        [SerializeField] private float _delayBeforeRise = 0.1f; // wait for lid to swing

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
            // Host the coroutine on a SEPARATE GameObject so it survives even if the puzzle
            // root deactivates its visuals (RotaryLockPuzzle.Solve toggles _lockedVisual /
            // _unlockedVisual right before OnUnlocked.Invoke, which would suspend any
            // coroutine running on this component).
            var host = new GameObject("~LockboxRevealRunner");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<RevealRunner>();
            runner.Begin(this);
        }

        /// <summary>External coroutine host so the reveal can't be paused by the puzzle root.</summary>
        private class RevealRunner : MonoBehaviour
        {
            public void Begin(LockboxPotionReveal owner)
            {
                StartCoroutine(Run(owner));
            }

            private IEnumerator Run(LockboxPotionReveal owner)
            {
                yield return owner.PlayReveal();
                Destroy(gameObject);
            }
        }

        public void OverrideSpawn(Transform spawn) => _potionSpawn = spawn;
        public void OverrideExistingPotion(Transform potion) => _existingPotion = potion;
        public void OverrideLid(Transform lid) => _lid = lid;

        private void HandleUnlocked() => TriggerReveal();

        internal IEnumerator PlayReveal()
        {
            Debug.Log($"[LockboxPotionReveal] Reveal started on '{name}'. Spawn anchor: " +
                      (_potionSpawn != null ? _potionSpawn.position.ToString("F2") : transform.position.ToString("F2")));

            // Swing the lid backward around its BACK edge (not its centre). The back edge
            // is computed from the lid's renderer bounds — we pick the world-Z-max edge as
            // "back" relative to the chest. The hinge axis is the lid's local +X. Lid lifts
            // backward like a real chest, not flipping sideways.
            if (_lid != null)
            {
                var ren = _lid.GetComponent<Renderer>() ?? _lid.GetComponentInChildren<Renderer>();
                if (ren != null)
                {
                    var b = ren.bounds;
                    // World X was tested and tipped the lid sideways. Force the hinge axis
                    // to world Z and pivot at one of the X extremes (the back edge along Z).
                    Vector3 hingePoint = new Vector3(b.max.x, b.max.y, b.center.z);
                    Vector3 hingeAxis = Vector3.forward;

                    float elapsed = 0f;
                    float lastAngle = 0f;
                    while (elapsed < _lidOpenDuration)
                    {
                        elapsed += Time.deltaTime;
                        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / _lidOpenDuration));
                        float target = k * _lidOpenAngle;
                        float delta = target - lastAngle;
                        _lid.RotateAround(hingePoint, hingeAxis, delta);
                        lastAngle = target;
                        yield return null;
                    }
                }
                else Debug.LogWarning("[LockboxPotionReveal] Lid set but no Renderer on it — skipping swing.");
            }
            else Debug.LogWarning("[LockboxPotionReveal] No lid wired — skipping swing.");

            // Kill the dials: SetActive(false) on the GameObject AND disable every Renderer
            // up the chain. Some chests have dials parented as Mesh children, others have
            // them as top-level children with a shared mesh — covering both cases.
            int hidden = 0, deactivated = 0;
            if (_puzzle != null)
            {
                foreach (var d in _puzzle.GetComponentsInChildren<RotaryDial>(true))
                {
                    if (d == null) continue;
                    foreach (var r in d.GetComponentsInChildren<Renderer>(true))
                        if (r != null) { r.enabled = false; hidden++; }
                    d.gameObject.SetActive(false);
                    deactivated++;
                }
            }
            Debug.Log($"[LockboxPotionReveal] Dials: hidden {hidden} renderers, deactivated {deactivated} GameObjects.");

            // Let the lid settle.
            yield return new WaitForSeconds(_delayBeforeRise);

            // Use the REAL potion mesh from inside the chest when present (set by bootstrap).
            // Fall back to procedural visual only if the box has no such child.
            GameObject bottle;
            bool destroyAfter;
            if (_existingPotion != null)
            {
                bottle = _existingPotion.gameObject;
                bottle.SetActive(true);
                bottle.transform.SetParent(null, true);
                destroyAfter = false;
                Debug.Log($"[LockboxPotionReveal] Using existing potion '{bottle.name}' at {bottle.transform.position:F2}.");
            }
            else
            {
                bottle = BuildPotionVisual();
                bottle.transform.position = _potionSpawn != null
                    ? _potionSpawn.position
                    : transform.position + Vector3.up * 0.1f;
                destroyAfter = true;
                Debug.Log($"[LockboxPotionReveal] Spawned procedural bottle at {bottle.transform.position:F2}.");
            }
            var anchor = bottle.transform.position;

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

            // Bottle stays in the world after the reveal — the player just received the
            // antidote in their inventory and the on-screen prop has done its job. No fade
            // on the chest either; the dial renderers are already hidden and the box can
            // stay open as a piece of set dressing.
            if (destroyAfter) Destroy(bottle);
            else bottle.SetActive(false);
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
