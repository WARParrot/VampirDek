using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.UI;
using Core;
using Combat;
using Definitions;
using Shared.UI;
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
    private int _lastHandCount = -1;
    private bool _lastDragAllowed = false;
    [Header("HR Animation")]
    [SerializeField] private TextMeshProUGUI _hrDeltaText;
    [SerializeField] private float _hrDeltaDuration = 0.5f;
    [SerializeField] private Color _hrPositiveColor = Color.green;
    [SerializeField] private Color _hrNegativeColor = Color.red;
    private int _displayedHR;
    void Update()
    {
        _duelManager = DuelManagerProxy.Instance;
        if (_duelManager?.CurrentDuelState == null) return;
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;
        EnsureReadableStatusText();
        PlayerTownHPText.text = $"Town HP: {side.Town?.Health}";
        OpponentTownHPText.text = $"Opp Town HP: {state.OpponentSide.Town?.Health}";
        PhaseText.text = GetPhaseDisplayText(state.CurrentPhase);
        if (_displayedHR != side.HumanResources)
        {
            int delta = side.HumanResources - _displayedHR;
            StartCoroutine(AnimateHRDelta(delta));
            _displayedHR = side.HumanResources;
        }
        PlayerHumanResText.text = $"HR: {side.HumanResources}";
        if (side.Hand.Count != _lastHandCount)
            RefreshHand(side);
        bool allowDrag = state.CurrentPhase.Tags.Contains("BuildingPhase") && IsCardDragAllowedByTutorial();
        foreach (var kv in _cardViews)
        {
            if (kv.Value != null) kv.Value.enabled = allowDrag;
        }
        _lastDragAllowed = allowDrag;
    }
    private void EnsureReadableStatusText()
    {
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
        if (phase == null) return "";
        if (phase.Tags.Contains("BuildingPhase"))
            return "Фаза строительства\nПеретащите карту на поле или подтвердите фазу";
        if (phase.Tags.Contains("PlanningPhase"))
            return "Фаза планирования\nВыберите цели для атаки";
        if (phase.Tags.Contains("ClashingPhase"))
            return "Фаза столкновений";
        if (phase.Tags.Contains("OneSidedAttackPhase"))
            return "Фаза атак";
        if (phase.Tags.Contains("StartOfTurn"))
            return "Начало хода";
        if (phase.Tags.Contains("EndOfTurn"))
            return "Конец хода";
        if (phase.Tags.Contains("DuelStart"))
            return "Начало дуэли";
        if (phase.Tags.Contains("Loot"))
            return "Награда";
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
            Destroy(kv.Value.gameObject);
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
        var state = _duelManager?.CurrentDuelState;
        return state != null && state.CurrentPhase.Tags.Contains("BuildingPhase") && IsCardDragAllowedByTutorial();
    }

    private bool IsCardDragAllowedByTutorial()
    {
        if (TutorialSystem == null)
        {
            TutorialSystem = FindObjectOfType<TutorialSystem>(true);
        }

        return TutorialSystem == null || !TutorialSystem.IsTutorialActive || TutorialSystem.AllowsCardDragging();
    }

    public void OnCardDragStarted(DragHandler handler)
    {
        _currentlyDragging = handler;
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
        EventSystem.current.RaycastAll(eventData, results);
        BoardSlotUI targetSlotUI = results
            .Select(r => r.gameObject.GetComponentInParent<BoardSlotUI>())
            .FirstOrDefault(s => s != null);
        if (targetSlotUI == null || !targetSlotUI.IsValidDropTarget)
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
                ? string.Join(" ", wanted.Costs.Select(c => c.GetCostText()))
                : "без стоимости";
            return $"Возьмите {wanted.CardName} ({costs}) и перетащите в свободный слот ряда {wanted.RowType}.";
        }
        return $"Сейчас нет подходящей карты для этого шага (HR={side?.HumanResources ?? 0}). Шаг будет пропущен.";
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
    private void ShowResourceWarning(CardCost cost, IPlayerSide side)
    {
        if (ResourceWarningUI == null) return;
        string warningMessage = "";
        if (cost is ManaCost manaCost)
        {
            warningMessage = $"Нельзя использовать - не хватает маны\nТребуется: {manaCost.Amount}, Доступно: {side.Mana}";
        }
        else if (cost is HumanResourceCost hrCost)
        {
            warningMessage = $"Нельзя использовать - не хватает HR\nТребуется: {hrCost.Amount}, Доступно: {side.HumanResources}";
        }
        else if (cost is SacrificeCost sacrificeCost)
        {
            var available = side.Board.GetCardsRow(sacrificeCost.RequiredRowType)
                .Count(slot => slot?.Occupant != null && slot.Occupant.IsAlive);
            warningMessage = $"Нельзя использовать - нужен живой {sacrificeCost.RequiredRowType} на поле\nТребуется: {sacrificeCost.Amount}, Доступно: {available}";
        }
        else
        {
            warningMessage = $"Нельзя использовать - недостаточно ресурсов\n{cost.GetCostText()}";
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
