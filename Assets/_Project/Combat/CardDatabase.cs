using System.Collections.Generic;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

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

        public static void RegisterCard(CardDef card)
        {
            if (!string.IsNullOrEmpty(card?.CardName))
                _cache[card.CardName] = card;
        }

        public static CardDef GetCard(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;
            if (_cache.TryGetValue(cardName, out var card))
                return card;
            try
            {
                var h = Addressables.LoadAssetAsync<CardDef>(cardName);
                h.WaitForCompletion();
                if (h.Result != null) _cache[cardName] = h.Result;
                return h.Result;
            }
            catch
            {
                Debug.LogWarning($"[CardDatabase] Card '{cardName}' not found. Returning null.");
                return null;
            }
        }

        public static async UniTask<CardDef> GetCardAsync(string cardName)
        {
            if (string.IsNullOrEmpty(cardName)) return null;
            if (_cache.TryGetValue(cardName, out var card))
                return card;
            try
            {
                var handle = Addressables.LoadAssetAsync<CardDef>(cardName);
                await handle.Task;
                if (handle.Result != null) _cache[cardName] = handle.Result;
                return handle.Result;
            }
            catch
            {
                Debug.LogWarning($"[CardDatabase] Card '{cardName}' not found in cache or Addressables. Returning null.");
                return null;
            }
        }
    }
}