using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Shared.Localization;
using Core;
using Exploration.Inventory;

namespace Exploration
{
    /// <summary>
    /// "Examine in place" rotary lockbox.
    ///
    /// Flow:
    ///   E   → enter examine (box flies a bit closer to the camera around _examineAnchor)
    ///   A/D → previous / next dial
    ///   W/S → step current dial +/- one tooth
    ///   RMB drag → orbit the box (camera stays still, only the box's rotation changes)
    ///   LMB on a dial → step that dial +1
    ///   Wheel over a dial → step +/- 1
    ///   Esc → exit, box returns to its original world pose
    ///
    /// Selection highlight is a SILHOUETTE: we instantiate a slightly-larger clone of the
    /// current dial's mesh rendered with a flat unlit colour BEHIND the original — so only
    /// the outline shows around the dial instead of tinting the whole surface.
    /// </summary>
    public class RotaryLockPuzzle : MonoBehaviour, IInteractable
    {
        [Header("Dials (reading order)")]
        [SerializeField] private List<RotaryDial> _dials = new();

        [Tooltip("Target symbol per dial. Must match _dials length.")]
        [SerializeField] private string[] _targetCode = { "A", "B", "C", "D", "E", "F" };

        [Header("Selection highlight (silhouette outline)")]
        [Tooltip("Outline colour for the currently selected dial.")]
        [SerializeField] private Color _outlineColor = new Color(1.0f, 0.78f, 0.35f, 1f);
        [Tooltip("Outline thickness as a fraction of the dial's bounds extent. 0.04 = 4% bigger.")]
        [SerializeField, Range(0.005f, 0.2f)] private float _outlineThickness = 0.04f;
        [SerializeField, Range(0f, 6f)] private float _outlinePulseSpeed = 3f;

        [Header("Examine framing")]
        [Tooltip("How far in front of the camera the box is brought, in metres.")]
        [SerializeField] private float _examineDistance = 0.85f;
        [Tooltip("Vertical offset from the camera centre so the dials sit nicely in view.")]
        [SerializeField] private float _examineVerticalOffset = 0f;
        [Tooltip("Optional: a CHILD transform on the box that marks its visual centre. If set, this point is placed in front of the camera. Otherwise transform.origin is used.")]
        [SerializeField] private Transform _examineAnchor;
        [Tooltip("Seconds for the slide-to-camera transition.")]
        [SerializeField] private float _examineEaseDuration = 0.25f;

        [Header("Box rotate (RMB drag)")]
        [SerializeField, Range(0.01f, 1f)] private float _boxDragSensitivity = 0.35f;
        [SerializeField, Min(0f)] private float _clickThresholdPixels = 6f;

        [Header("UI prompt")]
        [Tooltip("Optional: shown to the player while examining. Built automatically if left null.")]
        [SerializeField] private bool _showHelpHud = true;

        [Header("Visuals")]
        [SerializeField] private GameObject _lockedVisual;
        [SerializeField] private GameObject _unlockedVisual;

        [Header("Audio (FMOD)")]
        [SerializeField] private string _stepEvent;
        [SerializeField] private string _unlockEvent;

        [Header("Optional clue (gated by _clueIsRequired)")]
        [SerializeField] private bool _clueIsRequired;
        [SerializeField] private ItemDef _requiredClueItem;
        [SerializeField] private string _missingClueKey = "lockbox.missing_clue";
        [SerializeField] private string _missingClueFallback = "Нужна подсказка, чтобы открыть замок.";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        [Header("Events")]
        public UnityEvent OnUnlocked;

        [Header("Prompts")]
        [SerializeField] private string _promptKey = "rotary_lock.prompt";
        [SerializeField] private string _promptFallback = "Осмотреть";

        private bool _active;
        private bool _solved;
        private ExplorationController _player;
        private Camera _camera;

        // Pose bookkeeping
        private Quaternion _initialBoxRotation;
        private Vector3 _initialBoxPosition;

        // Mouse-drag bookkeeping
        private Vector2 _rmbDownPos;
        private bool _rmbDragging;
        private Vector2 _lmbDownPos;
        private bool _lmbDragging;
        private int _selectedDialIndex;

        // Silhouette outline clones for the currently selected dial.
        private readonly List<GameObject> _outlineClones = new();
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private Material _outlineSharedMaterial;

        // On-screen help HUD.
        private GameObject _hudRoot;
        private Text _hudText;

        private readonly List<Collider> _addedDialColliders = new();

        public bool IsSolved => _solved;
        public string PromptText => _solved ? string.Empty : LocalizationService.T(_promptKey, _promptFallback);

        private void Awake()
        {
            if (_lockedVisual) _lockedVisual.SetActive(true);
            if (_unlockedVisual) _unlockedVisual.SetActive(false);
            EnsureDialColliders();
        }

