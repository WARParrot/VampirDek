using System.Collections.Generic;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Combat
{
    public static class EnchantmentDatabase
    {
        private static Dictionary<string, EnchantmentData> _cache = new();

        [RuntimeInitializeOnLoadMethod]
        public static void Warmup()
        {
            var handle = Addressables.LoadAssetsAsync<EnchantmentData>("Enchantments", null);
            handle.Completed += result =>
            {
                foreach (var ench in result.Result)
                    _cache[ench.DisplayName] = ench;
            };
        }

        public static EnchantmentData Get(string displayName)
        {
            if (_cache.TryGetValue(displayName, out var data))
                return data;
            var handle = Addressables.LoadAssetAsync<EnchantmentData>(displayName);
            handle.WaitForCompletion();
            return handle.Result;
        }
    }
}