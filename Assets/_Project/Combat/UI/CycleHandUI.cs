using System.Collections.Generic;
using Combat;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    /// <summary>
    /// "Cycle hand" — once per turn during BuildingPhase the player can discard a card and
    /// draw a new one, paying 1 HR. Surfaces as a button next to the hand; clicking it opens
    /// a picker overlay listing the current hand. Built fully at runtime, no scene work needed.
    /// </summary>
    public class CycleHandUI : MonoBehaviour
    {
        private const int HrCost = 1;
        private const string Label = "↻ Замена (-1 HR)";

        private static CycleHandUI _instance;

        private GameObject _buttonRoot;
        private Button _button;
        private TextMeshProUGUI _buttonText;
        private GameObject _pickerOverlay;
        private RectTransform _pickerList;
        private bool _usedThisTurn;
        private bool _subscribed;
        private int _lastTurnPhaseSignature;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[CycleHandUI]");
            _instance = go.AddComponent<CycleHandUI>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // BuildButton + TrySubscribe deferred to Update — creating canvases during
            // scene-load Awake can race CanvasUpdateRegistry and IndexedSet errors fire.
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_buttonRoot == null) BuildButton();
            if (!_subscribed) TrySubscribe();
            RefreshButtonState();
        }

        // -- UI construction -----------------------------------------------------

        private void BuildButton()
        {
            // Use our own dedicated screen-space-overlay canvas so the button is always
            // on top of any other UI and its clicks are never eaten by sibling raycasters.
            var canvasGo = new GameObject("CycleHandCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasGo);
            var c = canvasGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 800;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _buttonRoot = new GameObject("CycleHandButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _buttonRoot.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)_buttonRoot.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(28f, 32f);
            rt.sizeDelta = new Vector2(220f, 56f);

            var img = _buttonRoot.GetComponent<Image>();
            img.color = new Color(0.10f, 0.07f, 0.16f, 0.95f);
            img.raycastTarget = true;
            img.maskable = false;
            var ol = _buttonRoot.AddComponent<Outline>();
            ol.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.85f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            _button = _buttonRoot.GetComponent<Button>();
            _button.targetGraphic = img;
            _button.onClick.AddListener(OnButtonClicked);

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_buttonRoot.transform, false);
            _buttonText = textGo.AddComponent<TextMeshProUGUI>();
            _buttonText.text = Label;
            _buttonText.fontSize = 20f;
            _buttonText.fontStyle = FontStyles.Bold;
            _buttonText.alignment = TextAlignmentOptions.Center;
            _buttonText.color = new Color(1f, 0.92f, 0.7f, 1f);
            _buttonText.raycastTarget = false;
            _buttonText.maskable = false;
            var trt = _buttonText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 4f);
            trt.offsetMax = new Vector2(-8f, -4f);

            _buttonRoot.SetActive(false);
        }

        private static Canvas FindScreenOverlayCanvas()
        {
            var all = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in all)
                if (c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return c;
            return null;
        }

        // -- State / event handling ---------------------------------------------

        private void TrySubscribe()
        {
            if (_subscribed) return;
            EventBus bus;
            try { bus = GlobalServices.EventBus; }
            catch { return; } // Resolver not ready yet
            if (bus == null) return;
            bus.Subscribe<PhaseEnterEvent>(OnPhaseEnter);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var bus = GlobalServices.EventBus;
            if (bus == null) return;
            bus.Unsubscribe<PhaseEnterEvent>(OnPhaseEnter);
            _subscribed = false;
        }

        private void OnPhaseEnter(PhaseEnterEvent e)
        {
            // Reset the once-per-turn limit at the start of each turn.
            if (e.PhaseId == "StartOfTurn") _usedThisTurn = false;
        }

        private bool _buttonActiveState = true;
        private void SetButtonActive(bool active)
        {
            if (_buttonRoot == null || _buttonActiveState == active) return;
            _buttonActiveState = active;
            _buttonRoot.SetActive(active);
        }

        private void RefreshButtonState()
        {
            if (_buttonRoot == null)
            {
                // Canvas might have arrived after Awake. Try again.
                if (FindScreenOverlayCanvas() != null) BuildButton();
                if (_buttonRoot == null) return;
            }

            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            if (state == null) { SetButtonActive(false); return; }

            bool inBuilding = state.CurrentPhase != null && state.CurrentPhase.Tags.Contains("BuildingPhase");
            var side = state.PlayerSide;
            if (!inBuilding || side == null) { SetButtonActive(false); return; }

            SetButtonActive(true);

            bool hasHand = side.Hand.Count > 0;
            bool hasDeck = side.Deck != null && side.Deck.RemainingCards > 0;
            bool canPay = side.HumanResources >= HrCost;
            bool ready = !_usedThisTurn && hasHand && hasDeck && canPay;

            // Always keep the button interactable so it shows clear feedback on click instead
            // of dying silently when prerequisites aren't met.
            if (!_button.interactable) _button.interactable = true;

            Color wantColor = ready
                ? new Color(1f, 0.92f, 0.7f, 1f)
                : new Color(0.7f, 0.65f, 0.55f, 1f);
            if (_lastTextColor != wantColor) { _buttonText.color = wantColor; _lastTextColor = wantColor; }

            string wantText;
            if (_usedThisTurn) wantText = "↻ Замена (использовано)";
            else if (!canPay) wantText = "↻ Замена (нужен 1 HR)";
            else if (!hasHand) wantText = "↻ Замена (рука пуста)";
            else if (!hasDeck) wantText = "↻ Замена (нет колоды)";
            else wantText = Label;
            if (wantText != _lastButtonText) { _buttonText.text = wantText; _lastButtonText = wantText; }
        }

        private Color _lastTextColor;
        private string _lastButtonText;

        // -- Picker overlay ------------------------------------------------------

        private void OnButtonClicked()
        {
            Debug.Log($"[CycleHand] Button clicked. usedThisTurn={_usedThisTurn}, pickerOpen={_pickerOverlay?.activeSelf == true}");
            if (_pickerOverlay != null && _pickerOverlay.activeSelf) { ClosePicker(); return; }

            // Clear feedback when something blocks the cycle.
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            var side = state?.PlayerSide;
            if (side == null) return;
            if (_usedThisTurn)        { Notify("Замена уже использована в этом ходу"); return; }
            if (side.Hand.Count == 0) { Notify("Рука пуста — нечего менять"); return; }
            if (side.Deck == null || side.Deck.RemainingCards == 0) { Notify("Колода пуста — нечего тянуть"); return; }
            if (side.HumanResources < HrCost) { Notify($"Нужен <color=#ffb14a>{HrCost} HR</color> для замены"); return; }

            OpenPicker();
        }

        private static void Notify(string message)
        {
            var w = ResourceWarningUI.Current;
            if (w != null) w.ShowWarningAsync(message).Forget();
            else Debug.Log($"[CycleHand] {message}");
        }

        private void OpenPicker()
        {
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            var side = state?.PlayerSide;
            if (side == null || side.Hand.Count == 0) return;

            if (_pickerOverlay == null) BuildPicker();
            if (_pickerOverlay == null) return;

            // Rebuild list every open — hand contents change.
            // Deactivate before Destroy so Graphics unregister synchronously and don't
            // race the new entries inside CanvasUpdateRegistry.PerformUpdate.
            for (int i = _pickerList.childCount - 1; i >= 0; i--)
            {
                var child = _pickerList.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            foreach (var card in side.Hand)
            {
                AddPickerEntry(card);
            }

            _pickerOverlay.SetActive(true);
        }

        private void ClosePicker()
        {
            if (_pickerOverlay != null) _pickerOverlay.SetActive(false);
        }

        private void BuildPicker()
        {
            // Parent picker on the same canvas as the button so it sits above other UI.
            var canvas = _buttonRoot != null ? _buttonRoot.transform.parent : null;
            if (canvas == null) return;

            _pickerOverlay = new GameObject("CyclePicker",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _pickerOverlay.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_pickerOverlay.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var vignette = _pickerOverlay.GetComponent<Image>();
            vignette.color = new Color(0.02f, 0.01f, 0.04f, 0.6f);
            vignette.raycastTarget = true;
            vignette.maskable = false;
            // Close on background click.
            var bgBtn = _pickerOverlay.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(ClosePicker);

            // Title.
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(_pickerOverlay.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "Какую карту сбросить?  (вернёшь -1 HR, возьмёшь новую)";
            title.fontSize = 28f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(1f, 0.88f, 0.5f, 1f);
            title.raycastTarget = false;
            title.maskable = false;
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -80f);
            trt.sizeDelta = new Vector2(1200f, 50f);

            // Scrollable list container (here a simple horizontal layout — no scroll for the 7-card max hand).
            var listGo = new GameObject("List",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            listGo.transform.SetParent(_pickerOverlay.transform, false);
            _pickerList = (RectTransform)listGo.transform;
            _pickerList.anchorMin = new Vector2(0.5f, 0.5f);
            _pickerList.anchorMax = new Vector2(0.5f, 0.5f);
            _pickerList.pivot = new Vector2(0.5f, 0.5f);
            _pickerList.anchoredPosition = new Vector2(0f, 0f);
            _pickerList.sizeDelta = new Vector2(1600f, 280f);
            var hlg = listGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
        }

        private void AddPickerEntry(ICard card)
        {
            var entryGo = new GameObject("Entry",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            entryGo.transform.SetParent(_pickerList, false);
            var rt = (RectTransform)entryGo.transform;
            rt.sizeDelta = new Vector2(180f, 260f);
            var img = entryGo.GetComponent<Image>();
            img.color = new Color(0.10f, 0.08f, 0.15f, 0.96f);
            img.maskable = false;
            var ol = entryGo.AddComponent<Outline>();
            ol.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            var btn = entryGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnCardPicked(card));

            // Name label.
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(entryGo.transform, false);
            var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = card?.Def != null ? Shared.Localization.LocalizationService.CardName(card.Def) : "?";
            nameTmp.fontSize = 18f;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.color = Color.white;
            nameTmp.raycastTarget = false;
            nameTmp.maskable = false;
            nameTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var nrt = nameTmp.rectTransform;
            nrt.anchorMin = new Vector2(0f, 1f);
            nrt.anchorMax = new Vector2(1f, 1f);
            nrt.pivot = new Vector2(0.5f, 1f);
            nrt.anchoredPosition = new Vector2(0f, -10f);
            nrt.sizeDelta = new Vector2(-12f, 60f);

            // Type/cost summary.
            string costStr = card?.Def?.Costs != null && card.Def.Costs.Count > 0
                ? string.Join(" ", card.Def.Costs.ConvertAll(Shared.UI.CardRulesText.FormatCostText))
                : "—";
            var infoGo = new GameObject("Info", typeof(RectTransform));
            infoGo.transform.SetParent(entryGo.transform, false);
            var info = infoGo.AddComponent<TextMeshProUGUI>();
            info.text = $"<color=#bfb4a4>{card?.Def?.Type}</color>\n{costStr}";
            info.fontSize = 15f;
            info.alignment = TextAlignmentOptions.Center;
            info.color = new Color(0.85f, 0.85f, 0.8f, 1f);
            info.raycastTarget = false;
            info.maskable = false;
            info.richText = true;
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f);
            irt.anchorMax = new Vector2(1f, 0f);
            irt.pivot = new Vector2(0.5f, 0f);
            irt.anchoredPosition = new Vector2(0f, 12f);
            irt.sizeDelta = new Vector2(-12f, 100f);
        }

        private void OnCardPicked(ICard card)
        {
            Debug.Log($"[CycleHand] Picked: {card?.Def?.CardName ?? "null"}");
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            var side = state?.PlayerSide;
            if (side == null) { Debug.LogWarning("[CycleHand] No player side"); ClosePicker(); return; }
            if (side.HumanResources < HrCost) { Debug.LogWarning($"[CycleHand] Not enough HR ({side.HumanResources}<{HrCost})"); ClosePicker(); return; }
            if (card is not Card concrete) { Debug.LogWarning("[CycleHand] Card is not concrete type"); ClosePicker(); return; }
            if (!side.Hand.Contains(concrete)) { Debug.LogWarning("[CycleHand] Card not in hand"); ClosePicker(); return; }

            // Pay, discard, draw — and emit events so log + UI react.
            side.PayHumanResources(HrCost);
            side.Hand.Remove(concrete);
            side.Graveyard.Add(concrete);
            try { GlobalServices.EventBus?.Publish(new CardDiscardedEvent(side, concrete)); } catch { }

            side.DrawCards(1);
            Debug.Log($"[CycleHand] Done. Hand={side.Hand.Count}, HR={side.HumanResources}");

            // Hand count is the same (removed 1, drew 1), so HandUIManager won't auto-refresh.
            // Force a redraw so the player sees the new card instead of the discarded one.
            var hand = HandUIManager.Current;
            if (hand != null) hand.RefreshHandImmediately();

            _usedThisTurn = true;
            ClosePicker();
        }
    }
}
