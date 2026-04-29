using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Win Condition/Take Town")]
    public class TakeTownWinCondition : WinCondition
    {
        public override bool Check(IDuelState state)
        {
            if (!state.OpponentTown.IsAlive) return true;
            if (!state.PlayerTown.IsAlive) return true;
            return false;
        }
    }
}
