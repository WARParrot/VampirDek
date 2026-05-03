using UnityEngine;

namespace Definitions
{
    public abstract class CardCost : ScriptableObject
    {
        [SerializeField] protected CostActionDefinition _payAction;
        public abstract bool CanPay(ICostContext context);
        public IGameAction GetPaymentAction(ICostContext context)
        {
            return _payAction?.CreateAction(context);
        }
        public IGameAction GetPaymentAction(IPlayerSide side, int amount)
        {
            var context = new CostContext { PlayerSide = side, Amount = amount };
            return GetPaymentAction(context);
        }   
        public abstract string GetCostText();
    }
}
