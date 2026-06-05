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
                EncounterId = state.Encounter.EncounterId,
                PlayerSide = SideSnapshot.FromSideState(state.PlayerSide),
                OpponentSide = SideSnapshot.FromSideState(state.OpponentSide),
                CurrentPhaseId = state.CurrentPhase.PhaseId,
                TurnNumber = state.TurnNumber
            };
        }

        public DuelState ToDuelState(CombatEncounter encounter, List<CardDef> playerDeckDefinitions, List<CardDef> opponentDeckDefinitions)
        {
            var state = new DuelState(encounter, playerDeckDefinitions, opponentDeckDefinitions);

            state.PlayerSide.Mana = PlayerSide.Mana;
            state.PlayerSide.HumanResources = PlayerSide.HumanResources;
            RestoreBoard(PlayerSide, state.PlayerSide.Board);

            state.OpponentSide.Mana = OpponentSide.Mana;
            state.OpponentSide.HumanResources = OpponentSide.HumanResources;
            RestoreBoard(OpponentSide, state.OpponentSide.Board);

            state.PlayerSide.Hand.Clear();
            state.PlayerSide.Hand.AddRange(RebuildCardList(PlayerSide.HandCardIds));

            state.PlayerSide.Deck.Clear();
            state.PlayerSide.Deck.AddRange(RebuildCardList(PlayerSide.DeckCardIds));

            state.PlayerSide.Graveyard.Clear();
            state.PlayerSide.Graveyard.AddRange(RebuildCardList(PlayerSide.GraveyardCardIds));

            state.OpponentSide.Hand.Clear();
            state.OpponentSide.Hand.AddRange(RebuildCardList(OpponentSide.HandCardIds));

            state.OpponentSide.Deck.Clear();
            state.OpponentSide.Deck.AddRange(RebuildCardList(OpponentSide.DeckCardIds));

            state.OpponentSide.Graveyard.Clear();
            state.OpponentSide.Graveyard.AddRange(RebuildCardList(OpponentSide.GraveyardCardIds));

            state.CurrentPhase = PhaseGraphDatabase.GetPhaseGraph(encounter.PhaseGraphId).Nodes.Find(n => n.PhaseId == CurrentPhaseId);
            state.TurnNumber = TurnNumber;

            return state;
        }

        private List<Card> RebuildCardList(List<string> cardIds)
        {
            var cards = new List<Card>();
            int nextId = 10000;
            foreach (var id in cardIds)
            {
                var def = CardDatabase.GetCard(id);
                if (def != null)
                    cards.Add(new Card(def, nextId++));
            }
            return cards;
        }

        private void RestoreBoard(SideSnapshot sideSnapshot, Board board)
        {
            foreach (var slotSnap in sideSnapshot.Board.Slots)
            {
                BoardSlot slot = null;
                switch (slotSnap.RowType)
                {
                    case Definitions.RowType.Vanguard:
                        if (slotSnap.Index < board.VanguardRow.Length) slot = board.VanguardRow[slotSnap.Index];
                        break;
                    case Definitions.RowType.Building:
                        if (slotSnap.Index < board.BuildingRow.Length) slot = board.BuildingRow[slotSnap.Index];
                        break;
                    case Definitions.RowType.Human:
                        if (slotSnap.Index < board.HumanRow.Length) slot = board.HumanRow[slotSnap.Index];
                        break;
                    case Definitions.RowType.Town:
                        slot = board.TownSlot;
                        break;
                }
                if (slot == null) continue;

                if (!string.IsNullOrEmpty(slotSnap.OccupantCardId))
                {
                    var cardDef = CardDatabase.GetCard(slotSnap.OccupantCardId);
                    if (cardDef == null) continue;

                    var card = new BoardCard(cardDef);
                    RestoreEnchantments(slotSnap, card);
                    RestoreMutableStats(slotSnap, card);

                    slot.Occupant = card;
                }
                else
                {
                    slot.Occupant = null;
                }
            }
        }

        private void RestoreEnchantments(SlotSnapshot slotSnap, BoardCard card)
        {
            var snapshots = slotSnap.Enchantments;
            if (snapshots == null || snapshots.Count == 0)
            {
                card.ApplyInnateEnchantments();
                return;
            }

            var restoredDataIds = new HashSet<string>();
            foreach (var enchSnap in snapshots)
            {
                if (enchSnap == null || string.IsNullOrEmpty(enchSnap.EnchantmentDataId)) continue;
                if (!restoredDataIds.Add(enchSnap.EnchantmentDataId)) continue;

                var enchData = EnchantmentDatabase.GetEnchantment(enchSnap.EnchantmentDataId);
                if (enchData == null) continue;

                var runtime = EnchantmentFactory.Create(enchData, card);
                card.Enchantments.Add(runtime);
                runtime.OnAttach();
                runtime.SetDurationLeft(enchSnap.DurationLeft);
            }
        }

        private static void RestoreMutableStats(SlotSnapshot slotSnap, BoardCard card)
        {
            card.SetHealth(
                Math.Min(slotSnap.OccupantHealth, card.MaxHealth),
                card.MaxHealth);
            card.CurrentSpeed = slotSnap.OccupantSpeed;
        }

    }

    [Serializable]
    public class SideSnapshot
    {
        public int Mana;
        public int HumanResources;
        public List<string> DeckCardIds;
        public List<string> HandCardIds;
        public List<string> GraveyardCardIds;
        public BoardSnapshot Board;

        public static SideSnapshot FromSideState(SideState side)
        {
            return new SideSnapshot
            {
                Mana = side.Mana,
                HumanResources = side.HumanResources,
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
        public List<SlotSnapshot> Slots = new();

        public static BoardSnapshot FromBoard(Board board)
        {
            var snap = new BoardSnapshot();

            for (int i = 0; i < board.VanguardRow.Length; i++)
                snap.Slots.Add(SlotSnapshot.FromSlot(board.VanguardRow[i], i));
            for (int i = 0; i < board.BuildingRow.Length; i++)
                snap.Slots.Add(SlotSnapshot.FromSlot(board.BuildingRow[i], i));
            for (int i = 0; i < board.HumanRow.Length; i++)
                snap.Slots.Add(SlotSnapshot.FromSlot(board.HumanRow[i], i));
            snap.Slots.Add(SlotSnapshot.FromSlot(board.TownSlot, 0));

            return snap;
        }
    }

    [Serializable]
    public class SlotSnapshot
    {
        public int Index;
        public Definitions.RowType RowType;
        public bool IsTownSlot;
        public string OccupantCardId;
        public int OccupantHealth;
        public int OccupantMaxHealth;
        public int OccupantSpeed;
        public int OccupantAttack;
        public List<EnchantmentSnapshot> Enchantments;

        public static SlotSnapshot FromSlot(BoardSlot slot, int rowSlotIndex)
        {
            return new SlotSnapshot
            {
                Index = rowSlotIndex,
                RowType = slot.AllowedRow,
                OccupantCardId = slot.Occupant?.SourceCard?.CardName ?? "",
                OccupantHealth = slot.Occupant?.Health ?? 0,
                OccupantMaxHealth = slot.Occupant?.MaxHealth ?? 0,
                OccupantSpeed = slot.Occupant?.CurrentSpeed ?? 0,
                OccupantAttack = slot.Occupant?.Attack ?? 0,
                Enchantments = slot.Occupant?.Enchantments?.Select(e => new EnchantmentSnapshot
                {
                    EnchantmentDataId = e.Data?.DisplayName ?? "",
                    DurationLeft = e.DurationLeft
                }).ToList() ?? new List<EnchantmentSnapshot>()
            };
        }
    }
    
    [Serializable]
    public class EnchantmentSnapshot
    {
        public string EnchantmentDataId;
        public int DurationLeft;
    }
}
