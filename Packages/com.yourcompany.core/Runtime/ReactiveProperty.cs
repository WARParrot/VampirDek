using System;
using UnityEngine;

namespace Core
{
    [Serializable]
    public class ReactiveProperty<T>
    {
        [SerializeField] private T _value;

        // Событие изменения: старое значение, новое значение
        public event Action<T, T> OnValueChanged;

        public ReactiveProperty(T initialValue = default)
        {
            _value = initialValue;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (Equals(_value, value)) return; // Не дёргаем, если значение не изменилось
                
                T oldValue = _value;
                _value = value;
                OnValueChanged?.Invoke(oldValue, value);
            }
        }

        // Чтобы объект красиво печатался в Debug.Log
        public override string ToString() => _value?.ToString() ?? "null";
    }
}