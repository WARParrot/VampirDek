using UnityEngine;
using Cysharp.Threading.Tasks;
using Definitions;
using Core;

namespace Combat
{
    public class DamageAction(IGameEntity target, int amount, IGameEntity source) : IGameAction, IPreventableAction
    {
        public bool IsPrevented { get; set; }
        private readonly int _amount = amount;
        private readonly IGameEntity _source = source;
        private readonly IGameEntity _target = target;
        
        public string Description => $"Deal {_amount} damage to {_target.Id}";

        public async UniTask ExecuteAsync()
        {
            if (IsPrevented)
            {
                OnPrevention();
                return;
            }
            
            int preEvent = new PreDamageEvent(_target, _amount, _source);
            GlobalServices.EventBus.Publish(preEvent);
            if (preEvent.IsPrevented)
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
