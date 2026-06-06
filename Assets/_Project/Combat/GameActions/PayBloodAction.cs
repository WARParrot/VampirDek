using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class PayBloodAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly int _amount;
        public string Description => $"Eat {_amount} human(s)";

        public PayBloodAction(IPlayerSide side, int amount)
        {
            _side = side;
            _amount = amount;
        }

        public async UniTask ExecuteAsync()
        {
            int needed = _amount;
            var board = _side.Board;

            foreach (var slot in board.HumanRow)
            {
                if (needed <= 0) break;
                var human = slot.Occupant;
                if (human == null || !human.IsAlive) continue;

                human.TakeDamage(human.Health, null);
                GlobalServices.EventBus.Publish(new EntityDiedEvent(human));
                board.RemoveCard(human);
                needed--;
            }

            await UniTask.CompletedTask;
        }
    }
}
