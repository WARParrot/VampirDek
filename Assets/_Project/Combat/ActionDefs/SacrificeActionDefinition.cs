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
            if (context?.PlayerSide?.Board == null) return null;

            var rowType = _requiredRowType;
            var amount = context.Amount;
            if (context is SacrificeCostContext sacrificeContext && sacrificeContext.Cost != null)
            {
                rowType = sacrificeContext.RequiredRowType;
                amount = sacrificeContext.RequiredAmount;
            }

            if (amount <= 0) return null;
            return new SacrificeAction(context.PlayerSide.Board, rowType, amount);
        }
    }
}
