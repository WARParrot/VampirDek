using System.Collections.Generic;
using Core;
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

        private static readonly Dictionary<string, string> PhaseNames = new Dictionary<string, string>
        {
            { "DuelStart", "Начало дуэли" },
            { "StartOfTurn", "Начало хода" },
            { "BuildingPhase", "Фаза строительства" },
            { "PlanningPhase", "Фаза планирования" },
            { "ClashingPhase", "Фаза столкновений" },
            { "OneSidedAttackPhase", "Атака" },
            { "EndOfTurn", "Конец хода" },
            { "Loot", "Лут" },
            { "DuelEnd", "Конец дуэли" },
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
                    if (PhaseNames.TryGetValue(tag, out var name)) { display = name; break; }
                }
            }
            if (display == null) PhaseNames.TryGetValue(evt.PhaseId ?? "", out display);
            if (string.IsNullOrEmpty(display)) display = evt.PhaseId;

            if (_phaseText != null) _phaseText.text = display;
            _targetAlpha = 1f;
            _hideTimer = _autoHide ? _autoHideAfter : 0f;
        }
    }
}
