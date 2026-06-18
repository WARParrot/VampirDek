using UnityEngine;

namespace Exploration.Inventory
{
    [CreateAssetMenu(menuName = "VampirDek/Inventory/Combine Recipe", fileName = "Recipe")]
    public class CombineRecipe : ScriptableObject
    {
        public ItemDef InputA;
        public ItemDef InputB;
        public ItemDef Output;

        [Tooltip("If true, both inputs are consumed. If false, only InputB is consumed (InputA stays — e.g. tool with limited charges).")]
        public bool ConsumeBoth = true;

        public bool Matches(ItemDef a, ItemDef b)
        {
            if (InputA == null || InputB == null) return false;
            return (a == InputA && b == InputB) || (a == InputB && b == InputA);
        }
    }
}
