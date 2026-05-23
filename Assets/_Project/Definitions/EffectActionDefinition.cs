using UnityEngine;

namespace Definitions
{
    public abstract class EffectActionDefinition : ScriptableObject
    {
        public abstract IGameAction CreateAction(ICardPlayContext context);
    }
}
