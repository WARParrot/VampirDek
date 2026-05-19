using UnityEngine;

namespace Definitions
{
    public abstract class HintCondition : ScriptableObject
    {
        public abstract bool IsMet(object context);
    }
}