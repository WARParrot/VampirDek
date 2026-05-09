using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public class DuelStartContext
    {
        public CombatEncounter Encounter;
        public List<CardDef> PlayerDeck;
        public string TableId;
        public MatchStateDTO SavedMatchState;
    }
}
