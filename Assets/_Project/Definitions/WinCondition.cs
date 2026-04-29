using UnityEngine;

namespace Definitions
{
    public abstract class WinCondition : ScriptableObject
    {
        public abstract bool Check(IDuelState state);
    }
}
