using System.Collections.Generic;
using UnityEngine;
using Definitions;
using Core;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

namespace Combat
{
    /// <summary>
    /// Система обучающих подсказок для первого боя
    /// Показывает пошаговые инструкции с визуальными указателями
    /// </summary>
    public class TutorialSystem : MonoBehaviour
    {
        [Header("Tutorial Settings")]
        [SerializeField] private bool _isTutorialEncounter = false;
        [SerializeField] private bool _forceShowAlways = false;
        [SerializeField] private List<TutorialStep> _tutorialSteps = new List<TutorialStep>();

        [Header("UI References")]
        [SerializeField] private TutorialArrowUI _arrowUI;
        [SerializeField] private TutorialMessageUI _messageUI;
        [SerializeField] private CanvasGroup _screenDimmer;
        [SerializeField] private HandUIManager _handUIManager;

        private int _currentStepIndex = 0;
        private bool _tutorialActive = false;
        private DuelManager _duelManager;
        private EventBus _eventBus;
        private CancellationTokenSource _cts;

        private bool _cardDragged = false;
        private bool _cardPlaced = false;
        private bool _targetSelected = false;
        private bool _phaseConfirmed = false;

        private void Start()
        {
            if (!_isTutorialEncounter) return;

            if (!_forceShowAlways && PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)
            {
                Debug.Log("[TutorialSystem] Tutorial already completed, skipping.");
                return;
            }

            if (_handUIManager == null)
            {
                _handUIManager = FindObjectOfType<HandUIManager>();
            }

            _eventBus = GlobalServices.EventBus;
            if (_eventBus == null)
            {
                Debug.LogError("[TutorialSystem] EventBus not found!");
                return;
            }

            SubscribeToEvents();
        }

        private const string TutorialCompletedKey = "combat_tutorial_completed";

