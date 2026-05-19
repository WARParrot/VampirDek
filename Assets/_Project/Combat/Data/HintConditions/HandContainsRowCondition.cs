using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Hint Condition/Hand Contains Row")]
    public class HandContainsRowCondition : HintCondition
    {
        public Definitions.RowType RequiredRow;

        public override bool IsMet(object context)
        {
            var state = context as DuelState;
            if (state == null) return false;
            return state.PlayerSide.Hand.Exists(c => c.Def.RowType == RequiredRow);
        }
    }
}