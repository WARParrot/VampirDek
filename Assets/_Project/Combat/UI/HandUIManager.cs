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

public class HandUIManager : MonoBehaviour
{
    [Header("Prefabs & Layout")]
    public GameObject CardViewPrefab;
    public Transform HandContainer;

    [Header("References")]
    public BoardView BoardView;

    [Header("Status Texts")]
    public TextMeshProUGUI PlayerTownHPText, OpponentTownHPText, PlayerManaText, PlayerHumanResText, PhaseText;

    private DuelManager _duelManager;
    private Dictionary<ICard, DragHandler> _cardViews = new();
    private DragHandler _currentlyDragging;
    private int _lastHandCount = -1;
    private bool _lastDragAllowed = false;

    void Update()
    {
        _duelManager = DuelManagerProxy.Instance;
        if (_duelManager?.CurrentDuelState == null) return;

        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

        PlayerTownHPText.text = $"Town HP: {side.Town?.Health}";
        OpponentTownHPText.text = $"Opp Town HP: {state.OpponentSide.Town?.Health}";
        //PlayerManaText.text = $"Mana: {side.Mana}";
        PlayerHumanResText.text = $"HR: {side.HumanResources}";
        PhaseText.text = $"Phase: {state.CurrentPhase.PhaseId}";

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
    }

    public void OnCardDragStarted(DragHandler handler)
    {
        _currentlyDragging = handler;
        BoardView.ShowValidDropZones(handler.GetCard().Def.RowType);
    }

    public void OnCardDragEnded(DragHandler handler, PointerEventData eventData)
    {
        _currentlyDragging = null;

        var card = handler.GetCard();
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

        Debug.Log($"[Drag] Card: {card.Def.CardName}, Phase: {state.CurrentPhase.PhaseId}, Tags: {string.Join(",", state.CurrentPhase.Tags)}");

        if (!state.CurrentPhase.Tags.Contains("BuildingPhase"))
        {
            Debug.Log("[Drag] Not BuildingPhase - ignoring drop");
            BoardView.HideAllHighlights();
            return;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        BoardSlotUI targetSlotUI = results
            .Select(r => r.gameObject.GetComponent<BoardSlotUI>())
            .FirstOrDefault(s => s != null);

        if (targetSlotUI == null)
        {
            Debug.Log("[Drag] No BoardSlotUI hit");
            BoardView.HideAllHighlights();
            return;
        }

        Debug.Log($"[Drag] Hit slot - RowType: {targetSlotUI.RowType}, Index: {targetSlotUI.Index}, IsValidDrop: {targetSlotUI.IsValidDropTarget}");

        if (!targetSlotUI.IsValidDropTarget)
        {
            Debug.Log("[Drag] Slot is not a valid drop target");
            BoardView.HideAllHighlights();
            return;
        }

        var def = card.Def;
        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
            if (!cost.CanPay(ctx))
            {
                Debug.Log($"[Drag] Cannot pay cost: {cost.GetCostText()}");
                BoardView.HideAllHighlights();
                return;
            }
        }

        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
            _duelManager.QueueAction(cost.GetPaymentAction(ctx));
        }

        var board = ((SideState)side).Board;
        BoardSlot targetSlot = null;
        switch (targetSlotUI.RowType)
        {
            case Definitions.RowType.Vanguard:
                if (targetSlotUI.Index < board.VanguardRow.Length)
                    targetSlot = board.VanguardRow[targetSlotUI.Index];
                break;
            case Definitions.RowType.Building:
                if (targetSlotUI.Index < board.BuildingRow.Length)
                    targetSlot = board.BuildingRow[targetSlotUI.Index];
                break;
            case Definitions.RowType.Human:
                if (targetSlotUI.Index < board.HumanRow.Length)
                    targetSlot = board.HumanRow[targetSlotUI.Index];
                break;
            case Definitions.RowType.Town:
                targetSlot = board.TownSlot;
                break;
        }

        if (targetSlot == null)
        {
            Debug.LogError("[Drag] Mismatch - UI slot found but no matching BoardSlot");
            BoardView.HideAllHighlights();
            return;
        }

        ((SideState)side).Hand.Remove((Card)card);
        _duelManager.QueueAction(new PlaceCardIntoSlotAction(board, def, targetSlot));
        Debug.Log($"[Drag] Card removed from hand, placement action queued.");

        BoardView.HideAllHighlights();
    }
}