        private void EnsureDialColliders()
        {
            foreach (var d in _dials)
            {
                if (d == null) continue;
                if (d.GetComponentInChildren<Collider>() != null) continue;
                var ren = d.GetComponentInChildren<Renderer>();
                if (ren != null && ren.GetComponent<Collider>() == null)
                {
                    var added = ren.gameObject.AddComponent<MeshCollider>();
                    added.convex = true;
                    _addedDialColliders.Add(added);
                    continue;
                }
                var box = d.gameObject.AddComponent<BoxCollider>();
                box.size = Vector3.one * 0.2f;
                _addedDialColliders.Add(box);
            }
        }

        public void Interact(ExplorationController player)
        {
            if (_solved || _active) return;

            if (_clueIsRequired && _requiredClueItem != null)
            {
                var inv = Inventory.Inventory.Current;
                if (inv == null || !inv.Has(_requiredClueItem))
                {
                    var msg = LocalizationService.T(_missingClueKey, _missingClueFallback);
                    if (_messageOverridePrompt != null) _messageOverridePrompt.Show(msg);
                    else Debug.Log($"[RotaryLockPuzzle] {msg}");
                    return;
                }
            }

            _camera = Camera.main;
            if (_camera == null) { Debug.LogError("[RotaryLockPuzzle] No main camera."); return; }

            _player = player;
            _player.Deactivate();
            _active = true;
            _initialBoxRotation = transform.rotation;
            _initialBoxPosition = transform.position;

            // Move the box: place the chosen anchor point in front of the camera. The anchor
            // is _examineAnchor.position (a child you parented to the visual centre in the
            // prefab) — if it's null we fall back to transform.position itself. That keeps
            // the math trivial: target_origin = desired_anchor_world - (anchor.world - origin.world).
            Vector3 camPos = _camera.transform.position;
            Vector3 camFwd = _camera.transform.forward;
            Vector3 camUp = _camera.transform.up;
            Vector3 desiredAnchorWorld = camPos + camFwd * _examineDistance + camUp * _examineVerticalOffset;
            Vector3 anchorWorld = _examineAnchor != null ? _examineAnchor.position : transform.position;
            Vector3 anchorOffset = anchorWorld - transform.position;
            Vector3 targetPos = desiredAnchorWorld - anchorOffset;

            // Slide to camera over a quarter second so the move reads as intentional.
            StartCoroutine(SlideTo(targetPos, _examineEaseDuration));

            GlobalServices.IsMenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _selectedDialIndex = 0;
            BuildOutlineForCurrentDial();
            if (_showHelpHud) BuildHud();

            int dialCount = 0;
            foreach (var d in _dials) if (d != null) dialCount++;
            Debug.Log($"[RotaryLockPuzzle] Examine: dials={dialCount}/{_dials.Count}, target='{(_targetCode == null ? "(empty)" : string.Join("", _targetCode))}'.");
            for (int i = 0; i < _dials.Count; i++)
            {
                var d = _dials[i];
                if (d == null) { Debug.Log($"[RotaryLockPuzzle] dial[{i}] = NULL"); continue; }
                Debug.Log($"[RotaryLockPuzzle] dial[{i}] '{d.name}': symbols=[{string.Join(",", d.Symbols)}] step={d.DegreesPerStep}", d);
            }
        }

        private System.Collections.IEnumerator SlideTo(Vector3 to, float dur)
        {
            if (dur <= 0f) { transform.position = to; yield break; }
            Vector3 from = transform.position;
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float n = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
                transform.position = Vector3.LerpUnclamped(from, to, n);
                yield return null;
            }
            transform.position = to;
        }

