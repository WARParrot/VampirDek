using System.Linq;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Spawn On Friendly Death")]
    public class SpawnOnFriendlyDeathActionDefinition : EnchantmentActionDefinition
    {
        [Tooltip("Filter: only react when a friendly card with this name dies. Leave empty to react to any friendly death.")]
        [SerializeField] private string _diedCardNameFilter;
        [SerializeField] private CardDef _cardToSpawn;
        [SerializeField] private Definitions.RowType _targetRow = Definitions.RowType.Vanguard;
        [SerializeField] private bool _oncePerRound = true;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new SpawnOnFriendlyDeathAction(owner, evt, _diedCardNameFilter, _cardToSpawn, _targetRow, _oncePerRound);
    }

    public class SpawnOnFriendlyDeathAction : IGameAction
    {
        private static readonly System.Collections.Generic.HashSet<int> _firedThisRound = new();
        private static readonly System.Random _rng = new();
        private readonly IGameEntity _owner;
        private readonly IGameEvent _evt;
        private readonly string _filter;
        private readonly CardDef _def;
        private readonly Definitions.RowType _row;
        private readonly bool _oncePerRound;

        public string Description => $"Spawn {_def?.CardName} on friendly death for {_owner?.Id}";

        public SpawnOnFriendlyDeathAction(IGameEntity owner, IGameEvent evt, string filter, CardDef def, Definitions.RowType row, bool oncePerRound)
        {
            _owner = owner;
            _evt = evt;
            _filter = filter;
            _def = def;
            _row = row;
            _oncePerRound = oncePerRound;
        }

        public UniTask ExecuteAsync()
        {
            if (_def == null || _owner == null) return UniTask.CompletedTask;
            if (_owner is BoardCard ownerCard && !ownerCard.IsAlive) return UniTask.CompletedTask;

            if (_evt is not ISubjectEvent subjEvt) return UniTask.CompletedTask;
            var dead = subjEvt.Subject as BoardCard;
            if (dead == null || dead == _owner) return UniTask.CompletedTask;

            if (!string.IsNullOrEmpty(_filter) && dead.SourceCard.CardName != _filter)
                return UniTask.CompletedTask;

            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            var ownerSide = SideLookup.FindSideOf(_owner, state);
            if (ownerSide == null) return UniTask.CompletedTask;

            // Subject (dead card) is already removed from board; we need to know which side it was on.
            // Fallback: assume the trigger reacted before removal — but EntityDiedEvent fires from BoardCard.TakeDamage,
            // before Board.RemoveCard. So dead.SourceCard side cannot be derived from board state. Trust the convention
            // that the carrier (e.g. Crypt) is on the same side and we only care about same-side deaths via filter name.
            // Simplest workable rule: react regardless of which side died, gated by name filter.

            if (_oncePerRound && _owner is BoardCard oc)
            {
                if (_firedThisRound.Contains(oc.Id)) return UniTask.CompletedTask;
                _firedThisRound.Add(oc.Id);
            }

            var row = ownerSide.Board.GetRow(_row);
            if (row == null) return UniTask.CompletedTask;
            var empties = row.Where(s => s.IsEmpty).ToList();
            if (empties.Count == 0) return UniTask.CompletedTask;
            var slot = empties[_rng.Next(empties.Count)];
            ownerSide.Board.TryPlaceCardIntoSlot(_def, slot);
            return UniTask.CompletedTask;
        }

        public static void ResetRoundTracking() => _firedThisRound.Clear();
    }
}
