using UnityEngine;

namespace Exploration.Inventory
{
    [CreateAssetMenu(menuName = "VampirDek/Inventory/Item", fileName = "Item")]
    public class ItemDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable string id used for save/load and equality. Keep unique across all items.")]
        public string Id;

        [Header("Display")]
        public Sprite Icon;
        public string DisplayNameKey = "item.name";
        public string DisplayNameFallback = "Item";
        public string DescriptionKey = "item.desc";
        public string DescriptionFallback = "";

        [Header("Examine")]
        [TextArea(3, 8)]
        public string ExamineTextFallback = "";
        public string ExamineTextKey = "item.examine";

        [Header("Inventory rules")]
        [Tooltip("Key items live in their own pocket, cannot be discarded, and always stack to 1.")]
        public bool IsKeyItem;

        [Tooltip("Slots this item occupies in the main pocket. Most items = 1. Big items = 2.")]
        [Min(1)] public int SlotSize = 1;

        public bool IsStackable;
        [Min(1)] public int MaxStack = 1;

        [Header("Examine 3D (optional)")]
        [Tooltip("Prefab spawned in the examine view. If set, examine shows a rotatable 3D model instead of just the icon.")]
        public GameObject ExaminePrefab;
        [Tooltip("Distance from examine camera to model.")]
        public float ExamineCameraDistance = 0.4f;
        [Tooltip("Initial euler shown when examine opens.")]
        public Vector3 ExamineStartEuler = Vector3.zero;

        [Header("Use behavior")]
        [Tooltip("Consumed when used on a matching world target (e.g. key turning in lock).")]
        public bool ConsumeOnUse = true;
    }
}
