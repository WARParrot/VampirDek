using Cysharp.Threading.Tasks;
using Core;
using Definitions;

namespace Combat
{
    public class DamageAction : IGameAction, IPreventableAction
    {
        public bool IsPrevented { get; set; }
        private readonly IGameEntity _target;
        private readonly int _amount;
        private readonly IGameEntity _source;

        public string Description => $"Deal {_amount} damage to {_target.Id}";

        public DamageAction(IGameEntity target, int amount, IGameEntity source)
        {
            _target = target;
            _amount = amount;
            _source = source;
        }

        public async UniTask ExecuteAsync()
        {
            if (IsPrevented)
            {
                OnPrevention();
                return;
            }

            var preEvent = new PreDamageEvent(_target, _amount, _source);
            GlobalServices.EventBus.Publish(preEvent);
            if (preEvent.Prevented)
            {
                OnPrevention();
                return;
            }

            int final = preEvent.ModifiedAmount;
            _target.TakeDamage(final, _source);
            GlobalServices.EventBus.Publish(new DamageDealtEvent(_target, final));
        }

        public void OnPrevention() => GlobalServices.EventBus.Publish(new DamagePreventedEvent(_target, _source));
    }
}
