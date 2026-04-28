using System;

namespace Core
{
    [Serializable]
    public class ReactiveInt : ReactiveProperty<int>
    {
        public ReactiveInt(int value = 0) : base(value) { }
    }
}