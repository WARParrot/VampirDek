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
    private Dictionary<ICard, Vector2> _originalPositions = new();
    private DragHandler _currentlyDragging;
    private int _lastHandCount = -1;

    public void SetDuelManager(DuelManager duelManager)
    {
        _duelManager = duelManager;
        if (_duelManager?.CurrentDuelState != null)
            RefreshHand(_duelManager.CurrentDuelState.PlayerSide);
    }

    IEnumerator RefreshOnceReady()
    {
        yield return null;
        Debug.Log("[HandUI] RefreshOnceReady coroutine running");
        if (_duelManager?.CurrentDuelState != null)
        {
            Debug.Log("[HandUI] Condition true, calling RefreshHand");
            RefreshHand(_duelManager.CurrentDuelState.PlayerSide);
        }
        else
        {
            Debug.LogError("[HandUI] Condition failed - DuelState is null or _duelManager is null");
        }
    }

    void Update()
    {
        if (_duelManager?.CurrentDuelState == null) return;
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

        PlayerTownHPText.text = $"Town HP: {side.Town?.Health}";
        OpponentTownHPText.text = $"Opp Town HP: {state.OpponentSide.Town?.Health}";
        //PlayerManaText.text = $"Mana: {side.Mana}";
        PlayerHumanResText.text = $"HR: {side.HumanResources}";
        PhaseText.text = $"Phase: {state.CurrentPhase.PhaseId}";

        if (side.Hand.Count != _lastHandCount) RefreshHand(side);
    }

    void RefreshHand(IPlayerSide side)
    {
        Debug.Log($"[HandUI] RefreshHand called, hand count: {side.Hand.Count}");
        
        foreach (var kv in _cardViews)
            Destroy(kv.Value.gameObject);
        _cardViews.Clear();
        _originalPositions.Clear();

        foreach (var card in side.Hand)
        {
            var go = Instantiate(CardViewPrefab, HandContainer);
            var handler = go.AddComponent<DragHandler>();
            handler.Setup(card, this);
            _cardViews[card] = handler;
            var rt = go.GetComponent<RectTransform>();
            _originalPositions[card] = rt.anchoredPosition;

            go.transform.Find("CardName").GetComponent<TextMeshProUGUI>().text = card.Def.CardName;
            go.transform.Find("CardCost").GetComponent<TextMeshProUGUI>().text = string.Join(" ", card.Def.Costs.Select(c => c.GetCostText()));
        }

        _lastHandCount = side.Hand.Count;
    }

    public void OnCardDragStarted(DragHandler handler)
    {
        _currentlyDragging = handler;
        BoardView.ShowValidDropZones(handler.GetCard().Def.RowType);
    }

    public void OnCardDragEnded(DragHandler handler, PointerEventData eventData)
    {
        _currentlyDragging = null;
        BoardView.HideAllHighlights();

        var card = handler.GetCard();
        var state = _duelManager.CurrentDuelState;
        var side = state.PlayerSide;

        if (state.CurrentPhase.PhaseId != "BuildingPhase")
        {
            handler.ReturnToHand(_originalPositions[card]);
            return;
        }

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        BoardSlotUI targetSlotUI = results
            .Select(r => r.gameObject.GetComponent<BoardSlotUI>())
            .FirstOrDefault(s => s != null);

        if (targetSlotUI == null || !targetSlotUI.IsValidDropTarget)
        {
            handler.ReturnToHand(_originalPositions[card]);
            return;
        }

        var def = card.Def;
        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
            if (!cost.CanPay(ctx))
            {
                handler.ReturnToHand(_originalPositions[card]);
                return;
            }
        }

        foreach (var cost in def.Costs)
        {
            var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
            _duelManager.QueueAction(cost.GetPaymentAction(ctx));
        }

        ((SideState)side).Hand.Remove((Card)card);
        _duelManager.QueueAction(new PlacedCardAction(side, def));
    }
}
