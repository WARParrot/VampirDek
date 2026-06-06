using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Blood Pay")]
    public class BloodPayActionDefinition : CostActionDefinition
    {
        public override IGameAction CreateAction(ICostContext context)
        {
            return new PayBloodAction(context.PlayerSide, context.Amount);
        }
    }
}
