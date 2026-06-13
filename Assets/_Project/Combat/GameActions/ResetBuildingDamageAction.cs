using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class ResetBuildingDamageAction : IGameAction
    {
        private Board _board;
        public string Description => "Reset building damage";

        public ResetBuildingDamageAction(Board board) => _board = board;

        public UniTask ExecuteAsync()
        {
            foreach (var slot in _board.BuildingRow)
                if (slot.Occupant != null)
                    slot.Occupant.DamageReceivedThisTurn = 0;
            return UniTask.CompletedTask;
        }
    }
}
