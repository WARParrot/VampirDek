using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class PlacedCardAction : IGameAction
    {
        private readonly Board _board;
        private readonly CardDef _cardDef;
        public string Description => $"Summon {_cardDef.CardName}";

        public PlacedCardAction(Board board, CardDef cardDef)
        {
            _board = board;
            _cardDef = cardDef;
        }

        public async UniTask ExecuteAsync()
        {
            if (!_board.TryPlaceCard(_cardDef, out _))
                GlobalServices.EventBus.Publish(new PlaceFailedEvent(_cardDef.CardName, "No valid slot"));
        }
    }
}
