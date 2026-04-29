using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class ClashAction : IGameAction
    {
        private BoardCard _attacker1, _attacker2;
        public string Description => $"Clash between {_attacker1.Id} and {_attacker2.Id}";

        public ClashAction(BoardCard a, BoardCard b) { _attacker1 = a; _attacker2 = b; }

        public async UniTask ExecuteAsync()
        {
            // Default clash: higher attack wins
            BoardCard winner, loser;
            if (_attacker1.Attack >= _attacker2.Attack) { winner = _attacker1; loser = _attacker2; }
            else { winner = _attacker2; loser = _attacker1; }

            // Loser's attack is cancelled (remove its planned target)
            loser.PlannedTarget = null;

            // Winner deals damage to loser (one-sided attack during clash)
            var damageAction = new DamageAction(loser, winner.Attack, winner);
            await damageAction.ExecuteAsync();

            GlobalServices.EventBus.Publish(new ClashResolvedEvent(winner, loser));
        }
    }
}