        private RotaryDial DialUnderCursor(Vector2 screenPos)
        {
            if (_camera == null) return null;
            var ray = _camera.ScreenPointToRay(screenPos);
            var hits = Physics.RaycastAll(ray, 100f);
            if (hits == null || hits.Length == 0) return null;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var dial = h.collider.GetComponentInParent<RotaryDial>();
                if (dial != null && _dials.Contains(dial)) return dial;
            }
            return null;
        }

        private void Update()
        {
            if (!_active) return;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (kb.escapeKey.wasPressedThisFrame) { Exit(); return; }

            // Keyboard
            if (_dials.Count > 0)
            {
                if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)
                {
                    _selectedDialIndex = (_selectedDialIndex - 1 + _dials.Count) % _dials.Count;
                    BuildOutlineForCurrentDial();
                }
                else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                {
                    _selectedDialIndex = (_selectedDialIndex + 1) % _dials.Count;
                    BuildOutlineForCurrentDial();
                }
                if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                    StepDial(_dials[_selectedDialIndex], +1);
                else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)
                    StepDial(_dials[_selectedDialIndex], -1);
            }

            Vector2 mousePos = mouse.position.ReadValue();

            // RMB drag: orbit the box around its visible centre (= anchor world position).
            // Rotation happens via Transform.Rotate around the anchor point: that's stable
            // because the anchor stays at fixed-distance-in-front-of-camera, and we always
            // re-derive the world pivot rather than accumulate state.
            Vector3 anchorWorld = _examineAnchor != null
                ? _examineAnchor.position
                : transform.position;

            if (mouse.rightButton.wasPressedThisFrame) { _rmbDownPos = mousePos; _rmbDragging = false; }
            if (mouse.rightButton.isPressed)
            {
                if ((mousePos - _rmbDownPos).sqrMagnitude > _clickThresholdPixels * _clickThresholdPixels)
                    _rmbDragging = true;
                if (_rmbDragging)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    transform.RotateAround(anchorWorld, Vector3.up, -delta.x * _boxDragSensitivity);
                    transform.RotateAround(anchorWorld, _camera.transform.right, delta.y * _boxDragSensitivity);
                }
            }

            // LMB
            if (mouse.leftButton.wasPressedThisFrame) { _lmbDownPos = mousePos; _lmbDragging = false; }
            if (mouse.leftButton.isPressed
                && (mousePos - _lmbDownPos).sqrMagnitude > _clickThresholdPixels * _clickThresholdPixels)
            {
                _lmbDragging = true;
            }
            if (mouse.leftButton.wasReleasedThisFrame && !_lmbDragging)
            {
                var dial = DialUnderCursor(mousePos);
                if (dial != null)
                {
                    int idx = _dials.IndexOf(dial);
                    if (idx >= 0) { _selectedDialIndex = idx; BuildOutlineForCurrentDial(); }
                    StepDial(dial, +1);
                }
            }

            // Scroll over a dial steps it.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var dial = DialUnderCursor(mousePos);
                if (dial != null)
                {
                    int idx = _dials.IndexOf(dial);
                    if (idx >= 0) { _selectedDialIndex = idx; BuildOutlineForCurrentDial(); }
                    StepDial(dial, scroll > 0 ? +1 : -1);
                }
            }

            // Pulse the outline.
            if (_outlineClones.Count > 0 && _outlinePulseSpeed > 0f && _outlineSharedMaterial != null)
            {
                float t = (Mathf.Sin(Time.unscaledTime * _outlinePulseSpeed) + 1f) * 0.5f;
                Color c = _outlineColor * Mathf.Lerp(0.55f, 1f, t);
                c.a = _outlineColor.a;
                if (_outlineSharedMaterial.HasProperty(BaseColorProp))
                    _outlineSharedMaterial.SetColor(BaseColorProp, c);
                if (_outlineSharedMaterial.HasProperty(ColorProp))
                    _outlineSharedMaterial.SetColor(ColorProp, c);
            }
        }

        private void StepDial(RotaryDial dial, int delta)
        {
            if (dial == null || dial.IsAnimating) return;
            dial.Step(delta);
            if (!string.IsNullOrEmpty(_stepEvent))
                FMODUnity.RuntimeManager.PlayOneShot(_stepEvent, transform.position);
            CheckSolution();
        }

        private void CheckSolution()
        {
            if (_targetCode == null || _targetCode.Length != _dials.Count) return;
            for (int i = 0; i < _dials.Count; i++)
            {
                if (_dials[i] == null) return;
                if (!string.Equals(_dials[i].CurrentSymbol, _targetCode[i], System.StringComparison.OrdinalIgnoreCase))
                    return;
            }
            Solve();
        }

        private void Solve()
        {
            _solved = true;
            if (!string.IsNullOrEmpty(_unlockEvent))
                FMODUnity.RuntimeManager.PlayOneShot(_unlockEvent, transform.position);
            if (_lockedVisual) _lockedVisual.SetActive(false);
            if (_unlockedVisual) _unlockedVisual.SetActive(true);
            Exit();
            OnUnlocked?.Invoke();
        }

        public void Exit()
        {
            if (!_active) return;
            _active = false;
            ClearOutline();
            DestroyHud();
            StopAllCoroutines();
            transform.SetPositionAndRotation(_initialBoxPosition, _initialBoxRotation);
            GlobalServices.IsMenuOpen = false;
            if (_player != null) _player.Activate();
        }

        private void OnDisable()
        {
            if (!_active) return;
            _active = false;
            ClearOutline();
            DestroyHud();
            transform.SetPositionAndRotation(_initialBoxPosition, _initialBoxRotation);
            if (!gameObject.scene.isLoaded) return;
            try
            {
                GlobalServices.IsMenuOpen = false;
                if (_player != null && _player.isActiveAndEnabled) _player.Activate();
            }
            catch { }
        }

        // --------------------------------------------------------------------
        // Silhouette outline
        // --------------------------------------------------------------------
        //
        // Approach: instantiate a clone of every MeshFilter under the selected dial,
        // parent the clone to that mesh, scale it up by (1+thickness), strip lighting
        // by assigning a flat unlit material with Cull = Front so only the back-faces
        // render. The original mesh draws over the front, producing a clean rim line
        // around the silhouette without tinting the whole surface.

        private void BuildOutlineForCurrentDial()
        {
            ClearOutline();
            if (_dials == null || _dials.Count == 0) return;
            if (_selectedDialIndex < 0 || _selectedDialIndex >= _dials.Count) return;
            var dial = _dials[_selectedDialIndex];
            if (dial == null) return;

            if (_outlineSharedMaterial == null) _outlineSharedMaterial = CreateOutlineMaterial();

            float s = 1f + _outlineThickness;
            foreach (var mf in dial.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var srcRen = mf.GetComponent<MeshRenderer>();
                if (srcRen == null || !srcRen.enabled) continue;

                var go = new GameObject($"~outline_{mf.name}");
                go.transform.SetParent(mf.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(s, s, s);

                var cloneFilter = go.AddComponent<MeshFilter>();
                cloneFilter.sharedMesh = mf.sharedMesh;
                var cloneRen = go.AddComponent<MeshRenderer>();
                // One material per submesh, all the same flat outline material.
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = _outlineSharedMaterial;
                cloneRen.sharedMaterials = mats;
                cloneRen.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cloneRen.receiveShadows = false;
                cloneRen.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                cloneRen.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                _outlineClones.Add(go);
            }
        }

        private Material CreateOutlineMaterial()
        {
            // Try the URP unlit first (your project's render pipeline). If the shader
            // can't be found, fall back to the legacy Unlit/Color so it still renders
            // SOMETHING rather than a magenta error mesh.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Hidden/Internal-Colored");
            var m = new Material(sh) { name = "RotaryLockOutline (runtime)" };
            if (m.HasProperty(BaseColorProp)) m.SetColor(BaseColorProp, _outlineColor);
            if (m.HasProperty(ColorProp)) m.SetColor(ColorProp, _outlineColor);
            // Render only back-faces so the original mesh covers everything except the
            // expanded silhouette ring around it.
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Front);
            // Force a render-queue just below opaque so it draws before the original.
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry - 1;
            return m;
        }

        private void ClearOutline()
        {
            foreach (var go in _outlineClones)
                if (go != null) Destroy(go);
            _outlineClones.Clear();
        }

        // --------------------------------------------------------------------
        // On-screen help HUD
        // --------------------------------------------------------------------

        private void BuildHud()
        {
            if (_hudRoot != null) return;
            var canvasGo = new GameObject("RotaryLockHud_Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            // Detach immediately — we want screen-space, not box-relative.
            canvasGo.transform.SetParent(null, true);
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 850;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.GetComponent<GraphicRaycaster>().enabled = false;

            var panel = new GameObject("Panel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 36f);
            prt.sizeDelta = new Vector2(820f, 64f);
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.10f, 0.85f);
            bg.maskable = false;
            var ol = panel.AddComponent<Outline>();
            ol.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(panel.transform, false);
            _hudText = textGo.AddComponent<Text>();
            // UI.Text needs an explicit font reference. Unity ships LegacyRuntime.ttf in the
            // built-in resources package; falling back to Arial avoids a null reference if
            // the legacy font has been stripped from the user's editor install.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _hudText.font = font;
            _hudText.fontSize = 20;
            _hudText.alignment = TextAnchor.MiddleCenter;
            _hudText.color = new Color(1f, 0.92f, 0.7f, 1f);
            _hudText.raycastTarget = false;
            _hudText.maskable = false;
            _hudText.supportRichText = true;
            _hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _hudText.verticalOverflow = VerticalWrapMode.Overflow;
            _hudText.text =
                "<b>A</b>/<b>D</b> — выбрать диск   " +
                "<b>W</b>/<b>S</b> — крутить ±   " +
                "<b>ЛКМ</b> — клик по диску   " +
                "<b>Колесо</b> — крутить   " +
                "<b>ПКМ</b> — повернуть замок   " +
                "<b>Esc</b> — выйти";
            var trt = _hudText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 8f);
            trt.offsetMax = new Vector2(-16f, -8f);

            _hudRoot = canvasGo;
        }

        private void DestroyHud()
        {
            if (_hudRoot != null) Destroy(_hudRoot);
            _hudRoot = null;
            _hudText = null;
        }
    }
}
