using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.UI;
using Core;
using Combat;
using Definitions;
using Shared.UI;
using Shared.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using FMODUnity;
public class HandUIManager : MonoBehaviour
{
    [Header("Prefabs & Layout")]
    public GameObject CardViewPrefab;
    public Transform HandContainer;
    [Header("References")]
    public BoardView BoardView;
    [Header("Status Texts")]
    public TextMeshProUGUI PlayerTownHPText, OpponentTownHPText, PlayerManaText, PlayerHumanResText, PhaseText;
    [Header("Warnings")]
    public ResourceWarningUI ResourceWarningUI;
    [Header("Tutorial")]
    public TutorialSystem TutorialSystem;
    private DuelManager _duelManager;
    private Dictionary<ICard, DragHandler> _cardViews = new();
    private DragHandler _currentlyDragging;
    private DragHandler _dropPreviewCard;
    private int _lastHandCount = -1;
    private bool _lastDragAllowed = false;
    [Header("HR Animation")]
    [SerializeField] private TextMeshProUGUI _hrDeltaText;
    [SerializeField] private float _hrDeltaDuration = 0.5f;
    [SerializeField] private Color _hrPositiveColor = Color.green;
    [SerializeField] private Color _hrNegativeColor = Color.red;
    private int _displayedHR;
    private string _lastPhaseId = null;
    private bool _statusTextConfigured;
    private int? _lastPlayerTownHp;
    private int? _lastOpponentTownHp;
    private int _lastHumanResources = int.MinValue;

