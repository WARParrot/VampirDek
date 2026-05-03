using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Human Resource Pay")]
    public class HumanResourcePayActionDefinition : CostActionDefinition
    {
        public override IGameAction CreateAction(ICostContext context)
        {
            return new PayHumanResourceAction(context.PlayerSide, _amount);
        }

        [SerializeField] private int _amount;   // must match the cost's amount
    }
}
