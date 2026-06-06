using System.Linq;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Cost/Blood (Eat Humans)")]
    public class BloodCost : CardCost
    {
        [SerializeField] private int _amount;

        public int Amount => _amount;
        public override int GetAmount() => _amount;

        public override bool CanPay(ICostContext context)
        {
            int alive = 0;
            foreach (var slot in context.PlayerSide.Board.HumanRow)
            {
                if (slot.Occupant != null && slot.Occupant.IsAlive)
                    alive++;
            }
            return alive >= _amount;
        }

        public override string GetCostText() => $"{_amount} blood";
    }
}
