using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Modify Owner Attack")]
    public class ModifyOwnerAttackActionDefinition : EnchantmentActionDefinition
    {
        [SerializeField] private int _delta = 1;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new ModifyOwnerAttackAction(owner, _delta);
    }

    public class ModifyOwnerAttackAction : IGameAction
    {
        private readonly IGameEntity _owner;
        private readonly int _delta;
        public string Description => $"Modify Attack of {_owner?.Id} by {_delta}";

        public ModifyOwnerAttackAction(IGameEntity owner, int delta)
        {
            _owner = owner;
            _delta = delta;
        }

        public UniTask ExecuteAsync()
        {
            if (_owner == null) return UniTask.CompletedTask;
            if (_owner is BoardCard bc && !bc.IsAlive) return UniTask.CompletedTask;
            _owner.ModifyAttack(_delta);
            return UniTask.CompletedTask;
        }
    }
}
