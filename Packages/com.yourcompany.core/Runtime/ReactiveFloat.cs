using System;

namespace Core
{
    [Serializable]
    public class ReactiveFloat : ReactiveProperty<float>
    {
        public ReactiveFloat(float value = 0f) : base(value) { }
    }
}