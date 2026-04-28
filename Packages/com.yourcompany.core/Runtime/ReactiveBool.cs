using System;

namespace Core
{
    [Serializable]
    public class ReactiveBool : ReactiveProperty<bool>
    {
        public ReactiveBool(bool value = false) : base(value) { }
    }
}