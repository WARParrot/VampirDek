using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Definitions;
using Shared.Localization;
using Shared.UI;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Combat.UI
{
    public class CardChoiceButton : MonoBehaviour
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private Button button;
        [SerializeField] private GameObject mandatoryHighlight;
        [SerializeField] private TextMeshProUGUI mandatoryLabel;

        private CardDef _card;
        private Action<CardDef> _callback;
        private GameObject _cardInstance;
        private CanvasGroup _cardCanvasGroup;
        private RectTransform _cardRect;
        private CanvasGroup _mandatoryHighlightCanvasGroup;
        private float _glowPulseT;

        public void Setup(CardDef card, Action<CardDef> callback)
        {
            _card = card;
            _callback = callback;

            if (_cardInstance) Destroy(_cardInstance);

            // Hide the host Button's own UI panel and its child "Button"-label so the white
            // sprite background doesn't sit under the card art.
            var hostImg = GetComponent<Image>();
            if (hostImg != null) hostImg.enabled = false;
            var hostBtn = GetComponent<Button>();
            if (hostBtn != null) hostBtn.enabled = false;
            foreach (var label in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label != null && label.transform.parent == transform)
                    label.enabled = false;
            }

            if (cardPrefab != null && container != null)
                _cardInstance = SetupFromPrefab(card);
            else
                _cardInstance = BuildProceduralCard(card);

            _cardRect = _cardInstance.transform as RectTransform;
            _cardCanvasGroup = _cardInstance.GetComponent<CanvasGroup>();
            if (_cardCanvasGroup == null) _cardCanvasGroup = _cardInstance.AddComponent<CanvasGroup>();

            EnsureMandatoryHighlight();
            SetMandatory(false);
            _restingCaptured = false; // recapture next time picked
        }

        private GameObject SetupFromPrefab(CardDef card)
        {
            var go = Instantiate(cardPrefab, container);

            // Keep CardView at its native size and scale the WHOLE card uniformly so internal
            // anchors don't shift relative to each other (which makes text "float" off).
            if (go.transform is RectTransform cvRect)
            {
                cvRect.anchorMin = new Vector2(0.5f, 0.5f);
                cvRect.anchorMax = new Vector2(0.5f, 0.5f);
                cvRect.pivot = new Vector2(0.5f, 0.5f);
                cvRect.anchoredPosition = Vector2.zero;

                var hostRect = transform as RectTransform;
                if (hostRect != null && cvRect.rect.width > 0.01f && cvRect.rect.height > 0.01f)
                {
                    float sx = hostRect.rect.width / cvRect.rect.width;
                    float sy = hostRect.rect.height / cvRect.rect.height;
                    float s = Mathf.Min(sx, sy);
                    cvRect.localScale = new Vector3(s, s, 1f);
                }
                else
                {
                    cvRect.localScale = Vector3.one;
                }
            }

            var nameText = go.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            if (nameText) nameText.text = LocalizationService.CardName(card);

            var costText = go.transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();
            if (costText)
            {
                var costStr = card.Costs != null
                    ? string.Join(" ", card.Costs.ConvertAll(CardRulesText.FormatCostText))
                    : string.Empty;
                costText.text = costStr;
            }

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnCardClicked);
            return go;
        }

        private GameObject BuildProceduralCard(CardDef card)
        {
            // Host the procedural card directly under this button's RectTransform so the
            // existing layout (anchored position chosen by CardSelectionUI) is preserved.
            var parentRect = (transform is RectTransform pr) ? pr : null;

            // Hide the default grey Button image so we don't see it behind the card.
            var btnImg = GetComponent<Image>();
            if (btnImg != null) btnImg.enabled = false;
            var btnSelf = GetComponent<Button>();
            if (btnSelf != null) btnSelf.enabled = false;

            var root = new GameObject("ProcCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.layer = gameObject.layer;
            var rt = (RectTransform)root.transform;
            rt.SetParent(parentRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(180f, 260f);

            var typeColor = ColorForType(card.Type);

            var bg = root.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.08f, 0.14f, 0.96f);
            bg.raycastTarget = true;

            var outline = root.AddComponent<Outline>();
            outline.effectColor = typeColor;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var btn = root.GetComponent<Button>();
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            btn.onClick.AddListener(OnCardClicked);

            // Colored header strip with the card name.
            var header = CreateUiChild(rt, "Header", new Color(typeColor.r, typeColor.g, typeColor.b, 0.85f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 40f));
            CreateLabel(header.rectTransform, "Name", LocalizationService.CardName(card),
                18, FontStyles.Bold, TextAlignmentOptions.Center, Color.white,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // Big art placeholder: tinted block with the card's type initial.
            var art = CreateUiChild(rt, "Art", new Color(typeColor.r * 0.55f, typeColor.g * 0.55f, typeColor.b * 0.55f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(-12f, 120f));
            CreateLabel(art.rectTransform, "Glyph", GlyphForType(card.Type),
                72, FontStyles.Bold, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.92f),
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            // Type tag below the art.
            CreateLabel(rt, "TypeTag", LocalizationService.T($"card.type.{card.Type}", card.Type.ToString()).ToUpper(),
                13, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center,
                new Color(0.95f, 0.85f, 0.55f, 1f),
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -168f), new Vector2(0f, 18f));

            // Stats row: ATK on left, HP on right.
            CreateStatBadge(rt, "Atk", card.Attack.ToString(), new Color(0.85f, 0.25f, 0.2f),
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(22f, 24f));
            CreateStatBadge(rt, "Hp", card.Health.ToString(), new Color(0.25f, 0.7f, 0.35f),
                new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(-22f, 24f));

            // Cost badge in top-right corner (mana-purple).
            var costStr = card.Costs != null && card.Costs.Count > 0
                ? string.Join(" ", card.Costs.ConvertAll(CardRulesText.FormatCostText))
                : "0";
            CreateStatBadge(rt, "Cost", costStr, new Color(0.45f, 0.35f, 0.85f),
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(-22f, -22f));

            // Speed band (only if meaningful).
            if (card.MaxSpeed > 0)
            {
                CreateLabel(rt, "Speed", $"<color=#ffd864>SPD</color> {card.MinSpeed}-{card.MaxSpeed}",
                    11, FontStyles.Italic, TextAlignmentOptions.Center,
                    new Color(0.85f, 0.85f, 0.85f, 0.9f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 6f), new Vector2(0f, 14f));
            }

            return root;
        }

        private static Image CreateUiChild(RectTransform parent, string name, Color color,
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
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
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
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void CreateStatBadge(RectTransform parent, string name, string text, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(36f, 36f);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            CreateLabel(rt, "Value", text, 18, FontStyles.Bold, TextAlignmentOptions.Center,
                Color.white, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        private static Color ColorForType(CardType type)
        {
            switch (type)
            {
                case CardType.Vanguard: return new Color(0.78f, 0.20f, 0.22f, 1f);
                case CardType.Building: return new Color(0.30f, 0.55f, 0.85f, 1f);
                case CardType.Human:    return new Color(0.40f, 0.75f, 0.40f, 1f);
                case CardType.Town:     return new Color(0.85f, 0.65f, 0.25f, 1f);
            }
            return new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        private static string GlyphForType(CardType type)
        {
            // Keep these labels ASCII-only: several TMP fallback assets miss emoji/symbol glyphs,
            // which renders tofu boxes on procedural cards.
            switch (type)
            {
                case CardType.Vanguard: return "ATK";
                case CardType.Building: return "BLD";
                case CardType.Human:    return "HUM";
                case CardType.Town:     return "TWN";
            }
            return "?";
        }

        private void EnsureMandatoryHighlight()
        {
            if (mandatoryHighlight != null) return;

            var parentRect = (transform is RectTransform pr) ? pr : null;
            if (parentRect == null) return;

            var go = new GameObject("MandatoryHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parentRect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(196f, 276f);
            rt.SetAsFirstSibling();

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 0.78f, 0.25f, 0.55f);
            img.raycastTarget = false;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.9f, 0.4f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            mandatoryHighlight = go;
            _mandatoryHighlightCanvasGroup = go.GetComponent<CanvasGroup>();
        }

        public void SetMandatory(bool isMandatory)
        {
            if (mandatoryHighlight != null)
            {
                mandatoryHighlight.SetActive(isMandatory);
                if (_mandatoryHighlightCanvasGroup == null)
                    _mandatoryHighlightCanvasGroup = mandatoryHighlight.GetComponent<CanvasGroup>();
            }
            if (mandatoryLabel != null) mandatoryLabel.gameObject.SetActive(isMandatory);
            enabled = isMandatory;
            _glowPulseT = 0f;
        }

        private void Update()
        {
            if (mandatoryHighlight == null || !mandatoryHighlight.activeSelf) return;
            _glowPulseT += Time.unscaledDeltaTime;
            if (_mandatoryHighlightCanvasGroup != null)
                _mandatoryHighlightCanvasGroup.alpha = 0.55f + 0.45f * Mathf.Sin(_glowPulseT * 4f);
        }

        public async UniTask PlayAppearAsync(float delay, float duration = 0.28f)
        {
            if (_cardRect == null || _cardCanvasGroup == null) return;

            _cardCanvasGroup.alpha = 0f;
            _cardRect.localScale = Vector3.one * 0.6f;
            var startPos = _cardRect.anchoredPosition + new Vector2(0f, -40f);
            var endPos = _cardRect.anchoredPosition;
            _cardRect.anchoredPosition = startPos;

            if (delay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(delay), DelayType.UnscaledDeltaTime);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                _cardCanvasGroup.alpha = eased;
                _cardRect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, eased);
                _cardRect.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                await UniTask.Yield();
            }
            _cardCanvasGroup.alpha = 1f;
            _cardRect.localScale = Vector3.one;
            _cardRect.anchoredPosition = endPos;
        }

        private Vector2 _restingPos;
        private Vector3 _restingScale = Vector3.one;
        private bool _restingCaptured;

        public async UniTask PlayPickedAsync(float duration = 0.28f)
        {
            if (_cardRect == null || _cardCanvasGroup == null) return;

            if (!_restingCaptured)
            {
                _restingPos = _cardRect.anchoredPosition;
                _restingScale = _cardRect.localScale;
                _restingCaptured = true;
            }

            // Pop, then settle into a "lifted" picked state. Keeps interactable=true so the
            // player can click again to un-pick.
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float pop = k < 0.4f ? Mathf.Lerp(1f, 1.12f, k / 0.4f) : Mathf.Lerp(1.12f, 0.92f, (k - 0.4f) / 0.6f);
                _cardRect.localScale = _restingScale * pop;
                _cardRect.anchoredPosition = _restingPos + new Vector2(0f, -22f * k);
                _cardCanvasGroup.alpha = 1f - k * 0.35f;
                await UniTask.Yield();
            }
            _cardRect.localScale = _restingScale * 0.92f;
            _cardRect.anchoredPosition = _restingPos + new Vector2(0f, -22f);
            _cardCanvasGroup.alpha = 0.65f;
            _cardCanvasGroup.interactable = true;
            _cardCanvasGroup.blocksRaycasts = true;
        }

        public async UniTask PlayUnpickedAsync(float duration = 0.22f)
        {
            if (_cardRect == null || _cardCanvasGroup == null) return;
            if (!_restingCaptured)
            {
                _restingPos = _cardRect.anchoredPosition;
                _restingScale = _cardRect.localScale;
                _restingCaptured = true;
            }

            var startScale = _cardRect.localScale;
            var startPos = _cardRect.anchoredPosition;
            var startAlpha = _cardCanvasGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - k, 3f);
                _cardRect.localScale = Vector3.Lerp(startScale, _restingScale, eased);
                _cardRect.anchoredPosition = Vector2.Lerp(startPos, _restingPos, eased);
                _cardCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, eased);
                await UniTask.Yield();
            }
            _cardRect.localScale = _restingScale;
            _cardRect.anchoredPosition = _restingPos;
            _cardCanvasGroup.alpha = 1f;
            _cardCanvasGroup.interactable = true;
            _cardCanvasGroup.blocksRaycasts = true;
        }

        private void OnCardClicked()
        {
            Debug.Log($"[CardChoice] Clicked on {_card?.CardName}, callback is {(_callback != null ? "set" : "NULL")}");
            _callback?.Invoke(_card);
        }

        private void OnDestroy()
        {
            if (_cardInstance)
            {
                var btn = _cardInstance.GetComponent<Button>();
                if (btn) btn.onClick.RemoveListener(OnCardClicked);
            }
        }
    }
}
