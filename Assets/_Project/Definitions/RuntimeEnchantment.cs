using System;
using System.Collections.Generic;
using Core;
using Definitions;
using Cysharp.Threading.Tasks;

namespace Definitions
{
    public class RuntimeEnchantment
    {
        private readonly EnchantmentData _data;
        private readonly IGameEntity _owner;
        private readonly List<IDisposable> _subscriptions = new();
        private int _durationLeft;
        public bool IsExpired => _data.Duration != -1 && _durationLeft <= 0;
        public IGameEntity Owner => _owner;
        public EnchantmentData Data => _data;

        public RuntimeEnchantment(EnchantmentData data, IGameEntity owner)
        {
            _data = data;
            _owner = owner;
            _durationLeft = data.Duration;
        }

        public void OnAttach()
        {
            foreach (var trigger in _data.Triggers)
            {
                var eventType = Type.GetType(trigger.EventType);
                if (eventType == null) continue;
                var method = typeof(EventBus).GetMethod("Subscribe").MakeGenericMethod(eventType);
                var actionType = typeof(Action<>).MakeGenericType(eventType);
                var handler = Delegate.CreateDelegate(actionType, this, nameof(OnEvent));
                var subscription = (IDisposable)method.Invoke(GlobalServices.EventBus, new[] { handler });
                _subscriptions.Add(subscription);
            }
            ApplyModifiers(1);
        }

        public void OnDetach()
        {
            foreach (var sub in _subscriptions) sub.Dispose();
            _subscriptions.Clear();
            ApplyModifiers(-1);
        }

        public void OnEvent<T>(T evt) where T : IGameEvent
        {
            foreach (var trigger in _data.Triggers)
            {
                if (trigger.EventType == typeof(T).FullName)
                {
                    foreach (var actionDef in trigger.Actions)
                    {
                        var action = actionDef.CreateAction();
                        action.ExecuteAsync().Forget();
                    }
                }
            }
        }

        private void ApplyModifiers(int sign)
        {
            if (_owner == null) return;
            foreach (var mod in _data.Modifiers)
            {
                switch (mod.Stat)
                {
                    case "Attack": _owner.ModifyAttack(mod.Value * sign); break;
                    case "Health": _owner.ModifyMaxHealth(mod.Value * sign); break;
                }
            }
        }
    }
}
