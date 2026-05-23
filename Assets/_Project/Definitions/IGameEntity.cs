using System.Collections.Generic;

namespace Definitions
{
    public interface IGameEntity
    {
        int Id { get; }
        int Health { get; }
        int MaxHealth { get; }
        int Attack { get; }
        List<RuntimeEnchantment> Enchantments { get; }
        bool IsAlive { get; }

        void TakeDamage(int amount, IGameEntity source);
        void Heal(int amount);
        void ModifyAttack(int delta);
        void ModifyMaxHealth(int delta);
        void AddEnchantment(RuntimeEnchantment enchantment);
        void RemoveEnchantment(RuntimeEnchantment enchantment);
    }
}
