using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Board Layout")]
    public class BoardLayoutData : ScriptableObject
    {
        public List<SlotDefinition> Slots;
        public int VanguardSlotsCount;
        public int BuildingSlotsCount;
        public int HumanSlotsCount;
    }

    [System.Serializable]
    public class SlotDefinition
    {
        public SlotType Type;
        public int index;
    }

    public enum SlotType { Vanguard, Building, Human, Town }
}
