using UnityEngine;
using Exploration.Inventory;

namespace Exploration
{
    /// <summary>
    /// Wired by the lockbox setup tool: when the rotary puzzle is solved, drop the antidote
    /// potion into the player's inventory. Counterpart to <see cref="LidLinkOnUnlocked"/>.
    /// </summary>
    public class GivePotionOnUnlocked : MonoBehaviour
    {
        [SerializeField] private RotaryLockPuzzle _puzzle;
        [SerializeField] private ItemDef _potion;

        private bool _given;

        private void Awake()
        {
            if (_puzzle == null || _potion == null) { enabled = false; return; }
            _puzzle.OnUnlocked.AddListener(HandleUnlocked);
        }

        private void OnDestroy()
        {
            if (_puzzle != null) _puzzle.OnUnlocked.RemoveListener(HandleUnlocked);
        }

        private void HandleUnlocked()
        {
            if (_given) return;
            var inv = Inventory.Inventory.Current;
            if (inv == null) { Debug.LogWarning("[GivePotionOnUnlocked] No Inventory in scene."); return; }
            if (!inv.Add(_potion)) { Debug.LogWarning("[GivePotionOnUnlocked] Inventory rejected potion."); return; }
            _given = true;
        }
    }
}
