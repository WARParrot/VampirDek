using Cysharp.Threading.Tasks;
using Definitions;
using Core;

namespace Combat
{
    public class BuildingDestructionCheckAction : IGameAction
    {
        private Board _board;
        public string Description => "Check building destruction";

        public BuildingDestructionCheckAction(Board board) => _board = board;

        public UniTask ExecuteAsync()
        {
            foreach (var slot in _board.BuildingRow)
            {
                if (slot.Occupant is BoardCard building && building.TypeOfRow == Definitions.RowType.Building)
                {
                    if (building.DamageReceivedThisTurn >= building.MaxHealth)
                    {
                        //building.Health = 0;
                        GlobalServices.EventBus.Publish(new EntityDiedEvent(building));
                        _board.RemoveCard(building);
                    }
                    building.DamageReceivedThisTurn = 0;
                }
            }
            return UniTask.CompletedTask;
        }
    }
}
