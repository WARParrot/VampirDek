using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Combat.UI;
using Core;
using Combat;
using Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;

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

    void Awake()
    {
        if (PlayerManaText != null)
            PlayerManaText.gameObject.SetActive(false);
    }

    void Update()
    {
        _duelManager = DuelManagerProxy.Instance;
        if (_duelManager?.CurrentDuelState == null) return;

        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

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

        bool allowDrag = state.CurrentPhase.Tags.Contains("BuildingPhase");
        if (allowDrag != _lastDragAllowed)
        {
            foreach (var kv in _cardViews)
                kv.Value.enabled = allowDrag;
            _lastDragAllowed = allowDrag;
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
            _cardViews[card] = handler;

            go.transform.Find("CardName").GetComponent<TextMeshProUGUI>().text = card.Def.CardName;
            go.transform.Find("CardCost").GetComponent<TextMeshProUGUI>().text = string.Join(" ", card.Def.Costs.Select(c => c.GetCostText()));
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

    public void OnCardDragStarted(DragHandler handler)
    {
        _currentlyDragging = handler;
        BoardView.ShowValidDropZones(handler.GetCard().Def.RowType);

        if (TutorialSystem != null && TutorialSystem.IsTutorialActive)
        {
            TutorialSystem.OnCardDragStarted();
        }
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
        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
            if (!cost.CanPay(ctx))
            {
                Debug.Log($"[Drag] Cannot pay cost: {cost.GetCostText()}");
                ShowResourceWarning(cost, side);
                if (cardImage != null)
                {
                    cardImage.color = new Color(1, 0, 0, 0.5f);
                    StartCoroutine(ResetCardHighlight(cardImage));
                }
                StartCoroutine(HighlightHRText());
                BoardView.HideAllHighlights();
                ResetDragState(handler);
                return;
            }
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        BoardSlotUI targetSlotUI = results
            .Select(r => r.gameObject.GetComponent<BoardSlotUI>())
            .FirstOrDefault(s => s != null);

        if (targetSlotUI == null || !targetSlotUI.IsValidDropTarget)
        {
            BoardView.HideAllHighlights();
            return;
        }

        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
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

    private void ShowResourceWarning(CardCost cost, IPlayerSide side)
    {
        if (ResourceWarningUI == null) return;

        string warningMessage = "";

        if (cost is HumanResourceCost hrCost)
        {
            warningMessage = $"Нельзя использовать - не хватает людей\nТребуется: {hrCost.Amount}, Доступно: {side.HumanResources}";
        }
        else if (cost is BloodCost bloodCost)
        {
            int aliveHumans = 0;
            foreach (var slot in side.Board.HumanRow)
                if (slot.Occupant != null && slot.Occupant.IsAlive) aliveHumans++;
            warningMessage = $"Нельзя использовать - не хватает крови\nТребуется людей съесть: {bloodCost.Amount}, Доступно: {aliveHumans}";
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
