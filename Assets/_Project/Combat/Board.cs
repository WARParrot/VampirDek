using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;

namespace Combat
{
    public class Board : IBoard
    {
        public BoardSlot[] VanguardRow { get; private set; }
        public BoardSlot[] BuildingRow { get; private set; }
        public BoardSlot[] HumanRow { get; private set; }
        public BoardSlot TownSlot { get; private set; }

        IBoardSlot IBoard.TownSlot => TownSlot;
        IBoardSlot[] IBoard.VanguardRow => VanguardRow;
        IBoardSlot[] IBoard.BuildingRow => BuildingRow;
        IBoardSlot[] IBoard.HumanRow => HumanRow;

        public Board(BoardLayoutData layout)
        {
            var vanguard = new List<BoardSlot>();
            var building = new List<BoardSlot>();
            var humanList = new List<BoardSlot>();

            foreach (var def in layout.Slots)
            {
                var slot = new BoardSlot { AllowedRow = def.Row, Index = def.Index };
                switch (def.Row)
                {
                    case RowType.Vanguard: vanguard.Add(slot); break;
                    case RowType.Building: building.Add(slot); break;
                    case RowType.Human:    humanList.Add(slot); break;
                    case RowType.Town:     TownSlot = slot; break;
                }
            }
            VanguardRow = vanguard.ToArray();
            BuildingRow = building.ToArray();
            HumanRow = humanList.ToArray();
        }

        public IEnumerable<BoardCard> AllCards()
        {
            foreach (var s in VanguardRow) if (s.Occupant != null) yield return s.Occupant;
            foreach (var s in BuildingRow) if (s.Occupant != null) yield return s.Occupant;
            foreach (var s in HumanRow) if (s.Occupant != null) yield return s.Occupant;
            if (TownSlot.Occupant != null) yield return TownSlot.Occupant;
        }

        public bool TryPlaceCard(CardDef cardDef, out BoardSlot placedSlot)
        {
            placedSlot = null;
            BoardSlot[] row = GetRow(cardDef.RowType);
            if (row == null) return false;
            var emptySlot = row.FirstOrDefault(s => s.IsEmpty);
            if (emptySlot == null) return false;

            var card = new BoardCard(cardDef);
            card.ApplyInnateEnchantments();
            emptySlot.Occupant = card;
            placedSlot = emptySlot;
            GlobalServices.EventBus.Publish(new CreatureSummonedEvent(card, this));
            return true;
        }

        public void PlaceTownCard(CardDef townDef)
        {
            if (!TownSlot.IsEmpty) throw new InvalidOperationException("Town slot is occupied.");
            var town = new BoardCard(townDef) { IsTown = true };
            town.ApplyInnateEnchantments();
            TownSlot.Occupant = town;
            GlobalServices.EventBus.Publish(new TownPlacedEvent(town, this));
        }

        public void RemoveCard(BoardCard card)
        {
            foreach (var slot in AllSlots())
            {
                if (slot.Occupant == card)
                {
                    slot.Occupant = null;
                    return;
                }
            }
        }

        private IEnumerable<BoardSlot> AllSlots()
        {
            foreach (var s in VanguardRow) yield return s;
            foreach (var s in BuildingRow) yield return s;
            foreach (var s in HumanRow) yield return s;
            yield return TownSlot;
        }

        private BoardSlot[] GetRow(RowType rowType) => rowType switch
        {
            RowType.Vanguard => VanguardRow,
            RowType.Building => BuildingRow,
            RowType.Human => HumanRow,
            RowType.Town => new[] { TownSlot },
            _ => null
        };
    }

    [Serializable]
    public class BoardSlot : IBoardSlot
    {
        public int Index;
        public RowType AllowedRow;
        public BoardCard Occupant;
        public bool IsEmpty => Occupant == null;

        RowType IBoardSlot.AllowedRow => AllowedRow;
        IBoardCard IBoardSlot.Occupant => Occupant;
        int IBoardSlot.Index => Index;
    }
}
