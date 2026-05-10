using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Deck Data")]
    public class DeckData : ScriptableObject
    {
        public List<CardDef> Cards;
    }
}