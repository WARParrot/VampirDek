using System;
using System.Collections.Generic;
using System.Linq;
using Definitions;

namespace Combat
{
    [Serializable]
    public class MatchStateDTO
    {
        public string EncounterId;
        public SideSnapshot PlayerSide;
        public SideSnapshot OpponentSide;
        public string CurrentPhaseId;
        public int TurnNumber;

        public static MatchStateDTO FromDuelState(DuelState state)
        {
            return new MatchStateDTO
            {
                EncounterId = "",
                PlayerSide = SideSnapshot.FromSideState(state.PlayerSide),
                OpponentSide = SideSnapshot.FromSideState(state.OpponentSide),
                CurrentPhaseId = state.CurrentPhase.PhaseId,
                TurnNumber = state.TurnNumber
            };
        }
    }

    [Serializable]
    public class SideSnapshot
    {
        public int Mana;
        public List<string> DeckCardIds;
        public List<string> HandCardIds;
        public List<string> GraveyardCardIds;
        public BoardSnapshot Board;

        public static SideSnapshot FromSideState(SideState side)
        {
            return new SideSnapshot
            {
                Mana = side.Mana,
                DeckCardIds = side.Deck.Select(c => c.Def.CardName).ToList(),
                HandCardIds = side.Hand.Select(c => c.Def.CardName).ToList(),
                GraveyardCardIds = side.Graveyard.Select(c => c.Def.CardName).ToList(),
                Board = BoardSnapshot.FromBoard(side.Board)
            };
        }
    }

    [Serializable]
    public class BoardSnapshot
    {
        public List<SlotSnapshot> Slots;

        public static BoardSnapshot FromBoard(Board board)
        {
            return new BoardSnapshot
            {
                /*Slots = board.Slots.Select(s => new SlotSnapshot
                {
                    Index = s.Index,
                    AllowedType = s.AllowedType,
                    IsTownSlot = s.IsTownSlot,
                    OccupantCardId = s.Occupant?.SourceCard.CardName ?? "",
                    OccupantHealth = s.Occupant?.Health ?? 0,
                    OccupantMaxHealth = s.Occupant?.MaxHealth ?? 0,
                    OccupantSpeed = s.Occupant?.Speed ?? 1
                }).ToList()*/
            };
        }
    }

    [Serializable]
    public class SlotSnapshot
    {
        public int Index;
        public SlotType AllowedType;
        public bool IsTownSlot;
        public string OccupantCardId;
        public int OccupantHealth;
        public int OccupantMaxHealth;
        public int OccupantSpeed;
    }
}
