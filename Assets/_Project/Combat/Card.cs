using Definitions;

namespace Combat
{
    public class Card(CardDef def, int instanceId) : ICard
    {
        public CardDef Def { get; } = def;
        public int InstanceId { get; } = instanceId;
    }
}
