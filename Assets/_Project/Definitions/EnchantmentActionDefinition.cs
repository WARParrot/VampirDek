using Core;

namespace Definitions
{
    public abstract class EnchantmentActionDefinition : ActionDefinition
    {
        public override IGameAction CreateAction() => CreateAction(null, default);
        public abstract IGameAction CreateAction(IGameEntity owner, IGameEvent evt);
    }
}
