using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Sacrifice")]
    public class SacrificeActionDefinition : CostActionDefinition
    {
        [SerializeField] private Definitions.RowType _requiredRowType;

        public override IGameAction CreateAction(ICostContext context)
        {
            var board = context.PlayerSide.Board;
            var target = board.GetFirstAliveCardInRow(_requiredRowType);
            if (target == null) return null;

            return new SacrificeAction(target, board);
        }
    }
}