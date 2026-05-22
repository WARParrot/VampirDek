using System;
using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [Serializable]
    [CreateAssetMenu(menuName = "Deck Data")]
    public class DeckData : ScriptableObject
    {
        [NonSerialized] public List<CardDef> Cards = new();
        public List<string> CardNames = new();
    }
}