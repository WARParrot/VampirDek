using UnityEngine;
using Definitions;

namespace Combat
{
    public static class EnchantmentFactory
    {
        public static RuntimeEnchantment Create(EnchantmentData data, IGameEntity owner)
        {
            return new RuntimeEnchantment(data, owner);
        }
    }
}
