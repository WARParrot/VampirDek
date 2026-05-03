using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Mana Pay")]
    public class ManaPayActionDefinition : CostActionDefinition
    {
        public override IGameAction CreateAction(ICostContext context)
        {
            return new PayManaAction(context.PlayerSide, context.Amount);
        }
    }
}
