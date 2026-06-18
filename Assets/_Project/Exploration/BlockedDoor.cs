using UnityEngine;
using UnityEngine.Events;
using Shared.Localization;
using Exploration.Inventory;

namespace Exploration
{
    /// <summary>
    /// A door / passage the player should not be able to use yet.
    /// Place this on a collider in front of the blocked area, optionally with an
    /// invisible wall so the player physically cannot pass. The Interact path
    /// surfaces a localized "you can't go this way yet" prompt instead of leaving
    /// the player walking off-map.
    ///
    /// Optionally accepts a key item: when assigned, possessing the item unlocks the
    /// door instead of showing the generic message — RE2-style "use Spade Key on lock".
    /// </summary>
    public class BlockedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _promptKey = "blocked_door.prompt";
        [SerializeField] private string _promptFallback = "Сюда пока нельзя.";
        [SerializeField] private string _messageKey = "blocked_door.message";
        [SerializeField] private string _messageFallback = "Эта дверь закрыта. Сначала разберитесь с делами в этой комнате.";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        [Header("Key requirement (optional)")]
        [SerializeField] private ItemDef _requiredKeyItem;
        [SerializeField] private bool _consumeKey;
        [SerializeField] private string _unlockMessageKey = "blocked_door.unlock";
        [SerializeField] private string _unlockMessageFallback = "Дверь открыта.";
        public UnityEvent OnUnlocked;

        private bool _unlocked;

        public string PromptText
        {
            get
            {
                if (_unlocked) return string.Empty;
                if (_requiredKeyItem != null)
                {
                    var name = LocalizationService.T(_requiredKeyItem.DisplayNameKey, _requiredKeyItem.DisplayNameFallback);
                    var verb = LocalizationService.T("interaction.use", "Use");
                    return $"{verb} {name}";
                }
                return LocalizationService.T(_promptKey, _promptFallback);
            }
        }

        public void Interact(ExplorationController player)
        {
            if (_unlocked) return;

            if (_requiredKeyItem != null)
            {
                var inv = Inventory.Inventory.Current;
                if (inv != null && inv.Has(_requiredKeyItem))
                {
                    if (_consumeKey) inv.Remove(_requiredKeyItem);
                    _unlocked = true;
                    var ok = LocalizationService.T(_unlockMessageKey, _unlockMessageFallback);
                    if (_messageOverridePrompt != null) _messageOverridePrompt.Show(ok);
                    OnUnlocked?.Invoke();
                    return;
                }
            }

            var message = LocalizationService.T(_messageKey, _messageFallback);
            if (_messageOverridePrompt != null)
                _messageOverridePrompt.Show(message);
            else
                Debug.Log($"[BlockedDoor] {message}");
        }

        public bool IsUnlocked => _unlocked;
    }
}
