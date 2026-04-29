using UnityEngine;

namespace Definitions
{
    public abstract class ActionDefinition : ScriptableObject
    {
        public abstract IGameAction CreateAction();
    }
}
