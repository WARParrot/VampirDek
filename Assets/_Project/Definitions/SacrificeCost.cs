using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Cost/Sacrifice")]
    public class SacrificeCost : CardCost
    {
        [SerializeField] private RowType _requiredRowType;
        public RowType RequiredRowType => _requiredRowType;

        [SerializeField] private int _amount;
        public int Amount => _amount;
        public override int GetAmount() => _amount;

        public override bool CanPay(ICostContext context)
        {
            return context.PlayerSide.Board.GetCardsRow(_requiredRowType).Length >= _amount;
        }

        public override string GetCostText() => (_amount == 1) ? $"{_requiredRowType}" : $"{_amount} {_requiredRowType}";
    }
}