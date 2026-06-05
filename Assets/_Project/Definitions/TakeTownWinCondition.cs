using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Win Condition/Take Town")]
    public class TakeTownWinCondition : WinCondition
    {
        public override bool Check(IDuelState state)
        {
            // This condition means the duel has reached a terminal town-destroyed state.
            // DuelManager is responsible for turning that terminal state into PlayerWon/PlayerLost/Draw.
            return state?.OpponentTown?.IsAlive == false
                || state?.PlayerTown?.IsAlive == false;
        }
    }
}
