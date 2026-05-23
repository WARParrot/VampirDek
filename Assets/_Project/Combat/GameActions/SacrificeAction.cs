using Cysharp.Threading.Tasks;
using Definitions;
using Core;

namespace Combat
{
    public class SacrificeAction : IGameAction
    {
        private readonly IBoardCard _target;
        private readonly IBoard _board;

        public string Description => $"Sacrifice {_target.SourceCard.CardName}";

        public SacrificeAction(IBoardCard target, IBoard board)
        {
            _target = target;
            _board = board;
        }

        public async UniTask ExecuteAsync()
        {
            _target.TakeDamage(_target.Health, null);
            GlobalServices.EventBus.Publish(new EntityDiedEvent(_target));
            _board.RemoveCard(_target);

            await UniTask.CompletedTask;
        }
    }
}