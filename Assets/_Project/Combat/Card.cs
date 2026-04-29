using Definitions;

namespace Combat
{
    public class Card : ICard
    {
        public CardDef Def { get; }
        public int InstanceId { get; }

        public Card(CardDef def, int instanceId)
        {
            Def = def;
            InstanceId = instanceId;
        }
    }
}
