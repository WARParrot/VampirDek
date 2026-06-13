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
        public int DrawIndex => _drawIndex;
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

        /// <summary>
        /// Removes a specific card from the undrawn portion of the deck without re-shuffling.
        /// Used by the draft flow to consume the card the player picked while leaving the rest
        /// of the deck order — and previously drawn cards — untouched.
        /// </summary>
        public bool TakeSpecificUndrawn(Card card)
        {
            if (card == null) return false;
            for (int i = _drawIndex; i < _cards.Count; i++)
            {
                if (!ReferenceEquals(_cards[i], card)) continue;
                _cards.RemoveAt(i);
                GlobalServices.EventBus?.Publish(new DeckCountChangedEvent(_owner, RemainingCards));
                if (IsEmpty)
                    GlobalServices.EventBus?.Publish(new EmptyDeckEvent(_owner));
                return true;
            }
            return false;
        }

        public IEnumerable<Card> UndrawnCards
        {
            get
            {
                for (int i = _drawIndex; i < _cards.Count; i++)
                    yield return _cards[i];
            }
        }
        public void AddCard(Card card)
        {
            _cards.Add(card);
        }
        public void AddRange(IEnumerable<Card> cards) => _cards.AddRange(cards);
        public void Clear()
        {
            _cards.Clear();
            _drawIndex = 0;
        }

        public void RestoreCards(IEnumerable<Card> cards, int drawIndex)
        {
            _cards.Clear();
            _cards.AddRange(cards);
            _drawIndex = drawIndex < 0 ? 0 : drawIndex > _cards.Count ? _cards.Count : drawIndex;
            GlobalServices.EventBus?.Publish(new DeckCountChangedEvent(_owner, RemainingCards));
        }

        public IEnumerator<Card> GetEnumerator() => _cards.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}