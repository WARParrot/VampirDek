using UnityEngine;
using UnityEngine.Events;
using Shared.Localization;

namespace Exploration.Inventory
{
    /// <summary>
    /// World object that accepts a specific inventory item ("insert medallion", "use key on lock").
    /// RE2 flow: interact prompts auto-uses required item if present, otherwise tells you what you need.
    /// Subscribers can react via <see cref="OnUsed"/> (open a door, reveal a passage, start a puzzle...).
    /// </summary>
    public class ItemUseTarget : MonoBehaviour, IInteractable
    {
        [Header("Requirement")]
        [SerializeField] private ItemDef _requiredItem;
        [SerializeField] private bool _consumeItem = true;

        [Header("Prompts")]
        [SerializeField] private string _usePromptKey = "interaction.use";
        [SerializeField] private string _usePromptFallback = "Use";
        [SerializeField] private string _missingKey = "interaction.missing_item";
        [SerializeField] private string _missingFallback = "You need: {0}";
        [SerializeField] private string _alreadyUsedKey = "interaction.already_used";
        [SerializeField] private string _alreadyUsedFallback = "";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        [Header("Events")]
        public UnityEvent OnUsed;

        private bool _consumed;

        public string PromptText
        {
            get
            {
                if (_consumed) return LocalizationService.T(_alreadyUsedKey, _alreadyUsedFallback);
                if (_requiredItem == null) return LocalizationService.T(_usePromptKey, _usePromptFallback);
                var itemName = LocalizationService.T(_requiredItem.DisplayNameKey, _requiredItem.DisplayNameFallback);
                var verb = LocalizationService.T(_usePromptKey, _usePromptFallback);
                return $"{verb} {itemName}";
            }
        }

        public bool IsConsumed => _consumed;
        public ItemDef RequiredItem => _requiredItem;

        public void Interact(ExplorationController player)
        {
            if (_consumed) return;

            // If the player picked a specific item via inventory "Use", honor it.
            if (InventoryUI.PendingUseItem != null)
            {
                bool ok = TryUseExternal(InventoryUI.PendingUseItem);
                InventoryUI.ClearPendingUseItem();
                if (ok) return;
            }

            var inv = Inventory.Current;
            bool hasItem = _requiredItem == null || (inv != null && inv.Has(_requiredItem));

            if (!hasItem)
            {
                var itemName = LocalizationService.T(_requiredItem.DisplayNameKey, _requiredItem.DisplayNameFallback);
                var fmt = LocalizationService.T(_missingKey, _missingFallback);
                var msg = string.Format(fmt, itemName);
                if (_messageOverridePrompt != null) _messageOverridePrompt.Show(msg);
                else Debug.Log($"[ItemUseTarget] {msg}");
                return;
            }

            if (_requiredItem != null && _consumeItem) inv.Remove(_requiredItem);
            _consumed = true;
            OnUsed?.Invoke();
        }

        /// <summary>
        /// Force-trigger from external code (e.g. inventory UI "Use → click world" flow).
        /// Returns true if accepted.
        /// </summary>
        public bool TryUseExternal(ItemDef item)
        {
            if (_consumed) return false;
            if (_requiredItem != null && item != _requiredItem) return false;

            var inv = Inventory.Current;
            if (_requiredItem != null && _consumeItem && inv != null) inv.Remove(_requiredItem);
            _consumed = true;
            OnUsed?.Invoke();
            return true;
        }
    }
}
