using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Shared.Localization;

namespace Exploration
{
    public class LockboxPuzzle : MonoBehaviour, IInteractable
    {
        [SerializeField] private string _correctCode = "MSPVHA";
        [SerializeField] private GameObject _puzzleUI;
        [SerializeField] private GameObject _lockedVisual;
        [SerializeField] private GameObject _unlockedVisual;

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
            if (_puzzleUI)
            {
                var txt = _puzzleUI.GetComponentInChildren<Text>();
                if (txt)
                    txt.text = new string('*', _currentIndex) + new string('_', _correctCode.Length - _currentIndex);
            }
        }

        private void Unlock()
        {
            _solved = true;
            _isActive = false;
            Keyboard.current.onTextInput -= OnTextInput;

            if (_lockedVisual) _lockedVisual.SetActive(false);
            if (_unlockedVisual) _unlockedVisual.SetActive(true);
            if (_puzzleUI) _puzzleUI.SetActive(false);

            _player.Activate();
        }

        private void CancelPuzzle()
        {
            _isActive = false;
            Keyboard.current.onTextInput -= OnTextInput;
            _currentIndex = 0;
            if (_puzzleUI) _puzzleUI.SetActive(false);
            _player.Activate();
        }

        private void OnDisable()
        {
            if (_isActive)
            {
                Keyboard.current.onTextInput -= OnTextInput;
                _isActive = false;
            }
        }
    }
}