    void Update()
    {
        _duelManager = DuelManagerProxy.Instance;
        if (_duelManager?.CurrentDuelState == null) return;
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

        EnsureReadableStatusText();

        // Reset the resource-warning counter every time we move to a new phase, so the
        // "first attempt is silent" rule resets after a turn/phase change.
        string phaseId = state.CurrentPhase?.PhaseId;
        if (phaseId != _lastPhaseId)
        {
            _failedAttempts.Clear();
            _lastPhaseId = phaseId;
            if (PhaseText != null)
                PhaseText.text = GetPhaseDisplayText(state.CurrentPhase);
        }

        int? playerTownHp = side.Town?.Health;
        if (playerTownHp != _lastPlayerTownHp)
        {
            _lastPlayerTownHp = playerTownHp;
            if (PlayerTownHPText != null)
                PlayerTownHPText.text = LocalizationService.TFormat("ui.town_hp", "Town HP: {0}", playerTownHp);
        }

        int? opponentTownHp = state.OpponentSide.Town?.Health;
        if (opponentTownHp != _lastOpponentTownHp)
        {
            _lastOpponentTownHp = opponentTownHp;
            if (OpponentTownHPText != null)
                OpponentTownHPText.text = LocalizationService.TFormat("ui.opponent_town_hp", "Opp Town HP: {0}", opponentTownHp);
        }

        if (_displayedHR != side.HumanResources)
        {
            int delta = side.HumanResources - _displayedHR;
            StartCoroutine(AnimateHRDelta(delta));
            _displayedHR = side.HumanResources;
        }

        bool humanResourcesChanged = side.HumanResources != _lastHumanResources;
        if (humanResourcesChanged)
        {
            _lastHumanResources = side.HumanResources;
            if (PlayerHumanResText != null)
                PlayerHumanResText.text = LocalizationService.TFormat("ui.hr", "HR: {0}", side.HumanResources);
        }

        bool handChanged = side.Hand.Count != _lastHandCount;
        if (handChanged)
            RefreshHand(side);

        bool allowDrag = CanStartCardDrag();
        bool dragPermissionChanged = allowDrag != _lastDragAllowed;
        if (handChanged || dragPermissionChanged)
        {
            foreach (var kv in _cardViews)
            {
                if (kv.Value != null) kv.Value.enabled = allowDrag;
            }
            _lastDragAllowed = allowDrag;
        }

        if (handChanged || dragPermissionChanged || humanResourcesChanged)
            ApplyHandCardAffordances(side, allowDrag);
    }
    private void EnsureReadableStatusText()
    {
        if (_statusTextConfigured) return;
        _statusTextConfigured = true;

        if (PlayerManaText != null)
            PlayerManaText.gameObject.SetActive(false);
        ConfigureHudText(PlayerTownHPText, 18f, 28f);
        ConfigureHudText(OpponentTownHPText, 18f, 28f);
        ConfigureHudText(PlayerHumanResText, 20f, 30f);
        ConfigureHudText(PhaseText, 20f, 30f);
    }
    private static void ConfigureHudText(TextMeshProUGUI text, float minSize, float maxSize)
    {
        if (text == null) return;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = maxSize;
        text.raycastTarget = false;
    }
    private static void EnsureCardTextReadable(GameObject cardView)
    {
        if (cardView == null) return;
        foreach (var text in cardView.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = Mathf.Max(text.fontSize, 18f);
            text.raycastTarget = false;
        }
    }
    private string GetPhaseDisplayText(PhaseNode phase)
    {
        if (phase == null) return string.Empty;
        if (phase.Tags.Contains("BuildingPhase"))
            return LocalizationService.T("phase.building", "Building Phase\nDrag a card onto the board or confirm the phase");
        if (phase.Tags.Contains("PlanningPhase"))
            return LocalizationService.T("phase.planning", "Planning Phase\nChoose attack targets");
        if (phase.Tags.Contains("ClashingPhase"))
            return LocalizationService.T("phase.clashing", "Clashing Phase");
        if (phase.Tags.Contains("OneSidedAttackPhase"))
            return LocalizationService.T("phase.one_sided_attack", "Attack Phase");
        if (phase.Tags.Contains("StartOfTurn"))
            return LocalizationService.T("phase.start_of_turn", "Start of turn");
        if (phase.Tags.Contains("EndOfTurn"))
            return LocalizationService.T("phase.end_of_turn", "End of turn");
        if (phase.Tags.Contains("DuelStart"))
            return LocalizationService.T("phase.duel_start", "Duel start");
        if (phase.Tags.Contains("Loot"))
            return LocalizationService.T("phase.loot", "Reward");
        return phase.PhaseId;
    }
    private IEnumerator AnimateHRDelta(int delta)
    {
        if (_hrDeltaText == null) yield break;
        _hrDeltaText.text = delta > 0 ? $"+{delta}" : $"{delta}";
        _hrDeltaText.color = delta >= 0 ? _hrPositiveColor : _hrNegativeColor;
        _hrDeltaText.gameObject.SetActive(true);
        _hrDeltaText.alpha = 1f;
        float elapsed = 0f;
        float duration = _hrDeltaDuration;
        Vector2 fixedPos = new Vector2(30, 20);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _hrDeltaText.rectTransform.anchoredPosition = fixedPos;
            _hrDeltaText.alpha = 1f - (elapsed / duration);
            yield return null;
        }
        _hrDeltaText.gameObject.SetActive(false);
    }
    void RefreshHand(IPlayerSide side)
    {
        Debug.Log($"[HandUI] RefreshHand - count: {side.Hand.Count}");
        foreach (var kv in _cardViews)
        {
            // ResumeFromSaveAsync can run after a scene reload that already destroyed
            // the previous duel's hand prefabs. Guard against the dangling handler so
            // the Unity-side null check fires before we touch gameObject.
            var handler = kv.Value;
            if (handler == null) continue;
            Destroy(handler.gameObject);
        }
        _cardViews.Clear();
        foreach (var card in side.Hand)
        {
            var go = Instantiate(CardViewPrefab, HandContainer);
            var handler = go.AddComponent<DragHandler>();
            handler.Setup(card, this);
            handler.enabled = CanStartCardDrag();
            _cardViews[card] = handler;
            EnsureCardTextReadable(go);
            var cardView = go.GetComponent<Shared.UI.CardView>();
            if (cardView == null)
            {
                cardView = go.AddComponent<Shared.UI.CardView>();
            }
            cardView.Bind(card);
        }
        _lastHandCount = side.Hand.Count;
    }
    public void RefreshHandImmediately()
    {
        var state = _duelManager?.CurrentDuelState;
        if (state != null)
            RefreshHand(state.PlayerSide);
        if (_duelManager?.CurrentDuelState != null)
            _displayedHR = _duelManager.CurrentDuelState.PlayerSide.HumanResources;
    }
    public bool CanStartCardDrag()
    {
        var duelManager = _duelManager != null ? _duelManager : DuelManagerProxy.Instance;
        var state = duelManager?.CurrentDuelState;
        return state != null &&
               state.CurrentPhase.Tags.Contains("BuildingPhase") &&
               duelManager.CanConfirmCurrentPhase &&
               IsCardDragAllowedByTutorial();
    }

    private void ApplyHandCardAffordances(IPlayerSide side, bool allowDrag)
    {
        // Row compatibility is visualized on board slots while dragging/tapping a card.
        // Keeping the same compatible/incompatible shader on idle hand cards makes it look
        // like the row-slot affordance was applied to the wrong surface, so hand cards stay neutral.
        foreach (var kv in _cardViews)
        {
            var handler = kv.Value;
            if (handler != null)
                handler.SetAffordanceState(CardAffordanceState.None);
        }
    }

    private bool IsCardDragAllowedByTutorial()
    {
        if (TutorialSystem == null)
        {
            TutorialSystem = FindObjectOfType<TutorialSystem>(true);
        }

        return TutorialSystem == null || !TutorialSystem.IsTutorialActive || TutorialSystem.AllowsCardDragging();
    }

    public void OnCardTapped(DragHandler handler)
    {
        if (handler == null || BoardView == null || !CanStartCardDrag()) return;
        var card = handler.GetCard();
        if (card?.Def == null) return;

        if (_dropPreviewCard == handler)
        {
            _dropPreviewCard = null;
            BoardView.HideAllHighlights();
            return;
        }

        _dropPreviewCard = handler;
        BoardView.ShowValidDropZones(card.Def.RowType);
    }

    public void OnCardDragStarted(DragHandler handler)
    {
        _currentlyDragging = handler;
        _dropPreviewCard = handler;
        BoardView.ShowValidDropZones(handler.GetCard().Def.RowType);
        if (TutorialSystem == null)
        {
            TutorialSystem = FindObjectOfType<TutorialSystem>(true);
        }
        if (TutorialSystem != null && TutorialSystem.IsTutorialActive)
        {
            TutorialSystem.OnCardDragStarted();
        }
        RuntimeManager.PlayOneShot("event:/Duel/Cards/SelectCard", _currentlyDragging.transform.position);
    }
    public void OnCardDragEnded(DragHandler handler, PointerEventData eventData)
    {
        _currentlyDragging = null;
        if (_dropPreviewCard == handler) _dropPreviewCard = null;
        var card = handler.GetCard();
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;
        if (!state.CurrentPhase.Tags.Contains("BuildingPhase"))
        {
            BoardView.HideAllHighlights();
            return;
        }
        var def = card.Def;
        var cardImage = handler.GetComponent<Image>();
        var results = new List<RaycastResult>();
        var raycastEventData = new PointerEventData(EventSystem.current)
        {
            position = handler.LastPointerScreenPosition,
            pressPosition = eventData != null ? eventData.pressPosition : handler.LastPointerScreenPosition,
            button = eventData != null ? eventData.button : PointerEventData.InputButton.Left
        };
        EventSystem.current.RaycastAll(raycastEventData, results);
        var raycastSlots = results
            .Select(r => r.gameObject.GetComponentInParent<BoardSlotUI>())
            .Where(s => s != null)
            .Distinct()
            .ToList();
        BoardSlotUI targetSlotUI = raycastSlots.FirstOrDefault(s => s.IsValidDropTarget);
        if (targetSlotUI == null)
        {
            BoardView.HideAllHighlights();
            ResetDragState(handler);
            return;
        }
        foreach (var cost in def.Costs)
        {
            var ctx = CreateCostContext(cost, side);
            if (!cost.CanPay(ctx))
            {
                Debug.Log($"[Drag] Cannot pay cost: {cost.GetCostText()}");
                ShowResourceWarning(cost, side);
                if (cardImage != null)
                {
                    cardImage.color = new Color(1, 0, 0, 0.5f);
                    StartCoroutine(ResetCardHighlight(cardImage));
                }
                if (cost is HumanResourceCost)
                {
                    StartCoroutine(HighlightHRText());
                }
                BoardView.HideAllHighlights();
                ResetDragState(handler);
                return;
            }
        }
        foreach (var cost in def.Costs)
        {
            var ctx = CreateCostContext(cost, side);
            var payment = cost.GetPaymentAction(ctx);
            if (payment != null)
                _duelManager.QueueAction(payment);
        }
        var board = ((SideState)side).Board;
        BoardSlot targetSlot = board.GetSlot(targetSlotUI.RowType, targetSlotUI.Index);
        if (targetSlot == null)
        {
            BoardView.HideAllHighlights();
            return;
        }
        _duelManager.QueueAction(new PlaceCardIntoSlotAction(board, def, targetSlot));
        BoardView.HideAllHighlights();
    }
    private ICostContext CreateCostContext(CardCost cost, IPlayerSide side)
    {
        if (cost is SacrificeCost sacrificeCost)
        {
            return new SacrificeCostContext
            {
                PlayerSide = side,
                Amount = sacrificeCost.Amount,
                Cost = sacrificeCost
            };
        }
        return new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
    }
    public CardDef GetFirstTutorialPlayableCardDef()
    {
        var side = _duelManager?.CurrentDuelState?.PlayerSide;
        if (side == null) return null;
        if (TutorialSystem == null)
        {
            TutorialSystem = FindObjectOfType<TutorialSystem>();
        }
        var preferredName = TutorialSystem != null && TutorialSystem.IsTutorialActive
            ? TutorialSystem.PreferredPlayableCardName
            : null;
        if (!string.IsNullOrEmpty(preferredName))
        {
            var preferred = FindPlayableCardDefByName(side, preferredName);
            if (preferred != null) return preferred;
        }
        foreach (var card in side.Hand)
        {
            if (IsPlayableCardForTutorial(card?.Def, side))
            {
                return card.Def;
            }
        }
        return null;
    }

    private CardDef FindPlayableCardDefByName(SideState side, string cardName)
    {
        foreach (var card in side.Hand)
        {
            if (card?.Def == null || card.Def.CardName != cardName) continue;
            if (IsPlayableCardForTutorial(card.Def, side)) return card.Def;
        }
        return null;
    }

    private bool IsPlayableCardForTutorial(CardDef def, SideState side)
    {
        return def != null &&
               CanPayAllCosts(def, side) &&
               BoardView?.FindFirstEmptyPlayerSlot(def.RowType) != null;
    }
    public RectTransform FindFirstPlayableCardViewForTutorial()
    {
        var wanted = GetFirstTutorialPlayableCardDef();
        if (wanted == null) return null;
        foreach (var kv in _cardViews)
        {
            if (kv.Key?.Def == wanted)
            {
                return kv.Value.transform as RectTransform;
            }
        }
        return null;
    }
    public RectTransform FindFirstPlayableBoardSlotForTutorial()
    {
        var wanted = GetFirstTutorialPlayableCardDef();
        if (wanted == null || BoardView == null) return null;
        return BoardView.FindFirstEmptyPlayerSlot(wanted.RowType)?.transform as RectTransform;
    }

    public RectTransform FindHumanResourcesTextForTutorial()
    {
        return PlayerHumanResText != null && PlayerHumanResText.gameObject.activeInHierarchy
            ? PlayerHumanResText.rectTransform
            : null;
    }
    public string GetTutorialPlayableCardHint()
    {
        var side = _duelManager?.CurrentDuelState?.PlayerSide;
        var wanted = GetFirstTutorialPlayableCardDef();
        if (wanted != null)
        {
            var costs = wanted.Costs != null && wanted.Costs.Count > 0
                ? string.Join(" ", wanted.Costs.Where(c => c != null).Select(CardRulesText.FormatCostText))
                : LocalizationService.T("tutorial.cost.free", "free");
            return LocalizationService.TFormat("tutorial.playable_card_hint", "Take {0} ({1}) and drag it into a free {2} row slot.", LocalizationService.CardName(wanted), costs, LocalizationService.RowTypeName(wanted.RowType));
        }
        return LocalizationService.TFormat("tutorial.no_playable_card", "There is no playable card for this step right now (HR={0}). The step will be skipped.", side?.HumanResources ?? 0);
    }
    private bool CanPayAllCosts(CardDef def, IPlayerSide side)
    {
        if (def == null || side == null) return false;
        foreach (var cost in def.Costs)
        {
            if (cost == null) continue;
            if (!cost.CanPay(CreateCostContext(cost, side))) return false;
        }
        return true;
    }
    private readonly System.Collections.Generic.Dictionary<string, int> _failedAttempts = new();

    public void ResetFailedAttempts() => _failedAttempts.Clear();

    private void ShowResourceWarning(CardCost cost, IPlayerSide side)
    {
        if (ResourceWarningUI == null || cost == null) return;
        // Debounce: only show the warning starting from the SECOND failed attempt for the
        // same cost. First failure is silent — players often re-try without realising.
        string key = cost.GetType().FullName ?? "?";
        _failedAttempts.TryGetValue(key, out int count);
        count++;
        _failedAttempts[key] = count;
        if (count < 2) return;
        const string RESOURCE = "#ffb14a";   // golden-orange — the resource you lack
        string warningMessage = "";
        if (cost is HumanResourceCost hrCost)
        {
            warningMessage = LocalizationService.TFormat("warning.not_enough_hr",
                "Не хватает <color={2}>Human Resources</color>\nТребуется: {0}, доступно: {1}",
                hrCost.Amount, side.HumanResources, RESOURCE);
        }
        else if (cost is SacrificeCost sacrificeCost)
        {
            var available = side.Board.GetCardsRow(sacrificeCost.RequiredRowType)
                .Count(slot => slot?.Occupant != null && slot.Occupant.IsAlive);
            warningMessage = LocalizationService.TFormat("warning.need_sacrifice",
                "Нужно <color={3}>жертвоприношение ({1})</color>\nТребуется: {0}, доступно: {2}",
                sacrificeCost.Amount, LocalizationService.RowTypeName(sacrificeCost.RequiredRowType), available, RESOURCE);
        }
        else
        {
            warningMessage = LocalizationService.TFormat("warning.not_enough_resources",
                "Недостаточно ресурсов: <color={1}>{0}</color>",
                CardRulesText.FormatCostText(cost), RESOURCE);
        }
        ResourceWarningUI.ShowWarningAsync(warningMessage).Forget();
    }
    private IEnumerator HighlightHRText()
    {
        if (PlayerHumanResText == null) yield break;
        Color originalColor = PlayerHumanResText.color;
        PlayerHumanResText.color = new Color(1f, 0.5f, 0f, 1f);
        yield return new WaitForSeconds(1.5f);
        PlayerHumanResText.color = originalColor;
    }
    private IEnumerator ResetCardHighlight(Image cardImage)
    {
        yield return new WaitForSeconds(1.5f);
        if (cardImage != null)
            cardImage.color = Color.white;
    }
    private void ResetDragState(DragHandler handler)
    {
        var canvasGroup = handler.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }
}
