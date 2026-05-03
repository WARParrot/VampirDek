using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Mana Pay")]
    public class ManaPayActionDefinition : CostActionDefinition
    {
        [SerializeField] private int _amount;

        public override IGameAction CreateAction(ICostContext context)
        {
            return new PayManaAction(context.PlayerSide, _amount);
        }
    }
}
