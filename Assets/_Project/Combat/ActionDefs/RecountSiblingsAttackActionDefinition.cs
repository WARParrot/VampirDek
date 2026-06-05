using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Recount Siblings Attack")]
    public class RecountSiblingsAttackActionDefinition : EnchantmentActionDefinition
    {
        [Tooltip("Sibling carrier card name (e.g. 'Ghoul'). Counts other living cards on same side with same SourceCard.CardName.")]
        [SerializeField] private string _siblingCardName;
        [SerializeField] private int _baseAttack = 1;
        [SerializeField] private int _bonusPerSibling = 1;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new RecountSiblingsAttackAction(owner, _siblingCardName, _baseAttack, _bonusPerSibling);
    }

    public class RecountSiblingsAttackAction : IGameAction
    {
        private readonly IGameEntity _owner;
        private readonly string _siblingName;
        private readonly int _baseAttack;
        private readonly int _bonusPerSibling;
        public string Description => $"Recount {_siblingName} siblings for {_owner?.Id}";

        public RecountSiblingsAttackAction(IGameEntity owner, string siblingName, int baseAttack, int bonusPerSibling)
        {
            _owner = owner;
            _siblingName = siblingName;
            _baseAttack = baseAttack;
            _bonusPerSibling = bonusPerSibling;
        }

        public UniTask ExecuteAsync()
        {
            if (_owner is not BoardCard owner || !owner.IsAlive) return UniTask.CompletedTask;
            var side = SideLookup.FindSideOf(owner);
            if (side == null) return UniTask.CompletedTask;

            int siblings = 0;
            foreach (var slot in side.Board.AllSlots())
            {
                var c = slot.Occupant;
                if (c == null || !c.IsAlive || c == owner) continue;
                if (c.SourceCard.CardName == _siblingName) siblings++;
            }

            int newAttack = _baseAttack + siblings * _bonusPerSibling;
            owner.SetAttack(newAttack);
            return UniTask.CompletedTask;
        }
    }
}
