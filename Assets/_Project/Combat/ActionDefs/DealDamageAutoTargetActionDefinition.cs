using System.Linq;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Deal Damage (Auto Target)")]
    public class DealDamageAutoTargetActionDefinition : EnchantmentActionDefinition
    {
        [SerializeField] private int _amount = 3;
        [SerializeField] private bool _preferHighestHealth = true;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new DealDamageAutoTargetAction(owner, _amount, _preferHighestHealth);
    }

    public class DealDamageAutoTargetAction : IGameAction
    {
        private readonly IGameEntity _owner;
        private readonly int _amount;
        private readonly bool _preferHighest;

        public string Description => $"Auto-deal {_amount} damage from {_owner?.Id}";

        public DealDamageAutoTargetAction(IGameEntity owner, int amount, bool preferHighest)
        {
            _owner = owner;
            _amount = amount;
            _preferHighest = preferHighest;
        }

        public async UniTask ExecuteAsync()
        {
            var side = SideLookup.FindSideOf(_owner);
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            var enemy = SideLookup.OpposingSide(side, state);
            if (enemy == null) return;

            var candidates = enemy.Board.VanguardRow
                .Where(s => s.Occupant != null && s.Occupant.IsAlive)
                .Select(s => s.Occupant)
                .ToList();

            IGameEntity target = _preferHighest
                ? candidates.OrderByDescending(c => c.Health).FirstOrDefault()
                : candidates.FirstOrDefault();

            if (target == null && enemy.Board.TownSlot.Occupant != null && enemy.Board.TownSlot.Occupant.IsAlive)
                target = enemy.Board.TownSlot.Occupant;

            if (target == null) return;

            await new DamageAction(target, _amount, _owner).ExecuteAsync();
        }
    }
}
