using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class CardDatabase
    {
        private static readonly Dictionary<string, CardDef> _cache = new();

        public static void RegisterCard(CardDef card)
        {
            if (card != null && !string.IsNullOrEmpty(card.CardName))
                _cache[card.CardName] = card;
        }

        public static CardDef GetCard(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;
            _cache.TryGetValue(cardName, out var card);
            return card;
        }
    }
}
