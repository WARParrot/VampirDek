using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private List<CardChoiceButton> choiceButtons;
        [SerializeField] private float staggerDelay = 0.09f;
        [SerializeField] private float fadeDuration = 0.22f;

        // The runtime overlay (built once, then reused).
        private GameObject _overlayRoot;
        private CanvasGroup _overlayGroup;
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _subtitleText;
        private TextMeshProUGUI _counterText;
        private RectTransform _cardsRow;
        private Image _progressFill;
        private float _progressBarMaxWidth;

        // Saved button transforms (so we can restore after the draft).
        private class ButtonOrigin
        {
            public Transform Parent;
            public int SiblingIndex;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
        }
        private readonly Dictionary<CardChoiceButton, ButtonOrigin> _origins = new();

        private UniTaskCompletionSource<CardDef> _tcs;
        private UniTaskCompletionSource<List<int>> _multiTcs;
        private readonly List<int> _pickedIndices = new();
        private readonly List<CardDef> _pickedDefs = new();
        private readonly HashSet<int> _disabledIndices = new();
        private int _picksAllowed = 1;
        private List<CardDef> _candidateRefs;
        private readonly HashSet<string> _mandatoryCardNames = new();

        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            // Lazy init: in the scene CardSelectionPanel starts disabled, so Awake never fires
            // until something activates it. Call this from every public entry point too.
            if (_initialized) return;
            _initialized = true;

            if (panelRoot == null) panelRoot = gameObject;
            if (choiceButtons == null || choiceButtons.Count == 0)
                choiceButtons = new List<CardChoiceButton>(panelRoot.GetComponentsInChildren<CardChoiceButton>(true));

            // The scene's CardSelectionPanel has a legacy semi-transparent white Image and a
            // weird flatten+rotate transform. We do NOT render anything through it anymore;
            // a fresh screen-space overlay takes over.
            var legacy = panelRoot.GetComponent<Image>();
            if (legacy != null) legacy.enabled = false;

            BuildOverlay();
        }

        // -- Overlay construction ---------------------------------------------------------

        private void BuildOverlay()
        {
            if (_overlayRoot != null) return;

            _overlayRoot = new GameObject("DraftOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(CanvasGroup));
            var canvas = _overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the in-world duel UI

            var scaler = _overlayRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _overlayGroup = _overlayRoot.GetComponent<CanvasGroup>();
            _overlayGroup.alpha = 0f;
            _overlayGroup.blocksRaycasts = false;

            var rootRect = (RectTransform)_overlayRoot.transform;

            // Vignette: dark full-screen image — focuses attention without being a hard blocker.
            var vignette = CreateImage(rootRect, "Vignette", new Color(0.02f, 0.01f, 0.04f, 0.62f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            vignette.raycastTarget = true; // soaks clicks outside cards

            // Header block: golden separator + title + subtitle, sitting on a discreet plate.
            var header = CreateImage(rootRect, "Header", new Color(0.06f, 0.04f, 0.09f, 0.0f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(900f, 180f));
            header.raycastTarget = false;

            // Top thin golden line.
            CreateImage((RectTransform)header.transform, "BarTop", new Color(0.92f, 0.74f, 0.32f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -6f), new Vector2(360f, 2f));

            // Title.
            _titleText = CreateLabel((RectTransform)header.transform, "Title", "ФАЗА ДРАФТА",
                64, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center,
                new Color(1f, 0.88f, 0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(900f, 80f));
            var titleShadow = _titleText.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            titleShadow.effectDistance = new Vector2(2f, -2f);

            // Subtitle / flavor line.
            _subtitleText = CreateLabel((RectTransform)header.transform, "Subtitle",
                "Выбери карты, что усилят твою колоду на этот ход",
                22, FontStyles.Italic, TextAlignmentOptions.Center,
                new Color(0.85f, 0.78f, 0.65f, 0.9f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -108f), new Vector2(900f, 32f));

            // Bottom thin golden line.
            CreateImage((RectTransform)header.transform, "BarBot", new Color(0.92f, 0.74f, 0.32f, 0.6f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -138f), new Vector2(640f, 1f));

            // Counter pill: its background IS the progress bar. The fill grows from the left
            // and the counter text sits on top of it.
            const float pillW = 360f;
            const float pillH = 54f;
            var pill = CreateImage(rootRect, "CounterPill", new Color(0.06f, 0.04f, 0.10f, 0.95f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -252f), new Vector2(pillW, pillH));
            var pillOutline = pill.gameObject.AddComponent<Outline>();
            pillOutline.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.95f);
            pillOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Progress fill — same size as pill, pivot left so width grows L→R inside it.
            _progressFill = CreateImage((RectTransform)pill.transform, "ProgressFill",
                new Color(0.85f, 0.58f, 0.18f, 0.85f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(0f, 0f));
            _progressBarMaxWidth = pillW;

            _counterText = CreateLabel((RectTransform)pill.transform, "Counter", "0 / 0",
                28, FontStyles.Bold, TextAlignmentOptions.Center,
                new Color(1f, 0.96f, 0.85f, 1f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var counterShadow = _counterText.gameObject.AddComponent<Shadow>();
            counterShadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            counterShadow.effectDistance = new Vector2(1f, -1f);

            // Card row — horizontal layout group, centered. Bigger cards for readability.
            var rowGo = new GameObject("CardRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(rootRect, false);
            _cardsRow = (RectTransform)rowGo.transform;
            _cardsRow.anchorMin = new Vector2(0.5f, 0.5f);
            _cardsRow.anchorMax = new Vector2(0.5f, 0.5f);
            _cardsRow.pivot = new Vector2(0.5f, 0.5f);
            _cardsRow.anchoredPosition = new Vector2(0f, 10f);
            _cardsRow.sizeDelta = new Vector2(1200f, 520f);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 60f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Footer hint.
            CreateLabel(rootRect, "Hint",
                "Клик по карте — выбрать. Повторный клик — отменить выбор.",
                20, FontStyles.Italic, TextAlignmentOptions.Center,
                new Color(0.85f, 0.82f, 0.75f, 0.85f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(1100f, 30f));

            _overlayRoot.SetActive(false);
        }

        // -- UI primitives ----------------------------------------------------------------

        private static Image CreateImage(RectTransform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text,
            float fontSize, FontStyles style, TextAlignmentOptions align, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        // -- Button parking ---------------------------------------------------------------

        private void ParkButtonsIntoOverlay()
        {
            _origins.Clear();
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                var b = choiceButtons[i];
                if (b == null) continue;

                var t = b.transform;
                var rt = t as RectTransform;
                var origin = new ButtonOrigin
                {
                    Parent = t.parent,
                    SiblingIndex = t.GetSiblingIndex(),
                    LocalPosition = t.localPosition,
                    LocalRotation = t.localRotation,
                    LocalScale = t.localScale,
                };
                if (rt != null)
                {
                    origin.AnchorMin = rt.anchorMin;
                    origin.AnchorMax = rt.anchorMax;
                    origin.Pivot = rt.pivot;
                    origin.AnchoredPosition = rt.anchoredPosition;
                    origin.SizeDelta = rt.sizeDelta;
                }
                _origins[b] = origin;

                t.SetParent(_cardsRow, false);
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(320f, 460f);
                }
            }
        }

        private void RestoreButtons()
        {
            foreach (var kv in _origins)
            {
                var b = kv.Key;
                var o = kv.Value;
                if (b == null) continue;
                var t = b.transform;
                t.SetParent(o.Parent, false);
                t.SetSiblingIndex(o.SiblingIndex);
                t.localPosition = o.LocalPosition;
                t.localRotation = o.LocalRotation;
                t.localScale = o.LocalScale;
                if (t is RectTransform rt)
                {
                    rt.anchorMin = o.AnchorMin;
                    rt.anchorMax = o.AnchorMax;
                    rt.pivot = o.Pivot;
                    rt.anchoredPosition = o.AnchoredPosition;
                    rt.sizeDelta = o.SizeDelta;
                }
            }
            _origins.Clear();
        }

        // -- Show / hide ------------------------------------------------------------------

        private async UniTask ShowOverlayAsync()
        {
            if (_overlayRoot == null) BuildOverlay();
            _overlayRoot.SetActive(true);
            _overlayGroup.blocksRaycasts = true;
            _overlayGroup.alpha = 0f;

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _overlayGroup.alpha = Mathf.Clamp01(t / fadeDuration);
                await UniTask.Yield();
            }
            _overlayGroup.alpha = 1f;
        }

        private async UniTask HideOverlayAsync()
        {
            if (_overlayRoot == null) return;
            _overlayGroup.blocksRaycasts = false;
            float start = _overlayGroup.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _overlayGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
                await UniTask.Yield();
            }
            _overlayGroup.alpha = 0f;
            _overlayRoot.SetActive(false);
        }

        // -- Public API -------------------------------------------------------------------

        public async UniTask<CardDef> ShowAsync(List<string> choices)
        {
            EnsureInitialized();
            _tcs = new UniTaskCompletionSource<CardDef>();
            ParkButtonsIntoOverlay();
            await ShowOverlayAsync();

            if (_titleText != null) _titleText.text = "Награда";
            if (_subtitleText != null) _subtitleText.text = "Выбери одну карту в награду за победу";
            if (_counterText != null) _counterText.transform.parent.gameObject.SetActive(false);

            int shown = 0;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (i < choices.Count)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    choiceButtons[i].Setup(CardDatabase.GetCard(choices[i]), OnCardChosen);
                    int delayIdx = shown++;
                    _ = choiceButtons[i].PlayAppearAsync(delayIdx * staggerDelay);
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }

            return await _tcs.Task;
        }

        public async UniTask<List<int>> ShowDraftAsync(List<CardDef> candidates, int picksAllowed, string mandatoryCardName = null)
        {
            var mandatory = string.IsNullOrEmpty(mandatoryCardName)
                ? null
                : new List<string> { mandatoryCardName };
            return await ShowDraftAsync(candidates, picksAllowed, mandatory);
        }

        public async UniTask<List<int>> ShowDraftAsync(List<CardDef> candidates, int picksAllowed, IReadOnlyCollection<string> mandatoryCardNames)
        {
            EnsureInitialized();
            _multiTcs = new UniTaskCompletionSource<List<int>>();
            _pickedIndices.Clear();
            _pickedDefs.Clear();
            _disabledIndices.Clear();
            _mandatoryCardNames.Clear();
            _picksAllowed = Mathf.Clamp(picksAllowed, 1, candidates.Count);
            _candidateRefs = candidates;
            if (mandatoryCardNames != null)
            {
                foreach (var name in mandatoryCardNames.Where(name => !string.IsNullOrEmpty(name) && candidates.Exists(c => c != null && c.CardName == name)))
                {
                    _mandatoryCardNames.Add(name);
                }
            }

            ParkButtonsIntoOverlay();
            await ShowOverlayAsync();

            if (_titleText != null) _titleText.text = "ФАЗА ДРАФТА";
            if (_subtitleText != null) _subtitleText.text = $"Выбери {_picksAllowed} карты для следующего хода";
            if (_counterText != null) _counterText.transform.parent.gameObject.SetActive(true);
            UpdateCounter();

            int shown = 0;
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (i < candidates.Count && candidates[i] != null)
                {
                    int capturedIndex = i;
                    choiceButtons[i].gameObject.SetActive(true);
                    var cg = choiceButtons[i].GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        cg.alpha = 1f;
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    }
                    choiceButtons[i].Setup(candidates[i], def => OnDraftCardChosen(capturedIndex, def));
                    bool isMandatory = _mandatoryCardNames.Contains(candidates[i].CardName);
                    choiceButtons[i].SetMandatory(isMandatory);
                    int delayIdx = shown++;
                    _ = choiceButtons[i].PlayAppearAsync(delayIdx * staggerDelay);
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }

            return await _multiTcs.Task;
        }

        // -- Pick handling ----------------------------------------------------------------

        private void UpdateCounter()
        {
            int picked = _pickedIndices.Count;
            if (_counterText != null) _counterText.text = $"{picked} / {_picksAllowed}";
            if (_progressFill != null)
            {
                float f = _picksAllowed > 0 ? Mathf.Clamp01((float)picked / _picksAllowed) : 0f;
                var sz = _progressFill.rectTransform.sizeDelta;
                sz.x = _progressBarMaxWidth * f;
                _progressFill.rectTransform.sizeDelta = sz;
            }
        }

        private void OnCardChosen(CardDef chosen)
        {
            _ = FinishSingleAsync(chosen);
        }

        private async UniTask FinishSingleAsync(CardDef chosen)
        {
            await HideOverlayAsync();
            RestoreButtons();
            _tcs.TrySetResult(chosen);
        }

        private void OnDraftCardChosen(int index, CardDef chosen)
        {
            if (chosen == null) return;

            // Toggle: if this card is already picked, un-pick it.
            int existingPickSlot = _pickedIndices.IndexOf(index);
            if (existingPickSlot >= 0)
            {
                // Mandatory cards can't be un-picked when there would no longer be enough
                // remaining pick slots to recover every required name.
                if (_mandatoryCardNames.Contains(chosen.CardName) && WouldBreakMandatoryCoverage(index))
                {
                    _ = FlashMandatoryAsync();
                    return;
                }

                _pickedIndices.RemoveAt(existingPickSlot);
                _pickedDefs.RemoveAt(existingPickSlot);
                _disabledIndices.Remove(index);
                UpdateCounter();

                if (index >= 0 && index < choiceButtons.Count && choiceButtons[index] != null)
                    _ = choiceButtons[index].PlayUnpickedAsync();
                return;
            }

            // Picking a new card: enforce the full mandatory set, not just one name.
            if (WouldBreakMandatoryCoverage(index, chosen.CardName))
            {
                _ = FlashMandatoryAsync();
                return;
            }

            if (_pickedIndices.Count >= _picksAllowed) return; // safety

            _disabledIndices.Add(index);
            _pickedIndices.Add(index);
            _pickedDefs.Add(chosen);
            UpdateCounter();

            if (index >= 0 && index < choiceButtons.Count && choiceButtons[index] != null)
                _ = choiceButtons[index].PlayPickedAsync();

            if (_pickedIndices.Count >= _picksAllowed)
                _ = FinishDraftAsync();
        }

        private async UniTask FinishDraftAsync()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(0.35f), DelayType.UnscaledDeltaTime);
            await HideOverlayAsync();
            RestoreButtons();
            var result = new List<int>(_pickedIndices);
            _pickedIndices.Clear();
            _pickedDefs.Clear();
            _disabledIndices.Clear();
            _multiTcs.TrySetResult(result);
        }

        private async UniTask FlashMandatoryAsync()
        {
            if (_candidateRefs == null) return;
            for (int i = 0; i < _candidateRefs.Count && i < choiceButtons.Count; i++)
            {
                if (_candidateRefs[i] == null || !_mandatoryCardNames.Contains(_candidateRefs[i].CardName)) continue;
                var rect = choiceButtons[i].transform as RectTransform;
                if (rect == null) continue;
                Vector2 origin = rect.anchoredPosition;
                for (int s = 0; s < 6; s++)
                {
                    rect.anchoredPosition = origin + new Vector2((s % 2 == 0 ? 1 : -1) * 8f, 0f);
                    await UniTask.Delay(TimeSpan.FromSeconds(0.03f), DelayType.UnscaledDeltaTime);
                }
                rect.anchoredPosition = origin;
            }
        }

        private bool WouldBreakMandatoryCoverage(int indexToRemove, string candidateName = null)
        {
            var remainingSlotsAfterAction = _picksAllowed - (_pickedIndices.Count + (candidateName == null ? -1 : 1));
            var pickedNames = _pickedDefs
                .Where((_, i) => i != _pickedIndices.IndexOf(indexToRemove))
                .Select(card => card?.CardName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
            if (!string.IsNullOrEmpty(candidateName) && !pickedNames.Contains(candidateName))
            {
                pickedNames.Add(candidateName);
            }
            var missingMandatory = _mandatoryCardNames.Count(name => !pickedNames.Contains(name));
            return missingMandatory > remainingSlotsAfterAction;
        }
    }
}
