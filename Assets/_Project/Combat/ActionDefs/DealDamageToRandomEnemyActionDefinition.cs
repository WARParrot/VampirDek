using System.Linq;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Deal Damage (Random Enemy)")]
    public class DealDamageToRandomEnemyActionDefinition : EnchantmentActionDefinition
    {
        [SerializeField] private int _amount = 2;
        [SerializeField] private bool _includeTown = true;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new DealDamageToRandomEnemyAction(owner, _amount, _includeTown);
    }

    public class DealDamageToRandomEnemyAction : IGameAction
    {
        private static readonly System.Random _rng = new();
        private readonly IGameEntity _owner;
        private readonly int _amount;
        private readonly bool _includeTown;

        public string Description => $"Deal {_amount} damage to random enemy from {_owner?.Id}";

        public DealDamageToRandomEnemyAction(IGameEntity owner, int amount, bool includeTown)
        {
            _owner = owner;
            _amount = amount;
            _includeTown = includeTown;
        }

        public async UniTask ExecuteAsync()
        {
            if (_owner is BoardCard bc && !bc.IsAlive) return;

            var side = SideLookup.FindSideOf(_owner);
            var state = Object.FindObjectOfType<DuelManager>()?.CurrentDuelState;
            var enemy = SideLookup.OpposingSide(side, state);
            if (enemy == null) return;

            var pool = enemy.Board.VanguardRow
                .Where(s => s.Occupant != null && s.Occupant.IsAlive)
                .Select(s => (IGameEntity)s.Occupant)
                .ToList();
            if (_includeTown && enemy.Board.TownSlot.Occupant != null && enemy.Board.TownSlot.Occupant.IsAlive)
                pool.Add(enemy.Board.TownSlot.Occupant);

            if (pool.Count == 0) return;
            var target = pool[_rng.Next(pool.Count)];
            await new DamageAction(target, _amount, _owner).ExecuteAsync();
        }
    }
}
