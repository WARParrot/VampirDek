using UnityEngine;

namespace Definitions
{
    public abstract class CostActionDefinition : ScriptableObject
    {
        public abstract IGameAction CreateAction(ICostContext context);
    }
}
