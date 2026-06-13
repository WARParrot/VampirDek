using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class PlacedCardAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly CardDef _cardDef;

        public string Description => $"Place {_cardDef.CardName}";

        public PlacedCardAction(IPlayerSide side, CardDef cardDef)
        {
            _side = side;
            _cardDef = cardDef;
        }

        public UniTask ExecuteAsync()
        {
            if (!_side.Board.TryPlaceCard(_cardDef, out _))
                GlobalServices.EventBus.Publish(new PlaceFailedEvent(_cardDef.CardName, "No valid slot"));
            return UniTask.CompletedTask;
        }
    }
}