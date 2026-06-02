using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Win Condition/Take Town")]
    public class TakeTownWinCondition : WinCondition
    {
        public override bool Check(IDuelState state)
        {
            return state?.OpponentTown?.IsAlive == false
                || state?.PlayerTown?.IsAlive == false;
        }
    }
}
