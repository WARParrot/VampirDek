using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Cost/Mana")]
    public class ManaCost : CardCost
    {
        [SerializeField] private int _amount;
        public int Amount => _amount;

        public override bool CanPay(ICostContext context)
        {
            return context.PlayerSide.Mana >= _amount;
        }

        public override string GetCostText() => _amount.ToString();
    }
}