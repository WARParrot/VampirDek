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
        public abstract string GetCostText();
    }
}