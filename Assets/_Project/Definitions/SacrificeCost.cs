using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Cost/Sacrifice")]
    public class SacrificeCost : CardCost
    {
        [SerializeField] private RowType _requiredRowType;
        public RowType RequiredRowType => _requiredRowType;

        public override bool CanPay(ICostContext context)
        {
            return context.PlayerSide.Board.GetFirstAliveCardInRow(_requiredRowType) != null;
        }

        public override string GetCostText() => $"Sacrifice a {_requiredRowType}";

        public override int GetAmount() => 0;
    }
}