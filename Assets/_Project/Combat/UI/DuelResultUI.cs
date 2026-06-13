using Core;
using Shared.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Combat.UI
{
    /// <summary>
    /// Shows a win/lose/draw banner after a duel ends so the player gets explicit closure
    /// before the scene transitions back to exploration.
    /// </summary>
    public class DuelResultUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private Button _continueButton;
        [SerializeField] private float _fadeSpeed = 6f;

        private float _targetAlpha;
        private EventBus _eventBus;
        private bool _showing;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            if (_continueButton != null) _continueButton.onClick.AddListener(Dismiss);
        }

        private void OnEnable()
        {
            _eventBus = GlobalServices.EventBus;
            if (_eventBus != null) _eventBus.Subscribe<DuelResultEvent>(OnDuelResult);
        }

        private void OnDisable()
        {
            if (_eventBus != null) _eventBus.Unsubscribe<DuelResultEvent>(OnDuelResult);
        }

        private void Update()
        {
            if (_canvasGroup == null) return;
            if (_canvasGroup.alpha != _targetAlpha)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.unscaledDeltaTime);
            }
            if (_showing && Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.eKey.wasPressedThisFrame))
            {
                Dismiss();
            }
        }

        private void OnDuelResult(DuelResultEvent evt)
        {
            string title, subtitle;
            switch (evt.Outcome)
            {
                case DuelOutcome.PlayerWon:
                    title = LocalizationService.T("duel_result.win.title", "Победа");
                    subtitle = LocalizationService.T("duel_result.win.subtitle", "Город противника разрушен. Возвращайтесь к исследованию.");
                    break;
                case DuelOutcome.PlayerLost:
                    title = LocalizationService.T("duel_result.lose.title", "Поражение");
                    subtitle = LocalizationService.T("duel_result.lose.subtitle", "Город пал. Попробуйте снова — каждая колода терпит проигрыш.");
                    break;
                case DuelOutcome.Draw:
                    title = LocalizationService.T("duel_result.draw.title", "Ничья");
                    subtitle = LocalizationService.T("duel_result.draw.subtitle", "Никто не уцелел. Дуэль завершена.");
                    break;
                default:
                    return;
            }

            if (_titleText != null) _titleText.text = title;
            if (_subtitleText != null) _subtitleText.text = subtitle;

            _targetAlpha = 1f;
            _showing = true;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
        }

        public void Dismiss()
        {
            _showing = false;
            _targetAlpha = 0f;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
        }
    }
}
