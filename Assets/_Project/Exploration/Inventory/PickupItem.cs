using UnityEngine;
using Shared.Localization;

namespace Exploration.Inventory
{
    /// <summary>
    /// World object you can pick up. Drops itself into the player's Inventory on interact.
    /// If the inventory is full, surfaces a "no room" prompt and stays in the world (RE2 behavior).
    /// </summary>
    public class PickupItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDef _item;
        [SerializeField, Min(1)] private int _count = 1;
        [SerializeField] private string _promptKey = "interaction.take";
        [SerializeField] private string _promptFallback = "Take";
        [SerializeField] private string _fullKey = "inventory.full";
        [SerializeField] private string _fullFallback = "No room in inventory.";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        public string PromptText
        {
            get
            {
                if (_item == null) return string.Empty;
                var nameText = LocalizationService.T(_item.DisplayNameKey, _item.DisplayNameFallback);
                var verb = LocalizationService.T(_promptKey, _promptFallback);
                return $"{verb} {nameText}";
            }
        }

        public void Interact(ExplorationController player)
        {
            if (_item == null) return;
            var inv = Inventory.Current;
            if (inv == null) { Debug.LogWarning("[PickupItem] No Inventory in scene."); return; }

            if (!inv.CanAdd(_item, _count))
            {
                var msg = LocalizationService.T(_fullKey, _fullFallback);
                if (_messageOverridePrompt != null) _messageOverridePrompt.Show(msg);
                else Debug.Log($"[PickupItem] {msg}");
                return;
            }

            inv.Add(_item, _count);
            gameObject.SetActive(false);
        }
    }
}
