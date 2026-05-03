using System.Collections.Generic;
using System.Linq;
using Definitions;

namespace Combat
{
    public class DuelState : IDuelState
    {
        public SideState PlayerSide { get; }
        public SideState OpponentSide { get; }
        public IGameEntity PlayerTown => PlayerSide.Board.TownSlot?.Occupant;
        public IGameEntity OpponentTown => OpponentSide.Board.TownSlot?.Occupant;
        public PhaseGraph PhaseGraph { get; }
        public PhaseNode CurrentPhase { get; set; }
        public int TurnNumber { get; set; }
        public CombatEncounter Encounter { get; private set; }

        public DuelState(CombatEncounter encounter, List<CardDef> playerDeck, List<CardDef> opponentDeck)
        {
            Encounter = encounter;
            PlayerSide = new SideState(encounter.PlayerBoardLayout);
            OpponentSide = new SideState(encounter.OpponentBoardLayout);

            int nextId = 0;
            PlayerSide.Deck = playerDeck.Select(d => new Card(d, nextId++)).ToList();
            OpponentSide.Deck = opponentDeck.Select(d => new Card(d, nextId++)).ToList();

            PlayerSide.InitializeTown();
            OpponentSide.InitializeTown();

            PhaseGraph = encounter.PhaseGraph;
            CurrentPhase = PhaseGraph.StartingNode;
            TurnNumber = 1;
        }
    }
}
