using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Shared.Localization;
using Core;

namespace Exploration
{
    public class LockboxPuzzle : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _correctCode = "MSPVHA";
        [SerializeField] private GameObject _puzzleUI;
        [SerializeField] private GameObject _lockedVisual;
        [SerializeField] private GameObject _unlockedVisual;
        [SerializeField] private Text _instructionText;

        private bool _solved;
        private int _currentIndex;
        private ExplorationController _player;
        private bool _isActive;

        public string PromptText => _solved ? string.Empty : LocalizationService.T("interaction.hack", "Hack");

        private void Awake()
        {
            if (_lockedVisual) _lockedVisual.SetActive(true);
            if (_unlockedVisual) _unlockedVisual.SetActive(false);
            if (_puzzleUI) _puzzleUI.SetActive(false);
        }

        private void Update()
        {
            if (_isActive && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPuzzle();
            }
        }

        public void Interact(ExplorationController player)
        {
            if (_solved) return;
            _player = player;
            _player.Deactivate();
            _isActive = true;
            _currentIndex = 0;
            if (_puzzleUI) _puzzleUI.SetActive(true);

            // Free the cursor while the puzzle UI is up so the player can see they are in a modal,
            // and the Esc-to-cancel behaviour reads as intentional rather than a lockup.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UpdateUI();

            if (Keyboard.current != null)
                Keyboard.current.onTextInput += OnTextInput;
        }

        private void OnTextInput(char c)
        {
            if (!_isActive) return;

            char upper = char.ToUpper(c);
            if (upper < 'A' || upper > 'Z') return;

            if (upper == _correctCode[_currentIndex])
            {
                _currentIndex++;
                UpdateUI();

                if (_currentIndex >= _correctCode.Length)
                {
                    Unlock();
                }
            }
            else
            {
                _currentIndex = 0;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (!_puzzleUI) return;

            // Player feedback: the puzzle felt like a lockup because there was no on-screen
            // explanation of what to do. Show a clear prompt and the running input string.
            var entered = new string('*', _currentIndex) + new string('_', _correctCode.Length - _currentIndex);
            var prompt = LocalizationService.T(
                "lockbox.prompt",
                $"Введите код из {_correctCode.Length} букв (A-Z). Esc — отмена.");

            var label = _instructionText;
            if (label == null)
                label = _puzzleUI.GetComponentInChildren<Text>();

            if (label != null)
                label.text = $"{prompt}\n\n<size=24>{entered}</size>";
        }

        private void Unlock()
        {
            _solved = true;
            ClosePuzzle();

            if (_lockedVisual) _lockedVisual.SetActive(false);
            if (_unlockedVisual) _unlockedVisual.SetActive(true);
        }

        private void CancelPuzzle()
        {
            _currentIndex = 0;
            ClosePuzzle();
        }

        private void ClosePuzzle()
        {
            _isActive = false;
            if (Keyboard.current != null)
                Keyboard.current.onTextInput -= OnTextInput;
            if (_puzzleUI) _puzzleUI.SetActive(false);

            // Always re-grab the player's cursor and re-enable control on close so a stuck
            // puzzle UI (or a scene unload mid-puzzle) cannot leave input dead.
            if (_player != null) _player.Activate();
        }

        private void OnDisable()
        {
            if (_isActive)
            {
                ClosePuzzle();
            }
        }
    }
}