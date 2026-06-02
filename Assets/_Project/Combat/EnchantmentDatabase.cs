using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class EnchantmentDatabase
    {
        private static readonly Dictionary<string, EnchantmentData> _cache = new();

        public static void RegisterEnchantment(EnchantmentData ench)
        {
            if (ench != null && !string.IsNullOrEmpty(ench.DisplayName))
                _cache[ench.DisplayName] = ench;
        }

        public static EnchantmentData GetEnchantment(string displayName)
        {
            _cache.TryGetValue(displayName, out var ench);
            return ench;
        }
    }
}