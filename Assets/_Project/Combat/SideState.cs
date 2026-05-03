using System;
using System.Collections.Generic;
using System.Linq;
using Definitions;

namespace Combat
{
    public class SideState : IPlayerSide
    {
        public Board Board { get; private set; }
        public int Mana { get; set; }
        public int MaxMana { get; set; }
        public int HumanResources { get; set; }
        public List<Card> Deck { get; set; } = new();
        public List<Card> Hand { get; } = new();
        public List<Card> Graveyard { get; } = new();
        public BoardCard Town => Board.TownSlot?.Occupant;

        IBoard IPlayerSide.Board => (IBoard)Board;
        IGameEntity IPlayerSide.Town => Board.TownSlot?.Occupant;
        IReadOnlyList<ICard> IPlayerSide.Hand => Hand.AsReadOnly();
        IReadOnlyList<ICard> IPlayerSide.Deck => Deck.AsReadOnly();
        IReadOnlyList<ICard> IPlayerSide.Graveyard => Graveyard.AsReadOnly();

        public SideState(BoardLayoutData layout)
        {
            Board = new Board(layout);
            Mana = 1;
            MaxMana = 1;
            HumanResources = 0;
        }

        public void PayMana(int amount) => Mana -= amount;
        public void PayHumanResources(int amount) => HumanResources -= amount;
        public void DrawCards(int count) { for (int i = 0; i < count; i++) DrawCard(); }
        private void DrawCard()
        {
            if (Deck.Count == 0 && Graveyard.Count > 0) { Deck.AddRange(Graveyard); Graveyard.Clear(); Shuffle(Deck); }
            if (Deck.Count == 0) return;
            var card = Deck[0]; Deck.RemoveAt(0); Hand.Add(card);
        }
        public void DiscardRandomCards(int count)
        {
            var hand = new List<Card>(Hand);
            var rng = new Random();
            for (int i = 0; i < count && hand.Count > 0; i++)
            {
                int idx = rng.Next(hand.Count);
                var card = hand[idx];
                hand.RemoveAt(idx);
                Hand.Remove(card);
                Graveyard.Add(card);
            }
        }
        private void Shuffle<T>(IList<T> list) { var rng = new Random(); for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }

        public void InitializeTown()
        {
            var townCard = Deck.FirstOrDefault(c => c.Def.Type == CardType.Town);
            if (townCard == null) throw new InvalidOperationException("Deck must contain exactly one Town card.");
            Deck.Remove(townCard);
            Board.PlaceTownCard(townCard.Def);
        }
    }
}
