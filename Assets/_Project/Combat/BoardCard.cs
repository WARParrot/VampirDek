using System;
using System.Collections.Generic;
using Core;
using Definitions;

namespace Combat
{
    public class BoardCard : IGameEntity, IBoardCard
    {
        private static int _nextId = 1000;
        public int Id { get; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Attack { get; private set; }
        public int CurrentSpeed { get; set; }
        public List<RuntimeEnchantment> Enchantments { get; } = new();
        public bool IsAlive => Health > 0;
        public CardDef SourceCard { get; }
        public bool IsTown { get; set; } = false;
        public Definitions.RowType TypeOfRow { get; private set; }

        public IGameEntity PlannedTarget { get; set; }

        public int DamageReceivedThisTurn { get; set; }

        public BoardCard(CardDef sourceCard)
        {
            Id = _nextId++;
            SourceCard = sourceCard;
            Health = sourceCard.Health;
            MaxHealth = sourceCard.Health;
            Attack = sourceCard.Attack;
        }

        public void SetHealth(int current, int max)
        {
            Health = current;
            MaxHealth = max;
        }

        public void SetAttack(int value) => Attack = value;

        public void TakeDamage(int amount, IGameEntity source)
        {
            Health -= amount;
            DamageReceivedThisTurn += amount;
            if (!IsAlive)
                GlobalServices.EventBus.Publish(new EntityDiedEvent(this));
        }

        public void Heal(int amount) => Health = Math.Min(Health + amount, MaxHealth);
        public void ModifyAttack(int delta) => Attack += delta;
        public void ModifyMaxHealth(int delta)
        {
            MaxHealth += delta;
            Health = Math.Min(Health, MaxHealth);
        }

        public void AddEnchantment(RuntimeEnchantment e) => Enchantments.Add(e);
        public void RemoveEnchantment(RuntimeEnchantment e) => Enchantments.Remove(e);

        public void ApplyInnateEnchantments()
        {
            if (SourceCard.InnateEnchantments == null) return;
            foreach (var data in SourceCard.InnateEnchantments)
            {
                var runtime = EnchantmentFactory.Create(data, this);
                runtime.OnAttach();
                Enchantments.Add(runtime);
            }
        }
    }
}
