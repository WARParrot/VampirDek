// Definitions/IPlayerSide.cs
using System.Collections.Generic;

namespace Definitions
{

    public interface IPlayerSide
    {
        int Mana { get; }
        int HumanResources { get; set; }
        IBoard Board { get; }
        IGameEntity Town { get; }
        IReadOnlyList<ICard> Hand { get; }
        IReadOnlyList<ICard> Deck { get; }
        IReadOnlyList<ICard> Graveyard { get; }

        void PayMana(int amount);
        void PayHumanResources(int amount);
        void DrawCards(int count);
        void DiscardRandomCards(int count);
    }
}