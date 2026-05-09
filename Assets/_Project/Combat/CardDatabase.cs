using System.Collections.Generic;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Combat
{
    public static class CardDatabase
    {
        private static Dictionary<string, CardDef> _cache = new();

        [RuntimeInitializeOnLoadMethod]
        public static void Warmup()
        {
            var handle = Addressables.LoadAssetsAsync<CardDef>("Cards", null);
            handle.Completed += result =>
            {
                foreach (var card in result.Result)
                    _cache[card.CardName] = card;
            };
        }

        public static CardDef GetCard(string cardName)
        {
            if (_cache.TryGetValue(cardName, out var card))
                return card;
            var h = Addressables.LoadAssetAsync<CardDef>(cardName);
            h.WaitForCompletion();
            return h.Result;
        }
    }
}