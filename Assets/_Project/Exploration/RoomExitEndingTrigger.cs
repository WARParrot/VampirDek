using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// Attach to the room's exit door. When the BlockedDoor unlocks (key used), show the
    /// branched ending screen. Final outcome depends on <see cref="EscapeQuestState.PotionConsumed"/>.
    /// </summary>
    [RequireComponent(typeof(BlockedDoor))]
    public class RoomExitEndingTrigger : MonoBehaviour
    {
        private BlockedDoor _door;

        private void Awake()
        {
            _door = GetComponent<BlockedDoor>();
            _door.OnUnlocked.AddListener(HandleUnlocked);
        }

        private void OnDestroy()
        {
            if (_door != null) _door.OnUnlocked.RemoveListener(HandleUnlocked);
        }

        private void HandleUnlocked() => EscapeRoomEndingUI.ShowEnding();
    }
}
