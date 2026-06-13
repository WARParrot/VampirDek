using UnityEngine;
using Shared.Localization;

namespace Exploration
{
    /// <summary>
    /// A door / passage the player should not be able to use yet.
    /// Place this on a collider in front of the blocked area, optionally with an
    /// invisible wall so the player physically cannot pass. The Interact path
    /// surfaces a localized "you can't go this way yet" prompt instead of leaving
    /// the player walking off-map.
    /// </summary>
    public class BlockedDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _promptKey = "blocked_door.prompt";
        [SerializeField] private string _promptFallback = "Сюда пока нельзя.";
        [SerializeField] private string _messageKey = "blocked_door.message";
        [SerializeField] private string _messageFallback = "Эта дверь закрыта. Сначала разберитесь с делами в этой комнате.";
        [SerializeField] private InteractionPromptUI _messageOverridePrompt;

        public string PromptText => LocalizationService.T(_promptKey, _promptFallback);

        public void Interact(ExplorationController player)
        {
            var message = LocalizationService.T(_messageKey, _messageFallback);
            if (_messageOverridePrompt != null)
            {
                _messageOverridePrompt.Show(message);
            }
            else
            {
                Debug.Log($"[BlockedDoor] {message}");
            }
        }
    }
}
