using Core;
using Definitions;
using Cysharp.Threading.Tasks;

namespace Combat.GameActions
{
    // Mirrors the player's draft: one Human is materialized fresh from CardDatabase (same as the
    // player's guaranteed-Human draft slot — does not consume the deck), the rest are normal draws.
    // Keeps the AI's Human supply effectively unlimited like the player's.
    public class DrawWithGuaranteedHumanAction : IGameAction
    {
        private const string HumanCardName = "Human";
        private static int _instanceIdCounter = -300000;

        private readonly SideState _side;
        private readonly int _count;

        public DrawWithGuaranteedHumanAction(IPlayerSide side, int count)
        {
            _side = (SideState)side;
            _count = count;
        }

        public string Description => $"Взять {_count} карт (гарантированно 1 Человек)";

        public UniTask ExecuteAsync()
        {
            int drawn = 0;

            if (_side.Hand.Count < SideState.MaxHandSize)
            {
                var humanDef = Combat.CardDatabase.GetCard(HumanCardName);
                if (humanDef != null)
                {
                    _side.AddCardToHand(new Card(humanDef, _instanceIdCounter--));
                    drawn++;
                }
            }

            for (; drawn < _count; drawn++)
            {
                if (_side.Hand.Count >= SideState.MaxHandSize) break;
                var card = _side.Deck.Draw();
                if (card == null) break;
                _side.AddCardToHand(card);
            }
            return UniTask.CompletedTask;
        }
    }
}
