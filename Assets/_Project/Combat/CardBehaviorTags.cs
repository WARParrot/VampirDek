using System.Linq;
using Definitions;

namespace Combat
{
    public static class CardBehaviorTags
    {
        public const string FreshSpawn = "FreshSpawn";
        public const string Decoy = "Decoy";
        public const string VampireLoner = "VampireLoner";
        public const string Ritualist = "Ritualist";
        public const string Ghoul = "Ghoul";
        public const string Gourmet = "Gourmet";

        public static bool IsProvoker(BoardCard c)
            => c != null && c.IsAlive && (c.SourceCard.CardName == FreshSpawn || c.SourceCard.CardName == Decoy);

        // VampireLoner is now Elusive: can only be hit by effects, not by direct attacks.
        public static bool IsElusive(BoardCard c)
            => c != null && c.SourceCard.CardName == VampireLoner;

        public static bool DiesAfterAttacking(BoardCard c)
            => c != null && c.SourceCard.CardName == Ritualist;

        // Gourmet refuses to attack the same target twice across the duel.
        public static bool NeverRepeatsTarget(BoardCard c)
            => c != null && c.SourceCard.CardName == Gourmet;

        public static BoardCard GetActiveProvokerOn(SideState side)
        {
            foreach (var slot in side.Board.VanguardRow)
                if (IsProvoker(slot.Occupant)) return slot.Occupant;
            return null;
        }
    }
}