        private void Update()
        {
            if (!_isTutorialEncounter) return;

            if (_tutorialActive)
            {
                var step = GetCurrentStep();
                if (step != null && step.DynamicArrow != DynamicArrowTarget.None)
                {
                    RefreshDynamicArrow(step);
                }
                return;
            }

            if (!_forceShowAlways && PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1) return;

            if (_duelManager == null)
            {
                _duelManager = FindObjectOfType<DuelManager>();
            }

            if (_duelManager != null && _duelManager.CurrentDuelState != null && !_tutorialActive)
            {
                StartTutorial();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void SubscribeToEvents()
        {
            _eventBus.Subscribe<PhaseEnterEvent>(OnPhaseEnter);
            _eventBus.Subscribe<ActionExecutedEvent>(OnActionExecuted);
        }

        private void UnsubscribeFromEvents()
        {
            if (_eventBus == null) return;

            _eventBus.Unsubscribe<PhaseEnterEvent>(OnPhaseEnter);
            _eventBus.Unsubscribe<ActionExecutedEvent>(OnActionExecuted);
        }

        private void OnPhaseEnter(PhaseEnterEvent evt)
        {
            if (!_tutorialActive) return;

            var step = GetCurrentStep();
            if (step == null) return;

            if (step.CompletionCondition == TutorialStepCondition.PhaseEntered)
            {
                if (string.IsNullOrEmpty(step.RequiredPhaseTag) ||
                    evt.Tags.Contains(step.RequiredPhaseTag))
                {
                    NextStep();
                }
            }
        }

        private void OnActionExecuted(ActionExecutedEvent evt)
        {
            if (!_tutorialActive) return;

            var step = GetCurrentStep();
            if (step == null) return;

            if (evt.Action is PlaceCardIntoSlotAction)
            {
                _cardPlaced = true;
                if (step.CompletionCondition == TutorialStepCondition.CardPlaced)
                {
                    NextStep();
                }
            }

            if (step.CompletionCondition == TutorialStepCondition.ActionExecuted)
            {
                NextStep();
            }
        }

        private void StartTutorial()
        {
            _tutorialActive = true;
            _currentStepIndex = 0;
            _cts = new CancellationTokenSource();

            Debug.Log("[TutorialSystem] Tutorial started!");
            ShowCurrentStep();
        }

        private void ShowCurrentStep()
        {
            if (_currentStepIndex >= _tutorialSteps.Count)
            {
                EndTutorial();
                return;
            }

            var step = _tutorialSteps[_currentStepIndex];

            Debug.Log($"[TutorialSystem] Showing step {_currentStepIndex}: {step.Message}");

            if (_screenDimmer != null)
            {
                _screenDimmer.alpha = step.DimScreen ? 0.5f : 0f;
            }

            if (_messageUI != null)
            {
                _messageUI.ShowMessage(step.Message);
            }
            else
            {
                Debug.LogWarning("[TutorialSystem] MessageUI is not assigned!");
            }

            if (_arrowUI != null)
            {
                if (step.DynamicArrow != DynamicArrowTarget.None)
                {
                    RefreshDynamicArrow(step);
                }
                else if (step.TargetUIElement != null)
                {
                    _arrowUI.PointToUI(step.TargetUIElement);
                }
                else if (step.TargetObject != null)
                {
                    _arrowUI.PointTo(step.TargetObject);
                }
                else
                {
                    _arrowUI.Hide();
                }
            }

            ResetStepFlags();
            ProcessStepCondition(step).Forget();
        }

        private void RefreshDynamicArrow(TutorialStep step)
        {
            if (_arrowUI == null || _duelManager?.CurrentDuelState == null) return;
            var state = _duelManager.CurrentDuelState;
            Board board = null;
            System.Func<BoardCard, bool> filter = null;

            switch (step.DynamicArrow)
            {
                case DynamicArrowTarget.PlayerVanguardCard:
                    board = state.PlayerSide.Board;
                    filter = c => c != null && c.IsAlive && c.TypeOfRow == Definitions.RowType.Vanguard;
                    break;
                case DynamicArrowTarget.EnemyAnyAliveCard:
                    board = state.OpponentSide.Board;
                    filter = c => c != null && c.IsAlive;
                    break;
            }

            if (board == null) { _arrowUI.Hide(); return; }

            BoardSlotUI matched = null;
            foreach (var slotUI in FindObjectsOfType<BoardSlotUI>(true))
            {
                if (slotUI.Board != board) continue;
                if (filter(slotUI.Occupant)) { matched = slotUI; break; }
            }

            if (matched != null)
            {
                _arrowUI.PointToUI(matched.transform as RectTransform);
            }
            else
            {
                _arrowUI.Hide();
            }
        }

        private void ResetStepFlags()
        {
            _cardDragged = false;
            _cardPlaced = false;
            _targetSelected = false;
            _phaseConfirmed = false;
        }

        private async UniTaskVoid ProcessStepCondition(TutorialStep step)
        {
            try
            {
                switch (step.CompletionCondition)
                {
                    case TutorialStepCondition.TimeElapsed:
                        await UniTask.Delay(TimeSpan.FromSeconds(step.TimeToWait), cancellationToken: _cts.Token);
                        NextStep();
                        break;

                    case TutorialStepCondition.None:
                        await UniTask.Delay(TimeSpan.FromSeconds(2f), cancellationToken: _cts.Token);
                        NextStep();
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private TutorialStep GetCurrentStep()
        {
            if (_currentStepIndex >= 0 && _currentStepIndex < _tutorialSteps.Count)
                return _tutorialSteps[_currentStepIndex];
            return null;
        }

        public void NextStep()
        {
            if (!_tutorialActive) return;

            _currentStepIndex++;
            ShowCurrentStep();
        }

        private void EndTutorial()
        {
            _tutorialActive = false;

            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();

            if (_screenDimmer != null)
            {
                _screenDimmer.alpha = 0f;
            }

            if (_messageUI != null)
            {
                _messageUI.Hide();
            }

            if (_arrowUI != null)
            {
                _arrowUI.Hide();
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            Debug.Log("[TutorialSystem] Tutorial completed!");
        }

        public void OnCardDragStarted()
        {
            if (!_tutorialActive) return;

            _cardDragged = true;
            var step = GetCurrentStep();
            if (step != null && step.CompletionCondition == TutorialStepCondition.CardDragged)
            {
                NextStep();
            }
        }

        public void OnTargetSelected()
        {
            if (!_tutorialActive) return;

            _targetSelected = true;
            var step = GetCurrentStep();
            Debug.Log($"[TutorialSystem] OnTargetSelected. CurrentStep={_currentStepIndex}, Condition={step?.CompletionCondition}");
            if (step != null && step.CompletionCondition == TutorialStepCondition.TargetSelected)
            {
                NextStep();
            }
        }

        public void OnAttackerCardSelected()
        {
            if (!_tutorialActive) return;

            var step = GetCurrentStep();
            Debug.Log($"[TutorialSystem] OnAttackerCardSelected. CurrentStep={_currentStepIndex}, Condition={step?.CompletionCondition}");
            if (step != null && step.CompletionCondition == TutorialStepCondition.AttackerCardSelected)
            {
                NextStep();
            }
        }

        public void OnPhaseConfirmed()
        {
            if (!_tutorialActive) return;

            _phaseConfirmed = true;
            var step = GetCurrentStep();
            if (step != null && step.CompletionCondition == TutorialStepCondition.PhaseConfirmed)
            {
                NextStep();
            }
        }

        public bool IsTutorialActive => _tutorialActive;
        public int CurrentStepIndex => _currentStepIndex;
    }

    /// <summary>
    /// Один шаг обучения
    /// </summary>
    public enum DynamicArrowTarget
    {
        None,
        PlayerVanguardCard,
        EnemyAnyAliveCard,
    }

    [System.Serializable]
    public class TutorialStep
    {
        [TextArea(3, 5)]
        public string Message;

        public GameObject TargetObject;
        public RectTransform TargetUIElement;
        public DynamicArrowTarget DynamicArrow = DynamicArrowTarget.None;

        public bool DimScreen = true;
        public string PhaseTag;

        public TutorialStepCondition CompletionCondition = TutorialStepCondition.ManualAdvance;
        public float TimeToWait = 3f;
        public string RequiredPhaseTag;
    }
}
