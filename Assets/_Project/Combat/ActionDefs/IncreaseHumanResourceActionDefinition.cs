using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Increase Human Resource")]
    public class IncreaseHumanResourceActionDefinition : ActionDefinition
    {
        [SerializeField] private int _amount = 1;

        public override IGameAction CreateAction()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager?.CurrentDuelState == null) return null;
            var playerSide = duelManager.CurrentDuelState.PlayerSide;
            return new IncreaseHumanResourceAction(playerSide, _amount);
        }
    }
}