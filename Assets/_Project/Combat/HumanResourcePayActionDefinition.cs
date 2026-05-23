using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Human Resource Pay")]
    public class HumanResourcePayActionDefinition : CostActionDefinition
    {
        public override IGameAction CreateAction(ICostContext context)
        {
            return new PayHumanResourceAction(context.PlayerSide, context.Amount);
        }
    }
}
