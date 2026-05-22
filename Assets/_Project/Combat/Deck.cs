using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;

namespace Combat
{
    public class Deck : IEnumerable<Card>
    {
        private readonly IPlayerSide _owner;
        private readonly List<Card> _cards;
        private int _drawIndex;

        public int Count => _cards.Count;
        public int RemainingCards => _cards.Count - _drawIndex;
        public bool IsEmpty => _drawIndex >= _cards.Count;

        public IReadOnlyList<Card> Cards => _cards.AsReadOnly();

        public Deck(IPlayerSide owner, IEnumerable<Card> cards)
        {
            _owner = owner;
            _cards = cards.ToList();
            Shuffle();
        }

        public void Shuffle()
        {
            var rng = new System.Random();
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
            _drawIndex = 0;
        }

        public Card Draw()
        {
            if (IsEmpty) return null;
            var card = _cards[_drawIndex++];
            GlobalServices.EventBus.Publish(new DeckCountChangedEvent(_owner, RemainingCards));
            if (IsEmpty)
                GlobalServices.EventBus.Publish(new EmptyDeckEvent(_owner));
            return card;
        }

        public void Remove(Card card)
        {
            _cards.Remove(card);
            Shuffle();
        }
        public void AddCard(Card card)
        {
            _cards.Add(card);
        }
        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
        public void Clear() => _cards.Clear();

        public IEnumerator<Card> GetEnumerator() => _cards.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}