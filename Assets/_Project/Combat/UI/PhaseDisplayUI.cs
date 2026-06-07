using System.Collections.Generic;
using Core;
using Shared.Localization;
using TMPro;
using UnityEngine;

namespace Combat.UI
{
    /// <summary>
    /// Отображает текущую фазу боя на экране.
    /// Слушает PhaseEnterEvent и обновляет текст с локализованным названием фазы.
    /// </summary>
    public class PhaseDisplayUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _phaseText;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _fadeSpeed = 5f;
        [SerializeField] private bool _autoHide = false;
        [SerializeField] private float _autoHideAfter = 2.5f;

        private EventBus _eventBus;
        private float _targetAlpha;
        private float _hideTimer;

        private static readonly Dictionary<string, string> PhaseKeys = new Dictionary<string, string>
        {
            { "DuelStart", "phase.duel_start" },
            { "StartOfTurn", "phase.start_of_turn" },
            { "BuildingPhase", "phase.building" },
            { "PlanningPhase", "phase.planning" },
            { "ClashingPhase", "phase.clashing" },
            { "OneSidedAttackPhase", "phase.one_sided_attack" },
            { "EndOfTurn", "phase.end_of_turn" },
            { "Loot", "phase.loot" },
            { "DuelEnd", "phase.duel_end" },
        };

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        private void OnEnable()
        {
            _eventBus = GlobalServices.EventBus;
            if (_eventBus != null) _eventBus.Subscribe<PhaseEnterEvent>(OnPhaseEnter);
        }

        private void OnDisable()
        {
            if (_eventBus != null) _eventBus.Unsubscribe<PhaseEnterEvent>(OnPhaseEnter);
        }

        private void Update()
        {
            if (_canvasGroup == null) return;

            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f) _targetAlpha = 0f;
            }

            if (_canvasGroup.alpha != _targetAlpha)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            }
        }

        private void OnPhaseEnter(PhaseEnterEvent evt)
        {
            string display = null;
            if (evt.Tags != null)
            {
                foreach (var tag in evt.Tags)
                {
                    if (PhaseKeys.TryGetValue(tag, out var key)) { display = LocalizationService.T(key, tag); break; }
                }
            }
            if (display == null && PhaseKeys.TryGetValue(evt.PhaseId ?? "", out var phaseKey)) display = LocalizationService.T(phaseKey, evt.PhaseId);
            if (string.IsNullOrEmpty(display)) display = evt.PhaseId;

            if (_phaseText != null) _phaseText.text = display;
            _targetAlpha = 1f;
            _hideTimer = _autoHide ? _autoHideAfter : 0f;
        }
    }
}
