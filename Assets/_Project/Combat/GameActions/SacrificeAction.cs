using Cysharp.Threading.Tasks;
using Definitions;
using Core;

namespace Combat
{
    public class SacrificeAction : IGameAction
    {
        private readonly IBoard _board;
        private readonly Definitions.RowType _rowType;
        private readonly int _amount;

        public string Description => _amount == 1
            ? $"Sacrifice one {_rowType}"
            : $"Sacrifice {_amount} {_rowType} cards";

        public SacrificeAction(IBoardCard target, IBoard board)
        {
            _board = board;
            _rowType = target?.TypeOfRow ?? Definitions.RowType.Human;
            _amount = 1;
        }

        public SacrificeAction(IBoard board, Definitions.RowType rowType, int amount)
        {
            _board = board;
            _rowType = rowType;
            _amount = amount;
        }

        public async UniTask ExecuteAsync()
        {
            if (_board == null || _amount <= 0)
            {
                await UniTask.CompletedTask;
                return;
            }

            for (var i = 0; i < _amount; i++)
            {
                var target = _board.GetFirstAliveCardInRow(_rowType);
                if (target == null) break;

                target.TakeDamage(target.Health, null);
                _board.RemoveCard(target);
            }

            await UniTask.CompletedTask;
        }
    }
}
