using System.Linq;
using Combat;
using Core;
using Definitions;
using Shared;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Combat.UI;
using Shared.UI;
using Shared.Localization;

namespace Bootstrap.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _exitSaveButton;

        [Header("Deck Display")]
        [SerializeField] private ScrollRect _deckScrollRect;
        [SerializeField] private Transform _deckContent;
        [SerializeField] private GameObject _cardViewPrefab;

        [Header("Localized Labels")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _resumeButtonText;
        [SerializeField] private TextMeshProUGUI _exitSaveButtonText;

        private InputAction _escapeAction;

        void Start()
        {
            gameObject.SetActive(false);

            _resumeButton.onClick.AddListener(Resume);
            _exitSaveButton.onClick.AddListener(ExitAndSave);
            ApplyLocalization();

            _escapeAction = new InputAction("EscapeMenu", binding: "<Keyboard>/escape");
            _escapeAction.performed += _ => Toggle();
            _escapeAction.Enable();
        }

        void OnDestroy()
        {
            _escapeAction?.Disable();
            _escapeAction?.Dispose();
        }

        private void Toggle()
        {
            if (gameObject.activeSelf) Resume();
            else Open();
        }

        private void ApplyLocalization()
        {
            if (_titleText == null)
            {
                foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (string.Equals(text.text, "Paused", System.StringComparison.OrdinalIgnoreCase))
                    {
                        _titleText = text;
                        break;
                    }
                }
            }

            if (_resumeButtonText == null && _resumeButton != null)
                _resumeButtonText = _resumeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (_exitSaveButtonText == null && _exitSaveButton != null)
                _exitSaveButtonText = _exitSaveButton.GetComponentInChildren<TextMeshProUGUI>(true);

            SetText(_titleText, LocalizationService.T("menu.paused", "Paused"));
            SetText(_resumeButtonText, LocalizationService.T("menu.resume", "Resume"));
            SetText(_exitSaveButtonText, LocalizationService.T("menu.exit_save", "Exit & Save"));
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }

        private void Open()
        {
            GlobalServices.IsMenuOpen = true;
            ApplyLocalization();

            foreach (Transform child in _deckContent)
                Destroy(child.gameObject);

            var playerData = GlobalServices.PlayerData;
            if (playerData?.ActiveDeckCardIds?.Count > 0)
            {
                foreach (var cardId in playerData.ActiveDeckCardIds)
                {
                    var def = CardDatabase.GetCard(cardId);
                    if (def == null) continue;

                    var cardViewObj = Instantiate(_cardViewPrefab, _deckContent);
                    var cardView = cardViewObj.GetComponent<CardView>();
                    if (cardView != null)
                    {
                        var model = new Card(def, -1);
                        cardView.Bind(model);

                        var button = cardViewObj.GetComponent<Button>();
                        if (button) button.interactable = false;

                        var dragHandler = cardViewObj.GetComponent<DragHandler>();
                        if (dragHandler) dragHandler.enabled = false;
                    }
                }
            }
            else
            {
                // Optionally show a "No cards" message - could add a simple Text element
            }

            gameObject.SetActive(true);
        }

        private void Resume()
        {
            gameObject.SetActive(false);
            GlobalServices.IsMenuOpen = false;
        }

        private async void ExitAndSave()
        {
            var duelManager = GlobalServices.Director.CurrentMode as DuelManager;
            if (duelManager != null)
            {
                duelManager.SaveCurrentDuel();
            }

            var stateService = GlobalServices.GameStateService;
            if (stateService != null)
            {
                var state = stateService.State;

                if (duelManager != null)
                {
                    state.ActiveDuelTableId = duelManager.TableId;
                }
                else
                {
                    state.ActiveDuelTableId = null;
                }

                var explorationMode = GlobalServices.Director.CurrentMode as Exploration.ExplorationMode;
                if (explorationMode != null)
                {
                    state.CurrentWorldSceneAddress = explorationMode.CurrentWorldAddress;
                }

                var player = Object.FindObjectOfType<Exploration.ExplorationController>();
                if (player != null)
                {
                    state.PlayerPosition = player.transform.position;
                    state.PlayerRotation = player.transform.rotation;
                }

                await stateService.SaveAsync();
            }

            Application.Quit();
        }
    }
}