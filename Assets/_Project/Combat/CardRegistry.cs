using System.Collections.Generic;
using Definitions;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Card Registry")]
    public class CardRegistry : ScriptableObject
    {
        public List<CardDef> Cards = new();
    }

    public static class CardRegistryLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAllCards()
        {
            var reg = Resources.Load<CardRegistry>("CardRegistry");
            if (reg == null)
            {
                Debug.LogWarning("[CardRegistry] Resources/CardRegistry.asset not found. New generated cards won't load. Run Tools/VampirDek11/Generate Vampire Content Pass.");
                return;
            }
            int n = 0;
            foreach (var card in reg.Cards)
            {
                if (card == null) continue;
                CardDatabase.RegisterCard(card);
                n++;
            }
            Debug.Log($"[CardRegistry] Registered {n} cards from Resources/CardRegistry.");
        }
    }
}
