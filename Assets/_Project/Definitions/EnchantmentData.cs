using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Enchantment")]
    public class EnchantmentData : ScriptableObject
    {
        public string DisplayName;
        public int Duration = -1;
        public List<TriggerEntry> Triggers;
        public List<ModifierEntry> Modifiers;
    }

    [System.Serializable]
    public class TriggerEntry
    {
        public string EventType;
        public List<ActionDefinition> Actions;
    }

    [System.Serializable]
    public class ModifierEntry
    {
        public string Stat;
        public int Value;
        public ModifierType Type;
    }

    public enum ModifierType { Add, Multiply }
}
