using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// Glue component placed by the editor setup tool. At runtime it subscribes to the
    /// puzzle's <see cref="RotaryLockPuzzle.OnUnlocked"/> event and unlocks + opens the lid.
    /// Kept as a separate MonoBehaviour because authoring UnityEvent persistent calls via
    /// SerializedProperty is fragile.
    /// </summary>
    public class LidLinkOnUnlocked : MonoBehaviour
    {
        [SerializeField] private RotaryLockPuzzle _puzzle;
        [SerializeField] private OpenableObject _openable;

        private void Awake()
        {
            if (_puzzle == null || _openable == null) { enabled = false; return; }
            _puzzle.OnUnlocked.AddListener(HandleUnlocked);
        }

        private void OnDestroy()
        {
            if (_puzzle != null) _puzzle.OnUnlocked.RemoveListener(HandleUnlocked);
        }

        private void HandleUnlocked()
        {
            _openable.SetLocked(false);
            _openable.Open();
        }
    }
}
