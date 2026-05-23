using System.Collections.Generic;
using Definitions;

namespace Shared
{
    public static class DeckManager
    {
        public static List<CardDef> CurrentDeck { get; set; } = new();
    }
}