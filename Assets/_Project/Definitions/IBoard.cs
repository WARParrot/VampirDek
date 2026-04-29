using System.Collections.Generic;

namespace Definitions
{
    public interface IBoard
    {
        IBoardSlot TownSlot { get; }
        IBoardSlot[] VanguardRow { get; }
        IBoardSlot[] BuildingRow { get; }
        IBoardSlot[] HumanRow { get; }

        bool TryPlaceCard(CardDef cardDef);
        void PlaceTownCard(CardDef townDef);
        void RemoveCard(IBoardCard card);
    }

    public interface IBoardSlot
    {
        int Index { get; }
        RowType AllowedRow { get; }
        IBoardCard Occupant { get; }
        bool IsEmpty { get; }
    }
}
