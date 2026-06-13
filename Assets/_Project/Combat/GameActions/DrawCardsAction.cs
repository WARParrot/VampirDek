using Core;
using Definitions;
using Cysharp.Threading.Tasks;

namespace Combat.GameActions
{
    public class DrawCardsAction : IGameAction
    {
        private readonly SideState _side;
        private readonly int _count;

        public DrawCardsAction(IPlayerSide side, int count)
        {
            _side = (SideState)side;
            _count = count;
        }

        public string Description => $"Взять {_count} карт";

        public UniTask ExecuteAsync()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_side.Hand.Count >= SideState.MaxHandSize)
                    break;

                var card = _side.Deck.Draw();
                if (card != null)
                    _side.AddCardToHand(card);
                else
                    break;
            }
            return UniTask.CompletedTask;
        }
    }
}