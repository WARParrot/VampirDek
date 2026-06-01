using Cysharp.Threading.Tasks;
using Definitions;
using Combat;
using Core;
using UnityEngine;

public class PlaceCardIntoSlotAction : IGameAction
{
    private Board _board;
    private CardDef _cardDef;
    private BoardSlot _slot;

    public PlaceCardIntoSlotAction(Board board, CardDef cardDef, BoardSlot slot)
    {
        _board = board;
        _cardDef = cardDef;
        _slot = slot;
    }

    public string Description => $"Place {_cardDef.CardName} into slot {_slot.AllowedRow}[{_slot.Index}]";

    public async UniTask ExecuteAsync()
    {
        Debug.Log($"[Action] Trying to place {_cardDef.CardName} into slot {_slot.AllowedRow}[{_slot.Index}]");
        bool success = _board.TryPlaceCardIntoSlot(_cardDef, _slot);
        if (success)
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            var side = _board == duelManager.CurrentDuelState.PlayerSide.Board 
                ? duelManager.CurrentDuelState.PlayerSide 
                : duelManager.CurrentDuelState.OpponentSide;
            var card = side.Hand.Find(c => c.Def == _cardDef);
            if (card != null) side.Hand.Remove(card);
        }
        else
            GlobalServices.EventBus.Publish(new PlaceFailedEvent(_cardDef.CardName, "Placement failed"));
    }
}