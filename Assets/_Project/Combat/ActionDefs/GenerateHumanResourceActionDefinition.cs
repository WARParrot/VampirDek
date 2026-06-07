using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Action Def/Generate Human Resource")]
    public class GenerateHumanResourceActionDefinition : EnchantmentActionDefinition
    {
        [SerializeField] private int _amount = 1;

        public override IGameAction CreateAction(IGameEntity owner, IGameEvent evt)
            => new GenerateHumanResourceAction(owner, _amount);
    }

    public class GenerateHumanResourceAction : IGameAction
    {
        private readonly IGameEntity _owner;
        private readonly int _amount;
        public string Description => $"Generate {_amount} HR for owner of {_owner?.Id}";

        public GenerateHumanResourceAction(IGameEntity owner, int amount)
        {
            _owner = owner;
            _amount = amount;
        }

        public UniTask ExecuteAsync()
        {
            if (_owner is BoardCard bc && !bc.IsAlive) return UniTask.CompletedTask;
            var side = SideLookup.FindSideOf(_owner);
            if (side == null) return UniTask.CompletedTask;
            side.HumanResources += _amount;
            GlobalServices.EventBus.Publish(new ManaChangedEvent(side));
            return UniTask.CompletedTask;
        }
    }
}
