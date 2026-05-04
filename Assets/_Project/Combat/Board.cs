using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;
using UnityEngine;

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
                var slot = new BoardSlot { AllowedRow = (Definitions.RowType)def.Type, Index = layout.Slots.IndexOf(def) };
                switch (def.Type)
                {
                    case Definitions.SlotType.Vanguard: vanguard.Add(slot); break;
                    case Definitions.SlotType.Building: building.Add(slot); break;
                    case Definitions.SlotType.Human:    humanList.Add(slot); break;
                    case Definitions.SlotType.Town:     TownSlot = slot; break;
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

        public bool TryPlaceCard(CardDef cardDef, out IBoardSlot placedSlot)
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
            GlobalServices.EventBus.Publish(new PlacedCardEvent(card, this));
            return true;
        }

        public bool TryPlaceCardIntoSlot(CardDef cardDef, BoardSlot targetSlot)
        {
            Debug.Log($"[Board] TryPlaceCardIntoSlot: {cardDef.CardName}, slot {targetSlot.AllowedRow}[{targetSlot.Index}]");
            if (targetSlot == null)
            {
                Debug.Log("[Board] Slot is null");
                return false;
            }
            if (!targetSlot.IsEmpty)
            {
                Debug.Log("[Board] Slot not empty");
                return false;
            }
            if (cardDef.RowType != targetSlot.AllowedRow)
            {
                Debug.Log("[Board] RowType mismatch");
                return false;
            }

            var card = new BoardCard(cardDef);
            card.ApplyInnateEnchantments();
            targetSlot.Occupant = card;
            GlobalServices.EventBus.Publish(new PlacedCardEvent(card, this));
            Debug.Log("[Board] Card placed successfully");
            return true;
        }

        public void PlaceTownCard(CardDef townDef)
        {
            if (TownSlot == null)
                throw new System.InvalidOperationException(
                    "Board layout does not contain a Town slot. " +
                    "Add a slot with Type = Town to the BoardLayoutData asset.");

            if (!TownSlot.IsEmpty)
                throw new System.InvalidOperationException("Town slot is already occupied.");

            var town = new BoardCard(townDef) { IsTown = true };
            town.ApplyInnateEnchantments();
            TownSlot.Occupant = town;
            GlobalServices.EventBus.Publish(new TownPlacedEvent(town, this));
        }

        public void RemoveCard(IBoardCard card)
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

        public IEnumerable<BoardSlot> AllSlots()
        {
            foreach (var s in VanguardRow) yield return s;
            foreach (var s in BuildingRow) yield return s;
            foreach (var s in HumanRow) yield return s;
            yield return TownSlot;
        }

        private BoardSlot[] GetRow(Definitions.RowType rowType) => rowType switch
        {
            Definitions.RowType.Vanguard => VanguardRow,
            Definitions.RowType.Building => BuildingRow,
            Definitions.RowType.Human => HumanRow,
            Definitions.RowType.Town => new[] { TownSlot },
        };

        public BoardSlot GetSlot(Definitions.RowType row, int index)
        {
            switch (row)
            {
                case Definitions.RowType.Vanguard: return index < VanguardRow.Length ? VanguardRow[index] : null;
                case Definitions.RowType.Building: return index < BuildingRow.Length ? BuildingRow[index] : null;
                case Definitions.RowType.Human:    return index < HumanRow.Length ? HumanRow[index] : null;
                case Definitions.RowType.Town:     return TownSlot;
            }
            return null;
        }
    }

    [Serializable]
    public class BoardSlot : IBoardSlot
    {
        public int Index;
        public Definitions.RowType AllowedRow;
        public BoardCard Occupant;
        public bool IsEmpty => Occupant == null;

        Definitions.RowType IBoardSlot.AllowedRow => AllowedRow;
        IBoardCard IBoardSlot.Occupant => Occupant;
        int IBoardSlot.Index => Index;
    }
}
