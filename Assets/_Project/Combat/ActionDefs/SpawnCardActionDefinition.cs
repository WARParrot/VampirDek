using System.Linq;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    public enum SpawnSlotRule { RandomEmpty, AdjacentRight, AdjacentLeft }

    [CreateAssetMenu(menuName = "Action Def/Spawn Card")]
    public class SpawnCardActionDefinition : EnchantmentActionDefinition
    {
        [SerializeField] private CardDef _cardToSpawn;
        [SerializeField] private Definitions.RowType _targetRow = Definitions.RowType.Human;
        [SerializeField] private SpawnSlotRule _slotRule = SpawnSlotRule.AdjacentRight;
        [SerializeField] private bool _onlyIfTargetRowMatchesCard = true;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new SpawnCardAction(owner, _cardToSpawn, _targetRow, _slotRule, _onlyIfTargetRowMatchesCard);
    }

    public class SpawnCardAction : IGameAction
    {
        private static readonly System.Random _rng = new();
        private readonly IGameEntity _owner;
        private readonly CardDef _def;
        private readonly Definitions.RowType _row;
        private readonly SpawnSlotRule _rule;
        private readonly bool _enforceRow;

        public string Description => $"Spawn {_def?.CardName} for {_owner?.Id}";

        public SpawnCardAction(IGameEntity owner, CardDef def, Definitions.RowType row, SpawnSlotRule rule, bool enforceRow)
        {
            _owner = owner;
            _def = def;
            _row = row;
            _rule = rule;
            _enforceRow = enforceRow;
        }

        public UniTask ExecuteAsync()
        {
            if (_def == null) return UniTask.CompletedTask;
            if (_enforceRow && _def.RowType != _row) return UniTask.CompletedTask;

            var side = SideLookup.FindSideOf(_owner);
            if (side == null) return UniTask.CompletedTask;

            var row = side.Board.GetRow(_row);
            if (row == null || row.Length == 0) return UniTask.CompletedTask;

            BoardSlot target = null;
            switch (_rule)
            {
                case SpawnSlotRule.RandomEmpty:
                {
                    var empties = row.Where(s => s.IsEmpty).ToList();
                    if (empties.Count > 0) target = empties[_rng.Next(empties.Count)];
                    break;
                }
                case SpawnSlotRule.AdjacentRight:
                case SpawnSlotRule.AdjacentLeft:
                {
                    int ownerIdx = -1;
                    for (int i = 0; i < row.Length; i++) if (row[i].Occupant == _owner) { ownerIdx = i; break; }
                    int dir = _rule == SpawnSlotRule.AdjacentRight ? 1 : -1;
                    if (ownerIdx >= 0)
                    {
                        for (int step = 1; step < row.Length; step++)
                        {
                            int idx = ownerIdx + dir * step;
                            if (idx < 0 || idx >= row.Length) break;
                            if (row[idx].IsEmpty) { target = row[idx]; break; }
                        }
                    }
                    if (target == null) target = row.FirstOrDefault(s => s.IsEmpty);
                    break;
                }
            }

            if (target == null) return UniTask.CompletedTask;
            side.Board.TryPlaceCardIntoSlot(_def, target);
            return UniTask.CompletedTask;
        }
    }
}
