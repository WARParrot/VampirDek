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
        [SerializeField, Min(1)] private int _shots = 1;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new DealDamageToRandomEnemyAction(owner, _amount, _includeTown, _shots);
    }

    public class DealDamageToRandomEnemyAction : IGameAction
    {
        private static readonly System.Random _rng = new();
        private readonly IGameEntity _owner;
        private readonly int _amount;
        private readonly bool _includeTown;
        private readonly int _shots;

        public string Description => $"Deal {_amount}x{_shots} damage to random enemy from {_owner?.Id}";

        public DealDamageToRandomEnemyAction(IGameEntity owner, int amount, bool includeTown, int shots = 1)
        {
            _owner = owner;
            _amount = amount;
            _includeTown = includeTown;
            _shots = Mathf.Max(1, shots);
        }

        public async UniTask ExecuteAsync()
        {
            if (_owner is BoardCard bc && !bc.IsAlive) return;

            var side = SideLookup.FindSideOf(_owner);
            var state = Object.FindObjectOfType<DuelManager>()?.CurrentDuelState;
            var enemy = SideLookup.OpposingSide(side, state);
            if (enemy == null) return;

            for (int shot = 0; shot < _shots; shot++)
            {
                var pool = enemy.Board.VanguardRow
                    .Where(s => s.Occupant != null && s.Occupant.IsAlive)
                    .Select(s => (IGameEntity)s.Occupant)
                    .ToList();
                if (_includeTown && enemy.Board.TownSlot.Occupant != null && enemy.Board.TownSlot.Occupant.IsAlive)
                    pool.Add(enemy.Board.TownSlot.Occupant);

                if (pool.Count == 0) return;
                var target = pool[_rng.Next(pool.Count)];

                // Explicit red beam: damage events that fire in the same frame collapse into one
                // visible trail because PlayDirectedAttack DOCompletes the source. Drawing our own
                // beam per shot guarantees N visible rays.
                Combat.UI.CombatVFX.PlaySpellShot(_owner, target, new Color(0.95f, 0.05f, 0.08f, 1f));
                await new DamageAction(target, _amount, _owner).ExecuteAsync();
                if (shot < _shots - 1)
                    await UniTask.Delay(System.TimeSpan.FromMilliseconds(220));
            }
        }
    }
}
