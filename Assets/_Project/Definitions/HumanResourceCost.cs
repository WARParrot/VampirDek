using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Cost/Human Resource")]
    public class HumanResourceCost : CardCost
    {
        [SerializeField] private int _amount;

        public int Amount => _amount;
        public override int GetAmount() => _amount;

        public override bool CanPay(ICostContext context)
        {
            return context.PlayerSide.HumanResources >= _amount;
        }

        public override string GetCostText() => $"{_amount} HR";
    }
}
