using System.Linq;
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
            if (context?.PlayerSide?.Board == null) return false;

            return context.PlayerSide.Board
                .GetCardsRow(_requiredRowType)
                .Count(slot => slot?.Occupant != null && slot.Occupant.IsAlive) >= _amount;
        }


        public override string GetCostText()
        {
            var rowName = _requiredRowType == RowType.Human ? "Human card" : _requiredRowType.ToString();
            return _amount == 1 ? rowName : $"{_amount} {rowName}s";
        }
    }
}
