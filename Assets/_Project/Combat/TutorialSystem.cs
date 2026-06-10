using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;

using Definitions;

using Core;

using Shared.Localization;

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

        private bool _tutorialCompletedThisSession = false;

        private DuelManager _duelManager;

        private EventBus _eventBus;

        private CancellationTokenSource _cts;

        private bool _cardDragged = false;

        private bool _cardPlaced = false;

        private bool _targetSelected = false;

        private bool _phaseConfirmed = false;

        // Set if a PlaceCardIntoSlotAction lands while a CardDragged step is still

        // counting down its read-time. The upcoming CardPlaced step would otherwise

        // hang waiting for a second placement that never happens.

        private bool _placeObservedDuringDragStep = false;

        private bool _waitingForRequiredPhase = false;

        private bool _advancePending = false;

        private bool _tutorialLeaveRequested = false;

        private bool _draftStepReached = false;

        private bool _deferLeavePromptUntilDraftComplete = false;

        private float _stepShownAt = 0f;

        private bool _tutorialFullyAcknowledged = false;

        private const float MinimumInteractiveReadSeconds = 5f;

        private const float MinimumManualAdvanceSeconds = 3f;

        private const float TimeElapsedManualGracePadding = 1.5f;

        private const string TutorialCompletedKey = "combat_tutorial_completed";

        private const string TutorialStartCountKey = "combat_tutorial_start_count";

        private const int MaxTutorialStartCount = 1;



        private static bool ShouldSkipDueToStartLimit()

        {

            return PlayerPrefs.GetInt(TutorialStartCountKey, 0) >= MaxTutorialStartCount;

        }



        private void Start()

        {

            if (!_isTutorialEncounter) return;

            if (!_forceShowAlways && PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1)

            {

                Debug.Log("[TutorialSystem] Tutorial already completed, skipping.");

                return;

            }

            if (!_forceShowAlways && ShouldSkipDueToStartLimit())

            {

                Debug.Log("[TutorialSystem] Tutorial already shown the maximum number of times, skipping.");

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

                if (IsLeaveDuelPromptActive() && Keyboard.current?.sKey.wasPressedThisFrame == true)

                {

                    TryLeaveTutorialDuelFromPrompt();

                }

                if (step != null && IsManuallyAdvanceable(step) && IsContinuePressed())

                {

                    var elapsed = Time.unscaledTime - _stepShownAt;

                    if (elapsed >= MinimumManualAdvanceSeconds)

                    {

                        AdvanceManually();

                    }

                }

                return;

            }

            // _forceShowAlways means "show on every tutorial duel even if PlayerPrefs says completed".

            // It must not restart the tutorial in a loop after it completed in the same duel scene.

            if (_tutorialCompletedThisSession) return;

            if (!_forceShowAlways && PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1) return;

            if (!_forceShowAlways && ShouldSkipDueToStartLimit()) return;

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

            _eventBus.Subscribe<PlacedCardEvent>(OnPlacedCard);

            _eventBus.Subscribe<HintEvent>(OnHint);

        }

        private void UnsubscribeFromEvents()

        {

            if (_eventBus == null) return;

            _eventBus.Unsubscribe<PhaseEnterEvent>(OnPhaseEnter);

            _eventBus.Unsubscribe<ActionExecutedEvent>(OnActionExecuted);

            _eventBus.Unsubscribe<PlacedCardEvent>(OnPlacedCard);

            _eventBus.Unsubscribe<HintEvent>(OnHint);

        }

        /// <summary>

        /// Returns true if the duel may open the draft UI right now. During the tutorial we

        /// hold the draft closed until the intro steps reach the dedicated "draft" step so

        /// the player only sees the board + empty hand while reading the explainers.

        /// </summary>

        public bool IsReadyForDraft()

        {

            if (!_isTutorialEncounter) return true;

            if (!_tutorialActive)

            {

                // If the tutorial has already been skipped or completed, do not hold up drafting.

                if (!_forceShowAlways && (PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1 || ShouldSkipDueToStartLimit()))

                    return true;

                return false;

            }

            // Once we've shown the draft step at least once, later drafts may open immediately,

            // except when the player is still reading the end-of-turn explanation. The final

            // leave-duel prompt is intentionally held until that next draft closes.

            if (_deferLeavePromptUntilDraftComplete) return true;

            var step = GetCurrentStep();

            if (step != null && IsTurnEndPrompt(step)) return false;

            if (_draftStepReached && IsCurrentPhaseTag("StartOfTurn") && !HasAcknowledgedStep("tutorial.turn_end"))

            {

                return false;

            }

            if (_draftStepReached) return true;

            if (step != null && step.CompletionCondition == TutorialStepCondition.DraftCompleted)

            {

                _draftStepReached = true;

                return true;

            }

            return false;

        }

        private void OnHint(HintEvent evt)

        {

            if (!_tutorialActive) return;

            var step = GetCurrentStep();

            if (step == null) return;

            if (_deferLeavePromptUntilDraftComplete && evt.Tag == "DraftCompleted")

            {

                _deferLeavePromptUntilDraftComplete = false;

                ShowCurrentStep();

                return;

            }

            if (step.CompletionCondition == TutorialStepCondition.DraftCompleted && evt.Tag == "DraftCompleted")

            {

                AdvanceAfterReadTime().Forget();

            }

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

                    AdvanceAfterReadTime().Forget();

                }

            }

        }

        private void OnPlacedCard(PlacedCardEvent evt)

        {

            if (!_tutorialActive) return;

            HandleCardPlacedForCurrentStep();

        }

        private void HandleCardPlacedForCurrentStep()

        {

            if (!_tutorialActive) return;

            _cardPlaced = true;

            var step = GetCurrentStep();

            if (step == null) return;

            if (step.CompletionCondition == TutorialStepCondition.CardPlaced)

            {

                AdvanceAfterReadTime().Forget();

            }

            else if (step.CompletionCondition == TutorialStepCondition.CardDragged)

            {

                _placeObservedDuringDragStep = true;

            }

        }

        private void OnActionExecuted(ActionExecutedEvent evt)

        {

            if (!_tutorialActive) return;

            var step = GetCurrentStep();

            if (step == null) return;

            if (evt.Action is PlaceCardIntoSlotAction)

            {

                HandleCardPlacedForCurrentStep();

            }

            if (step.CompletionCondition == TutorialStepCondition.ActionExecuted)

            {

                AdvanceAfterReadTime().Forget();

            }

        }

        private void StartTutorial()

        {

            // Count every time we actually begin the tutorial flow so we can cap how often the

            // player re-sees it after sitting at the duel table.

            var startCount = PlayerPrefs.GetInt(TutorialStartCountKey, 0) + 1;

            PlayerPrefs.SetInt(TutorialStartCountKey, startCount);

            PlayerPrefs.Save();

            Debug.Log($"[TutorialSystem] Tutorial start count is now {startCount}/{MaxTutorialStartCount}.");



            // Keep tutorial content data-driven from TutorialStepsData instead of stale scene-serialized lists.

            // Older scene overrides are easy to forget and were the reason dynamic arrows silently disappeared.

            var defaultSteps = TutorialStepsData.CreateDefaultSteps();

            _tutorialSteps = defaultSteps;

            SeedTutorialDraft();

            _tutorialActive = true;

            _tutorialCompletedThisSession = false;

            _currentStepIndex = 0;

            _tutorialLeaveRequested = false;

            _draftStepReached = false;

            _deferLeavePromptUntilDraftComplete = false;

            _placeObservedDuringDragStep = false;

            _cts = new CancellationTokenSource();

            Debug.Log("[TutorialSystem] Tutorial started!");

            ShowCurrentStep();

        }

        private void SeedTutorialDraft()

        {

            // With empty starting hands + a draft on every StartOfTurn, we can't rely on the

            // player happening to roll Vampire from the random portion of the draft. Force it

            // into the next draft so the tutorial's Vanguard step can actually fire.

            if (_duelManager == null) return;

            _duelManager.PendingDraftCardNames.Clear();

            _duelManager.PendingDraftCardNames.Add("Vampire");

            _duelManager.PendingMandatoryDraftCardNames.Clear();

            _duelManager.PendingMandatoryDraftCardNames.Add("Human");

            _duelManager.PendingMandatoryDraftCardNames.Add("Vampire");

            _duelManager.PendingMandatoryDraftCardName = "Human";

            Debug.Log("[TutorialSystem] Seeded next draft with Vampire; Human and Vampire marked mandatory.");

        }



        private void ShowCurrentStep()

        {

            if (_currentStepIndex >= _tutorialSteps.Count)

            {

                // Reaching the natural end of the step list means the player went through everything.

                _tutorialFullyAcknowledged = true;

                EndTutorial();

                return;

            }

            var step = _tutorialSteps[_currentStepIndex];

            if (ShouldDeferLeavePromptUntilDraftComplete(step))

            {

                _deferLeavePromptUntilDraftComplete = true;

                _messageUI?.Hide();

                _arrowUI?.Hide();

                if (_screenDimmer != null)

                {

                    _screenDimmer.alpha = 0f;

                    _screenDimmer.blocksRaycasts = false;

                    _screenDimmer.interactable = false;

                }

                Debug.Log("[TutorialSystem] Deferring leave-duel prompt until the current draft completes.");

                return;

            }

            if (ShouldSkipStepForCurrentState(step))

            {

                Debug.Log($"[TutorialSystem] Skipping irrelevant step {_currentStepIndex}: {step.CompletionCondition}");

                _currentStepIndex++;

                ShowCurrentStep();

                return;

            }

            if (!IsStepPhaseReady(step))

            {

                WaitForRequiredPhaseThenShow(_currentStepIndex, step.RequiredPhaseTag).Forget();

                return;

            }

            _stepShownAt = Time.unscaledTime;

            _advancePending = false;

            Debug.Log($"[TutorialSystem] Showing step {_currentStepIndex}: {step.Message}");

            if (_screenDimmer != null)

            {

                _screenDimmer.alpha = step.DimScreen ? 0.5f : 0f;

                // The dimmer is only visual. If it blocks raycasts, tutorial-highlighted

                // board cards/buttons feel randomly unclickable depending on overlay order.

                _screenDimmer.blocksRaycasts = false;

                _screenDimmer.interactable = false;

            }

            if (_messageUI != null)

            {

                var previewCard = !string.IsNullOrEmpty(step.PreviewCardName)

                    ? CardDatabase.GetCard(step.PreviewCardName)

                    : null;

                var previewPrefab = _handUIManager != null ? _handUIManager.CardViewPrefab : null;

                _messageUI.ShowMessage(BuildContextualMessage(step), previewCard, previewPrefab, step.PreviewCaption);

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

            if (step.CompletionCondition == TutorialStepCondition.CardPlaced && (_placeObservedDuringDragStep || _cardPlaced))

            {

                _placeObservedDuringDragStep = false;

                AdvanceAfterReadTime().Forget();

                return;

            }

            ProcessStepCondition(step).Forget();

        }

        private bool ShouldDeferLeavePromptUntilDraftComplete(TutorialStep step)

        {

            if (step == null || step.CompletionCondition != TutorialStepCondition.LeaveDuel) return false;

            if (!_draftStepReached || _deferLeavePromptUntilDraftComplete) return false;

            var tags = _duelManager?.CurrentDuelState?.CurrentPhase?.Tags;

            return tags != null && tags.Contains("StartOfTurn");

        }

        private static bool IsTurnEndPrompt(TutorialStep step)

        {

            return step != null && step.MessageKey == "tutorial.turn_end";

        }

        private bool HasAcknowledgedStep(string messageKey)

        {

            if (string.IsNullOrEmpty(messageKey)) return true;

            for (var i = 0; i < _tutorialSteps.Count; i++)

            {

                if (_tutorialSteps[i]?.MessageKey == messageKey)

                {

                    return _currentStepIndex > i;

                }

            }

            return true;

        }

        private bool IsCurrentPhaseTag(string phaseTag)

        {

            if (string.IsNullOrEmpty(phaseTag)) return false;

            var tags = _duelManager?.CurrentDuelState?.CurrentPhase?.Tags;

            return tags != null && tags.Contains(phaseTag);

        }

        private bool IsStepPhaseReady(TutorialStep step)

        {

            if (step == null || string.IsNullOrEmpty(step.RequiredPhaseTag)) return true;

            var tags = _duelManager?.CurrentDuelState?.CurrentPhase?.Tags;

            return tags != null && tags.Contains(step.RequiredPhaseTag);

        }

        private async UniTaskVoid WaitForRequiredPhaseThenShow(int stepIndex, string requiredPhaseTag)

        {

            if (_waitingForRequiredPhase) return;

            _waitingForRequiredPhase = true;

            try

            {

                while (_tutorialActive && _currentStepIndex == stepIndex)

                {

                    var step = GetCurrentStep();

                    if (step == null || string.IsNullOrEmpty(requiredPhaseTag) || IsStepPhaseReady(step))

                    {

                        break;

                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, _cts?.Token ?? this.GetCancellationTokenOnDestroy());

                }

            }

            catch (OperationCanceledException)

            {

                return;

            }

            finally

            {

                _waitingForRequiredPhase = false;

            }

            if (_tutorialActive && _currentStepIndex == stepIndex)

            {

                ShowCurrentStep();

            }

        }

        private void RefreshDynamicArrow(TutorialStep step)

        {

            if (_arrowUI == null || _duelManager?.CurrentDuelState == null) return;

            var state = _duelManager.CurrentDuelState;

            switch (step.DynamicArrow)

            {

                case DynamicArrowTarget.PlayerPlayableHandCard:

                {

                    var target = _handUIManager != null ? _handUIManager.FindFirstPlayableCardViewForTutorial() : null;

                    if (target != null) _arrowUI.PointToUI(target);

                    else _arrowUI.Hide();

                    return;

                }

                case DynamicArrowTarget.PlayerPlayableBoardSlot:

                {

                    var target = _handUIManager != null ? _handUIManager.FindFirstPlayableBoardSlotForTutorial() : null;

                    if (target != null) _arrowUI.PointToUI(target);

                    else _arrowUI.Hide();

                    return;

                }

                case DynamicArrowTarget.PlayerHumanResourcesText:

                {

                    var target = _handUIManager != null ? _handUIManager.FindHumanResourcesTextForTutorial() : null;

                    if (target != null) _arrowUI.PointToUI(target);

                    else _arrowUI.Hide();

                    return;

                }

                case DynamicArrowTarget.PhaseConfirmationButton:

                {

                    var button = FindObjectOfType<global::PhaseConfirmationButton>(true);

                    var target = button != null ? button.transform as RectTransform : null;

                    if (target != null && target.gameObject.activeInHierarchy) _arrowUI.PointToUI(target);

                    else _arrowUI.Hide();

                    return;

                }

            }

            Board board = null;

            System.Func<BoardCard, bool> filter = null;

            switch (step.DynamicArrow)

            {

                case DynamicArrowTarget.PlayerVanguardCard:

                    board = state.PlayerSide.Board;

                    filter = IsAttackCapable;

                    break;

                case DynamicArrowTarget.EnemyAnyAliveCard:

                    board = state.OpponentSide.Board;

                    filter = c => c != null && c.IsAlive;

                    break;

            }

            if (board == null || filter == null) { _arrowUI.Hide(); return; }

            var boardView = FindObjectOfType<BoardView>(true);

            var matched = boardView != null

                ? boardView.FindFirstOccupiedSlot(board, filter)

                : null;

            if (matched != null)

            {

                _arrowUI.PointToUI(matched.transform as RectTransform);

            }

            else

            {

                _arrowUI.Hide();

            }

        }

        private string BuildContextualMessage(TutorialStep step)

        {

            if (step == null) return string.Empty;

            var message = LocalizationService.T(step.MessageKey, step.Message ?? string.Empty);

            if (message.Contains("{PlayableCardHint}"))

            {

                var hint = _handUIManager != null

                    ? _handUIManager.GetTutorialPlayableCardHint()

                    : LocalizationService.T("tutorial.hand_initializing", "Tutorial is waiting for the hand to initialize.");

                message = message.Replace("{PlayableCardHint}", hint);

            }

            if (IsManuallyAdvanceable(step))

            {

                var continuePrompt = LocalizationService.T(

                    "tutorial.continue_prompt",

                    "Нажмите Space, чтобы продолжить.");

                message += $"\n\n<size=80%><color=#FFD36A>▶ {continuePrompt}</color></size>";

            }

            return message;

        }



        private static bool IsManuallyAdvanceable(TutorialStep step)

        {

            if (step == null) return false;

            return step.CompletionCondition == TutorialStepCondition.ManualAdvance ||

                   step.CompletionCondition == TutorialStepCondition.None ||

                   step.CompletionCondition == TutorialStepCondition.TimeElapsed;

        }



        private static bool IsContinuePressed()

        {

            var kb = Keyboard.current;

            if (kb == null) return false;

            return kb.spaceKey.wasPressedThisFrame ||

                   kb.enterKey.wasPressedThisFrame;

        }



        private void AdvanceManually()

        {

            if (!_tutorialActive || _advancePending) return;

            _advancePending = true;

            NextStep();

        }

        private bool ShouldSkipStepForCurrentState(TutorialStep step)

        {

            if (step == null) return false;

            if (step.CompletionCondition == TutorialStepCondition.CardDragged ||

                step.CompletionCondition == TutorialStepCondition.CardPlaced)

            {

                return _handUIManager != null && _handUIManager.GetFirstTutorialPlayableCardDef() == null;

            }

            if (step.CompletionCondition == TutorialStepCondition.AttackerCardSelected ||

                step.DynamicArrow == DynamicArrowTarget.PlayerVanguardCard)

            {

                return !HasAttackCapableCard(_duelManager?.CurrentDuelState?.PlayerSide?.Board);

            }

            if (step.CompletionCondition == TutorialStepCondition.TargetSelected ||

                step.DynamicArrow == DynamicArrowTarget.EnemyAnyAliveCard)

            {

                var state = _duelManager?.CurrentDuelState;

                return !HasAttackCapableCard(state?.PlayerSide?.Board) ||

                       !HasAnyAliveCard(state?.OpponentSide?.Board);

            }

            return false;

        }

        private static bool HasAttackCapableCard(Board board)

        {

            if (board == null) return false;

            foreach (var slot in board.AllSlots())

            {

                if (IsAttackCapable(slot?.Occupant)) return true;

            }

            return false;

        }



        private static bool IsAttackCapable(BoardCard card)

        {

            return card != null && card.IsAlive && card.Attack > 0;

        }

        private static bool HasAnyAliveCard(Board board)

        {

            if (board == null) return false;

            foreach (var slot in board.AllSlots())

            {

                if (slot?.Occupant != null && slot.Occupant.IsAlive) return true;

            }

            return false;

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

                    {

                        // TimeElapsed used to auto-advance with a hard timer.

                        // Players reported steps changing before they could read them, so we now

                        // treat the configured time as a *minimum* and wait for Space afterwards.

                        var minTime = Mathf.Max(step.TimeToWait, MinimumManualAdvanceSeconds) + TimeElapsedManualGracePadding;

                        await UniTask.Delay(TimeSpan.FromSeconds(minTime), cancellationToken: _cts.Token);

                        // After the read window the Update loop handles Space. Nothing else to do.

                        break;

                    }

                    case TutorialStepCondition.None:

                    case TutorialStepCondition.ManualAdvance:

                        // Wait for the player to press Space. Update() drives the advance.

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

        private async UniTaskVoid AdvanceAfterReadTime()

        {

            if (!_tutorialActive || _advancePending) return;

            _advancePending = true;

            var remaining = MinimumInteractiveReadSeconds - (Time.unscaledTime - _stepShownAt);

            if (remaining > 0f)

            {

                try

                {

                    await UniTask.Delay(TimeSpan.FromSeconds(remaining), cancellationToken: _cts?.Token ?? this.GetCancellationTokenOnDestroy());

                }

                catch (OperationCanceledException)

                {

                    return;

                }

            }

            _advancePending = false;

            NextStep();

        }

        public bool OnLeaveDuelRequested()

        {

            if (!_isTutorialEncounter || !_tutorialActive) return true;



            if (IsLeaveDuelPromptActive())

            {

                _tutorialLeaveRequested = true;

                _tutorialFullyAcknowledged = true;

                EndTutorial();

                return true;

            }



            Debug.Log("[TutorialSystem] The tutorial is still active; finish the current tutorial prompt before leaving the duel table.");

            return false;

        }



        public bool IsLeaveDuelPromptActive()

        {

            var step = GetCurrentStep();

            return step != null && step.CompletionCondition == TutorialStepCondition.LeaveDuel;

        }



        private void TryLeaveTutorialDuelFromPrompt()

        {

            if (_tutorialLeaveRequested) return;

            _tutorialLeaveRequested = true;

            EndTutorial();



            if (_duelManager == null)

            {

                _duelManager = FindObjectOfType<DuelManager>(true);

            }



            if (_duelManager == null)

            {

                Debug.LogWarning("[TutorialSystem] Cannot leave tutorial duel: DuelManager is not available.");

                return;

            }



            _duelManager.RequestLeaveDuel();

        }



        public void CompleteTutorialForTerminalDuel()

        {

            if (!_isTutorialEncounter) return;

            if (_tutorialCompletedThisSession && !_tutorialActive) return;



            Debug.Log("[TutorialSystem] Tutorial duel reached a terminal outcome; completing combat tutorial prompts.");

            EndTutorial();

        }



        private void EndTutorial()

        {

            _tutorialActive = false;

            _tutorialCompletedThisSession = true;

            // Only persist completion when the player explicitly worked through to the

            // final acknowledgement step. Terminal-duel early exits (win/loss before the

            // tutorial finished) used to mark it completed and skip onboarding on relaunch.

            if (_tutorialFullyAcknowledged)

            {

                PlayerPrefs.SetInt(TutorialCompletedKey, 1);

                PlayerPrefs.Save();

            }

            else

            {

                Debug.Log("[TutorialSystem] Tutorial ended early; not persisting completion so the player sees it again.");

            }

            if (_screenDimmer != null)

            {

                _screenDimmer.alpha = 0f;

                _screenDimmer.blocksRaycasts = false;

                _screenDimmer.interactable = false;

            }

            if (_messageUI != null)

            {

                _messageUI.Hide();

            }

            if (_arrowUI != null)

            {

                _arrowUI.Hide();

            }

            _handUIManager?.RefreshHandImmediately();

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

                AdvanceAfterReadTime().Forget();

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

                AdvanceAfterReadTime().Forget();

            }

        }

        public void OnAttackerCardSelected()

        {

            if (!_tutorialActive) return;

            var step = GetCurrentStep();

            Debug.Log($"[TutorialSystem] OnAttackerCardSelected. CurrentStep={_currentStepIndex}, Condition={step?.CompletionCondition}");

            if (step != null && step.CompletionCondition == TutorialStepCondition.AttackerCardSelected)

            {

                // Attacker selection is a two-click interaction: attacker, then target.

                // Delaying this transition makes the immediate target click look ignored.

                NextStep();

            }

        }

        public void OnPhaseConfirmed()

        {

            if (!_tutorialActive) return;

            if (!AllowsPhaseConfirmation()) return;

            _phaseConfirmed = true;

            AdvanceAfterReadTime().Forget();

        }



        public bool AllowsPhaseConfirmation()

        {

            if (!_tutorialActive) return false;

            var step = GetCurrentStep();

            return step != null &&

                   step.CompletionCondition == TutorialStepCondition.PhaseConfirmed &&

                   IsStepPhaseReady(step);

        }



        public bool AllowsCardDragging()

        {

            if (!_tutorialActive) return false;

            var step = GetCurrentStep();

            return step != null &&

                   IsStepPhaseReady(step) &&

                   (step.CompletionCondition == TutorialStepCondition.CardDragged ||

                    step.CompletionCondition == TutorialStepCondition.CardPlaced);

        }

        public bool IsTutorialActive => _tutorialActive;

        public int CurrentStepIndex => _currentStepIndex;

        public string PreferredPlayableCardName => GetCurrentStep()?.PreferredCardName;

    }

    /// <summary>

    /// Один шаг обучения

    /// </summary>

    public enum DynamicArrowTarget

    {

        None,

        PlayerVanguardCard,

        EnemyAnyAliveCard,

        PlayerPlayableHandCard,

        PlayerPlayableBoardSlot,

        PlayerHumanResourcesText,

        PhaseConfirmationButton,

    }

    [System.Serializable]

    public class TutorialStep

    {

        [TextArea(3, 5)]

        public string Message;

        public string MessageKey;

        public GameObject TargetObject;

        public RectTransform TargetUIElement;

        public DynamicArrowTarget DynamicArrow = DynamicArrowTarget.None;

        public bool DimScreen = true;

        public string PhaseTag;

        public TutorialStepCondition CompletionCondition = TutorialStepCondition.ManualAdvance;

        public float TimeToWait = 3f;

        public string RequiredPhaseTag;

        public string PreferredCardName;

        public string PreviewCardName;


        [TextArea(1, 3)] public string PreviewCaption;

    }

}
