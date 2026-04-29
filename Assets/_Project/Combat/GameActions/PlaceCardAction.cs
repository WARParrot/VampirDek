using UnityEngine;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class PlaceCardAction(Board board, CardDef cardDef) : IGameAction
    {
        private readonly Board _board = board;
        private readonly CardDef _cardDef = cardDef;
        public string Description => $"Place {_cardDef.CardName}";

        public async UniTask ExecuteAsync()
        {
            if (!_board.TryPlaceCard(_cardDef, out _))
                GlobalServices.EventBus.Publish(new PlaceFailedEvent(_cardDef.CardName, "No valid slot"));
        }
    }
}
