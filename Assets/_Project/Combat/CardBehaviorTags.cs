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

        public static bool IsProvoker(BoardCard c)
            => c != null && c.IsAlive && (c.SourceCard.CardName == FreshSpawn || c.SourceCard.CardName == Decoy);

        public static bool HasDoubleAttackWhenAlone(BoardCard c)
            => c != null && c.SourceCard.CardName == VampireLoner;

        public static bool DiesAfterAttacking(BoardCard c)
            => c != null && c.SourceCard.CardName == Ritualist;

        public static BoardCard GetActiveProvokerOn(SideState side)
        {
            foreach (var slot in side.Board.VanguardRow)
                if (IsProvoker(slot.Occupant)) return slot.Occupant;
            return null;
        }

        public static bool IsAloneOnVanguard(BoardCard c, SideState side)
        {
            int count = 0;
            foreach (var slot in side.Board.VanguardRow)
                if (slot.Occupant != null && slot.Occupant.IsAlive) count++;
            return count == 1 && side.Board.VanguardRow.Any(s => s.Occupant == c);
        }
    }
}
