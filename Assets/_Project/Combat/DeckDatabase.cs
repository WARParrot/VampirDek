using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class DeckDatabase
    {
        private static readonly Dictionary<string, DeckData> _decks = new();

        public static void RegisterDeck(DeckData deck)
        {
            if (deck != null && !string.IsNullOrEmpty(deck.name))
                _decks[deck.name] = deck;
        }

        public static DeckData GetDeck(string deckName)
        {
            if (string.IsNullOrEmpty(deckName)) return null;
            _decks.TryGetValue(deckName, out var deck);
            return deck;
        }
    }
}