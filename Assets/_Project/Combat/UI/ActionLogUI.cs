using System.Collections.Generic;
using Combat;
using Core;
using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.UI
{
    /// <summary>
    /// Right-side panel that lists recent combat events: phase changes, card plays, deaths,
    /// clashes, damage. Builds itself at runtime so no scene work is required.
    /// </summary>
    public class ActionLogUI : MonoBehaviour
    {
        private const int MaxLines = 30;
        private const int VisibleLines = 7;
        private const string PanelTitle = "ИСТОРИЯ";

        private static ActionLogUI _instance;

        private readonly LinkedList<string> _lines = new();
        private RectTransform _linesContainer;
        private GameObject _panelRoot;
        private VerticalLayoutGroup _layout;
        private readonly List<TextMeshProUGUI> _lineLabels = new();
        private bool _subscribed;
        private int _currentTurn;

        // Cached references rebuilt each duel.
        private SideState _playerSide;
        private SideState _opponentSide;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ActionLogUI]");
            _instance = go.AddComponent<ActionLogUI>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            // BuildPanel + TrySubscribe deferred to Update — Canvas creation during scene-load
            // Awake races CanvasUpdateRegistry; subscribe needs DI Resolver to be ready.
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (_panelRoot == null) BuildPanel();
            if (!_subscribed) TrySubscribe();
            CacheSides();
            RefreshVisibility();
        }

        private bool _panelLastActive = true;

        private void RefreshVisibility()
        {
            if (_panelRoot == null) return;
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            bool inDuel = state != null && state.CurrentPhase != null;
            if (_panelLastActive == inDuel) return; // edge-trigger only
            _panelLastActive = inDuel;
            _panelRoot.SetActive(inDuel);
        }

        // -- Panel construction --------------------------------------------------

        private void BuildPanel()
        {
            var canvas = FindScreenOverlayCanvas();
            if (canvas == null)
            {
                // Build our own overlay if no scene-wide one exists yet — same approach as
                // the draft overlay, with a slightly lower sortingOrder so the log sits
                // behind modal dialogs but above world UI.
                var co = new GameObject("ActionLogCanvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var c = co.GetComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 200;
                var scaler = co.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                co.GetComponent<GraphicRaycaster>().enabled = false; // log doesn't take clicks
                canvas = c;
            }

            _panelRoot = new GameObject("ActionLogPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_panelRoot.transform;
            // Compact panel anchored top-right corner — fixed small box, not a full-height bar.
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-18f, -18f);
            rt.sizeDelta = new Vector2(260f, 220f);
            var bg = _panelRoot.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.04f, 0.10f, 0.88f);
            bg.raycastTarget = false;
            bg.maskable = false;
            var ol = _panelRoot.AddComponent<Outline>();
            ol.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.7f);
            ol.effectDistance = new Vector2(1.5f, -1.5f);

            // Header bar — small gold strip with the title centered.
            var headerGo = new GameObject("Header",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            headerGo.transform.SetParent(_panelRoot.transform, false);
            var headerImg = headerGo.GetComponent<Image>();
            headerImg.color = new Color(0.18f, 0.13f, 0.05f, 0.95f);
            headerImg.raycastTarget = false;
            headerImg.maskable = false;
            var hrt = (RectTransform)headerGo.transform;
            hrt.anchorMin = new Vector2(0f, 1f);
            hrt.anchorMax = new Vector2(1f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0f, 26f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = PanelTitle;
            titleTmp.fontSize = 14f;
            titleTmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = new Color(1f, 0.88f, 0.5f, 1f);
            titleTmp.raycastTarget = false;
            titleTmp.maskable = false;
            titleTmp.characterSpacing = 4f;
            var trt = titleTmp.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Thin golden line under the header.
            var divGo = new GameObject("Divider",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divGo.transform.SetParent(_panelRoot.transform, false);
            var div = divGo.GetComponent<Image>();
            div.color = new Color(0.95f, 0.78f, 0.32f, 1f);
            div.raycastTarget = false;
            div.maskable = false;
            var drt = div.rectTransform;
            drt.anchorMin = new Vector2(0f, 1f);
            drt.anchorMax = new Vector2(1f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = new Vector2(0f, -26f);
            drt.sizeDelta = new Vector2(0f, 1f);

            // Lines container.
            var listGo = new GameObject("Lines", typeof(RectTransform), typeof(VerticalLayoutGroup));
            listGo.transform.SetParent(_panelRoot.transform, false);
            _linesContainer = (RectTransform)listGo.transform;
            _linesContainer.anchorMin = new Vector2(0f, 0f);
            _linesContainer.anchorMax = new Vector2(1f, 1f);
            _linesContainer.pivot = new Vector2(0.5f, 0f);
            _linesContainer.offsetMin = new Vector2(10f, 8f);
            _linesContainer.offsetMax = new Vector2(-10f, -32f);
            _layout = listGo.GetComponent<VerticalLayoutGroup>();
            _layout.childAlignment = TextAnchor.LowerLeft;
            _layout.spacing = 1f;
            _layout.childControlWidth = true;
            _layout.childControlHeight = true;
            _layout.childForceExpandWidth = true;
            _layout.childForceExpandHeight = false;

            for (int i = 0; i < VisibleLines; i++)
            {
                var lineGo = new GameObject($"L{i}", typeof(RectTransform));
                lineGo.transform.SetParent(_linesContainer, false);
                var tmp = lineGo.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 12f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = new Color(0.92f, 0.9f, 0.85f, 1f);
                tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                tmp.raycastTarget = false;
                tmp.maskable = false;
                tmp.richText = true;
                tmp.text = "";
                _lineLabels.Add(tmp);
            }
        }

        private static Canvas FindScreenOverlayCanvas()
        {
            var all = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in all)
                if (c != null && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return c;
            return null;
        }

        // -- Event subscription --------------------------------------------------

        private void TrySubscribe()
        {
            if (_subscribed) return;
            EventBus bus;
            try { bus = GlobalServices.EventBus; }
            catch { return; } // Resolver not ready yet
            if (bus == null) return;

            bus.Subscribe<PhaseEnterEvent>(OnPhaseEnter);
            bus.Subscribe<PlacedCardEvent>(OnCardPlaced);
            bus.Subscribe<EntityDiedEvent>(OnEntityDied);
            bus.Subscribe<DamageDealtEvent>(OnDamageDealt);
            bus.Subscribe<ClashResolvedEvent>(OnClashResolved);
            bus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            bus.Subscribe<PlaceFailedEvent>(OnPlaceFailed);
            bus.Subscribe<DuelStartedEvent>(OnDuelStarted);
            bus.Subscribe<DuelEndedEvent>(OnDuelEnded);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var bus = GlobalServices.EventBus;
            if (bus == null) return;
            bus.Unsubscribe<PhaseEnterEvent>(OnPhaseEnter);
            bus.Unsubscribe<PlacedCardEvent>(OnCardPlaced);
            bus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
            bus.Unsubscribe<DamageDealtEvent>(OnDamageDealt);
            bus.Unsubscribe<ClashResolvedEvent>(OnClashResolved);
            bus.Unsubscribe<CardDrawnEvent>(OnCardDrawn);
            bus.Unsubscribe<PlaceFailedEvent>(OnPlaceFailed);
            bus.Unsubscribe<DuelStartedEvent>(OnDuelStarted);
            bus.Unsubscribe<DuelEndedEvent>(OnDuelEnded);
            _subscribed = false;
        }

        private void CacheSides()
        {
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            if (state == null) return;
            _playerSide = state.PlayerSide;
            _opponentSide = state.OpponentSide;
        }

        // -- Handlers ------------------------------------------------------------

        private void OnDuelStarted(DuelStartedEvent _)
        {
            _lines.Clear();
            _currentTurn = 0;
            Render();
        }

        private void OnDuelEnded(DuelEndedEvent _)
        {
            AddLine("<color=#ffd17a><b>↻ Дуэль завершена</b></color>");
        }

        private void OnPhaseEnter(PhaseEnterEvent e)
        {
            if (e.PhaseId == "StartOfTurn")
            {
                _currentTurn++;
                AddLine($"<color=#ffd17a><b>──  Ход {_currentTurn}  ──</b></color>");
                return;
            }
            string label = e.PhaseId switch
            {
                "BuildingPhase"      => "↻ Фаза строительства",
                "PlanningPhase"      => "↻ Фаза планирования",
                "ClashingPhase"      => "↻ Фаза клэша",
                "OneSidedAttackPhase" => "↻ Атака",
                "Loot"               => "↻ Лут",
                _ => null,
            };
            if (label != null) AddLine($"<color=#9c9aa0>{label}</color>");
        }

        private void OnCardPlaced(PlacedCardEvent e)
        {
            if (e.Card == null) return;
            bool isPlayer = OwnsCard(e.Card, _playerSide);
            string who = isPlayer ? "<color=#5fb8ff>Ты</color>" : "<color=#ff7a6e>Противник</color>";
            string name = SafeCardName(e.Card);
            AddLine($"► {who}: {name}");
        }

        private void OnEntityDied(EntityDiedEvent e)
        {
            string name = SafeEntityName(e.Entity);
            bool isPlayer = OwnsEntity(e.Entity, _playerSide);
            string tag = isPlayer ? "<color=#ff9577>твоя</color>" : "<color=#ffd17a>врага</color>";
            AddLine($"<color=#ff7a4a>✕</color> {name} ({tag}) пал");
        }

        private void OnDamageDealt(DamageDealtEvent e)
        {
            // Skip the dying blow — EntityDied will surface it cleaner.
            if (e.Target != null && !e.Target.IsAlive) return;
            if (e.Amount <= 0) return;
            string name = SafeEntityName(e.Target);
            AddLine($"<color=#ff8d6e>−{e.Amount}</color> {name} ({e.Target?.Health}/{e.Target?.MaxHealth})");
        }

        private void OnClashResolved(ClashResolvedEvent e)
        {
            string winner = SafeEntityName(e.Winner);
            string loser = SafeEntityName(e.Loser);
            AddLine($"<color=#ffd864>ATK</color> {winner} побеждает {loser}");
        }

        private void OnCardDrawn(CardDrawnEvent e)
        {
            if (e.Side != _playerSide) return; // не светим карты противника
            string name = SafeICardName(e.Card);
            AddLine($"<color=#7ad48b>+</color> Взял: {name}");
        }

        private void OnPlaceFailed(PlaceFailedEvent e)
        {
            AddLine($"<color=#ffb14a>⚠</color> Не получилось: {e.CardName} ({e.Reason})");
        }

        // -- Helpers --------------------------------------------------------------

        private bool OwnsCard(BoardCard card, SideState side)
        {
            if (card == null || side?.Board == null) return false;
            foreach (var slot in side.Board.AllSlots())
                if (slot != null && slot.Occupant == card) return true;
            return false;
        }

        private bool OwnsEntity(IGameEntity entity, SideState side)
        {
            if (entity == null || side == null) return false;
            if (entity == side.Town) return true;
            foreach (var slot in side.Board.AllSlots())
                if (slot != null && slot.Occupant == entity) return true;
            return false;
        }

        private static string SafeCardName(BoardCard c)
        {
            if (c?.SourceCard == null) return "?";
            return Shared.Localization.LocalizationService.CardName(c.SourceCard);
        }

        private static string SafeICardName(ICard c)
        {
            if (c == null) return "?";
            var def = c.Def;
            return def != null ? Shared.Localization.LocalizationService.CardName(def) : "?";
        }

        private static string SafeEntityName(IGameEntity e)
        {
            if (e is BoardCard bc) return SafeCardName(bc);
            return e?.GetType().Name ?? "?";
        }

        private void AddLine(string formattedText)
        {
            _lines.AddLast(formattedText);
            while (_lines.Count > MaxLines) _lines.RemoveFirst();
            Render();
        }

        private void Render()
        {
            int total = _lines.Count;
            int show = Mathf.Min(VisibleLines, total);
            int startIdx = total - show;

            int li = 0;
            var node = _lines.First;
            for (int i = 0; i < startIdx && node != null; i++) node = node.Next;

            // Fill the visible slots from the bottom up so the newest line sits flush with
            // the bottom of the panel (LayoutGroup is LowerLeft).
            int blanks = VisibleLines - show;
            for (int b = 0; b < blanks && li < _lineLabels.Count; b++)
            {
                _lineLabels[li++].text = "";
            }
            while (node != null && li < _lineLabels.Count)
            {
                _lineLabels[li++].text = node.Value;
                node = node.Next;
            }
            while (li < _lineLabels.Count) _lineLabels[li++].text = "";
        }
    }
}
