using System.Collections.Generic;

namespace Definitions
{
    public interface IBoard
    {
        IBoard OwnerBoard { get; }
        IBoardSlot TownSlot { get; }
        IBoardSlot[] VanguardRow { get; }
        IBoardSlot[] BuildingRow { get; }
        IBoardSlot[] HumanRow { get; }

        bool TryPlaceCard(CardDef cardDef, out IBoardSlot placedSlot);
        void PlaceTownCard(CardDef townDef);
        void RemoveCard(IBoardCard card);
        IBoardCard GetFirstAliveCardInRow(RowType rowType);
    }

    public interface IBoardSlot
    {
        int Index { get; }
        RowType AllowedRow { get; }
        IBoardCard Occupant { get; }
        bool IsEmpty { get; }
    }

}
