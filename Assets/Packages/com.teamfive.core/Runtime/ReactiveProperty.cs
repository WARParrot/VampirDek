using System;

namespace Core
{
    public class ReactiveProperty<T>
    {
        private T _value;
        public event Action<T> OnChanged;

        public T Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    _value = value;
                    OnChanged?.Invoke(_value);
                }
            }
        }

        public ReactiveProperty(T initialValue = default) => _value = initialValue;
        public void SetWithoutNotify(T value) => _value = value;
    }
}
