using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.InputSystem;
using UnityEngine.AddressableAssets;
using Combat.GameActions;
using Combat.UI;

namespace Combat
{
    public class DuelManager : MonoBehaviour, IGameMode
    {
        private readonly Queue<IGameAction> _actionQueue = new();
        private CombatState _state = CombatState.PlayerTurnIdle;
        private DuelState _duelState;
        private CombatEncounter _encounter;
        private string _tableId;
        private Scene _combatScene;
        private AsyncOperationHandle<SceneInstance> _duelLoadHandle;

        private InputAction _leaveAction;
        private bool _leaveDuelRequested;
        private bool _victoryScreenShown;

        public DeckData _playerPersistentDeck;
        public DuelState CurrentDuelState => _duelState;
        public string TableId => _tableId;
        public bool LoadDuelScene = false;
        private bool _playerConfirmedPhase;
        private bool _phaseConfirmationReady;
        public bool CanConfirmCurrentPhase => _phaseConfirmationReady;
        private DuelOutcome _duelOutcome = DuelOutcome.InProgress;
        private bool _duelFinished = false;
        private bool _duelResultPublished = false;
        private bool _processingQueuedActions = false;
        private GameDirector _director;

        // AI System
        private OpponentAI _opponentAI;
        [SerializeField] private VictoryScreen _victoryScreenPrefab;

        public void ConfirmCurrentPhase()
        {
            Debug.Log($"[DuelDebug] ConfirmCurrentPhase requested. ready={_phaseConfirmationReady}; confirmed={_playerConfirmedPhase}; {DescribeDuelDebugState()}");
            if (!_phaseConfirmationReady)
            {
                Debug.Log("[DuelManager] ConfirmCurrentPhase ignored because the current phase is not ready for confirmation yet");
                return;
            }

            Debug.Log("[DuelManager] ConfirmCurrentPhase called");
            _playerConfirmedPhase = true;
        }

        public async UniTask EnterAsync(object context)
        {
            if (context is not DuelStartContext ctx)
            {
                Debug.LogError($"Duel requires {nameof(DuelStartContext)} but got {context?.GetType().Name ?? "null"}.");
                return;
            }
            _playerPersistentDeck = ctx.PlayerPersistentDeck;
            _encounter = ctx.Encounter;
            _tableId = ctx.TableId;
            _duelLoadHandle = ctx.DuelSceneHandle;
            _director = ctx.Director ?? ResolveGameDirector();
            MatchStateDTO savedDto = null;
            if (!string.IsNullOrEmpty(ctx.SavedMatchJson))
            {
                savedDto = JsonUtility.FromJson<MatchStateDTO>(ctx.SavedMatchJson);
                ctx.SavedMatchState = savedDto;
            }

            if (_encounter == null)
            {
                Debug.LogError("Cannot initialize duel: encounter is missing from the duel start context.");
                return;
            }

            List<CardDef> opponentDeckList = DeckDatabase.GetDeck(_encounter.OpponentDeckId)?.Cards;
            var playerDeckList = ctx.PlayerDeck;
            if (playerDeckList == null || opponentDeckList == null)
            {
                Debug.LogError($"Cannot initialize duel: {(playerDeckList == null ? "player deck is missing" : "opponent deck is missing")}.");
                return;
            }

            _opponentAI = new OpponentAI(AIStrategy.Balanced, 0.7f);

            if (ctx.SavedMatchState != null)
            {
                _duelState = ctx.SavedMatchState.ToDuelState(_encounter, playerDeckList, opponentDeckList);
                await ResumeFromSaveAsync();
            }
            else
            {
                try
                {
                    _duelState = new DuelState(_encounter, playerDeckList, opponentDeckList);
                    await TransitionToPhaseAsync(_duelState.CurrentPhase);
                }
                catch (System.InvalidOperationException ex)
                {
                    Debug.LogError($"Failed to initialize duel: {ex.Message}");
                    if (_leaveAction != null)
                    {
                        _leaveAction.performed -= OnLeaveDuel;
                        _leaveAction.Disable();
                        _leaveAction = null;
                    }
                    var director = ResolveGameDirector();
                    if (director != null)
                    {
                        director.PopModeAsync().Forget();
                    }
                    return;
                }
            }

            var input = FindObjectOfType<InputController>();
            if (input != null)
            {
                _leaveAction = input.GetAction("Duel/LeaveDuel");
                if (_leaveAction != null)
                {
                    _leaveAction.performed += OnLeaveDuel;
                    _leaveAction.Enable();
                }
            }

            GlobalServices.EventBus.Publish(new DuelStartedEvent(_encounter));
        }

        public async UniTask ExitAsync()
        {
            if (_duelState != null)
            {
                CaptureDuelOutcomeIfFinished();
            }

            PublishDuelResultIfNeeded();

            if (_duelState != null && _encounter != null)
            {
                if (_duelOutcome == DuelOutcome.InProgress)
                {
                    var dto = MatchStateDTO.FromDuelState(_duelState);
                    string json = JsonUtility.ToJson(dto);
                    GlobalServices.SaveSystem.SaveActiveBattle(_tableId, json);
                }
                else
                {
                    GlobalServices.SaveSystem.ClearActiveBattle(_tableId);
                }
            }

            if (_duelState != null)
            {
                DetachAllEnchantments(_duelState.PlayerSide);
                DetachAllEnchantments(_duelState.OpponentSide);
            }

            if (_duelLoadHandle.IsValid())
                await Addressables.UnloadSceneAsync(_duelLoadHandle, true);

            _duelState = null;
            _encounter = null;

            if (_leaveAction != null)
            {
                _leaveAction.performed -= OnLeaveDuel;
                _leaveAction.Disable();
                _leaveAction.Dispose();
            }

            var switcher = Camera.main?.GetComponent<DuelCameraSwitcher>();
            if (switcher != null)
                switcher.enabled = false;

            GlobalServices.EventBus.Publish(new DuelEndedEvent());
            Destroy(gameObject);
        }

        public UniTask OnPauseAsync()
        {
            _state = CombatState.Paused;
            return UniTask.CompletedTask;
        }

        public UniTask OnResumeAsync()
        {
            _state = CombatState.PlayerTurnIdle;
            return UniTask.CompletedTask;
        }

        private GameDirector ResolveGameDirector()
        {
            if (_director != null) return _director;

            try
            {
                var director = GlobalServices.Director;
                if (director != null)
                {
                    _director = director;
                    return _director;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DuelManager] GameDirector service lookup failed: {ex.Message}");
            }

            return null;
        }

        public void RequestLeaveDuel()
        {
            TryLeaveDuel();
        }

        private void Update()
        {
            if (_leaveDuelRequested) return;

            var actionPressed = _leaveAction != null && _leaveAction.WasPressedThisFrame();
            var keyboardPressed = Keyboard.current?.sKey.wasPressedThisFrame == true;
            if (actionPressed || keyboardPressed)
            {
                TryLeaveDuel();
            }
        }

        private void OnLeaveDuel(InputAction.CallbackContext ctx)
        {
            TryLeaveDuel();
        }

        private void TryLeaveDuel()
        {
            if (_leaveDuelRequested) return;

            var tutorial = FindObjectOfType<TutorialSystem>(true);
            var isFinalTutorialLeavePrompt = tutorial != null && tutorial.IsLeaveDuelPromptActive();
            if (GlobalServices.IsMenuOpen && !isFinalTutorialLeavePrompt) return;

            if (tutorial != null && !tutorial.OnLeaveDuelRequested())
            {
                return;
            }

            var director = ResolveGameDirector();
            if (director == null)
            {
                Debug.LogWarning("Cannot leave duel: GameDirector service is not available.");
                return;
            }

            _leaveDuelRequested = true;
            director.PopModeAsync().Forget();
        }

        private void OnDestroy()
        {
            _leaveAction?.Disable();
            _leaveAction?.Dispose();
        }

        private async UniTask ResumeFromSaveAsync()
        {
            await UniTask.DelayFrame(5);
            var boardView = FindObjectOfType<BoardView>(true);
            boardView?.RefreshAllSlots();

            var handUI = FindObjectOfType<HandUIManager>(true);
            handUI?.RefreshHandImmediately();

            await ResumeCurrentPhaseAsync();

            boardView?.RefreshAllSlots();
            handUI?.RefreshHandImmediately();
        }

        private async UniTask ResumeCurrentPhaseAsync()
        {
            var currentPhase = _duelState?.CurrentPhase;
            if (currentPhase == null)
            {
                Debug.LogWarning("[DuelManager] Cannot resume phase: current phase is missing.");
                return;
            }

            GlobalServices.EventBus.Publish(new PhaseEnterEvent(currentPhase.PhaseId, currentPhase.Tags));
            GlobalServices.EventBus.Publish(new HintEvent { Tag = "PhaseEnter", Context = _duelState, Mode = GameMode.Combat });
            Debug.Log($"[Phase] Resumed {currentPhase.PhaseId} without replaying phase-entry effects. Tags: {string.Join(", ", currentPhase.Tags)}");

            if (currentPhase.Tags.Contains("BuildingPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "BuildingPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await WaitForPlayerPhaseConfirmationAsync();
                if (_leaveDuelRequested || _duelState == null) return;
                await CheckAutoTransitionsAsync();
            }
            else if (currentPhase.Tags.Contains("PlanningPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "PlanningPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await WaitForPlayerPhaseConfirmationAsync();
                if (_leaveDuelRequested || _duelState == null) return;
                await CheckAutoTransitionsAsync();
            }
            else
            {
                await CheckAutoTransitionsAsync();
            }
        }

        private async UniTask WaitForPlayerPhaseConfirmationAsync()
        {
            Debug.Log("[Phase] Waiting for player confirmation...");
            _playerConfirmedPhase = false;
            _phaseConfirmationReady = true;

            while (!_playerConfirmedPhase)
            {
                if (_leaveDuelRequested || _duelState == null) return;

                if (_actionQueue.Count > 0)
                {
                    await ProcessActionsAsync();
                    if (_leaveDuelRequested || _duelState == null) return;
                }
                else
                {
                    await UniTask.Yield();
                }
            }

            _phaseConfirmationReady = false;
            Debug.Log("[Phase] Confirmed - advancing.");
        }

        private const int DraftPicksAllowed = 2;
        private const int DraftCandidateCount = 3;
        private const string DraftGuaranteedCardName = "Human";
        private int _draftInstanceIdCounter = -200000;

        /// <summary>
        /// One-shot override for the next draft's non-guaranteed slots. The tutorial uses this
        /// to make sure the player sees a Vampire candidate on the very first draft so the
        /// onboarding flow can teach Vanguard play.
        /// </summary>
        public List<string> PendingDraftCardNames { get; } = new List<string>();

        /// <summary>
        /// One-shot override making a specific card mandatory for the next draft. Cleared after use.
        /// </summary>
        public string PendingMandatoryDraftCardName { get; set; }

        /// <summary>
        /// One-shot override list for multiple mandatory cards in the next draft.
        /// </summary>
        public List<string> PendingMandatoryDraftCardNames { get; } = new List<string>();

        private async UniTask DraftPlayerHandAsync(SideState side, int picksAllowed)
        {
            if (side == null) return;

            var candidates = BuildDraftCandidates(side, out var candidateDeckCards);
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[Draft] No candidates available; skipping draft.");
                return;
            }

            var actualPicks = Mathf.Min(picksAllowed, candidates.Count);
            var ui = FindObjectOfType<CardSelectionUI>(true);
            if (ui == null)
            {
                Debug.LogWarning("[Draft] CardSelectionUI not found; falling back to auto-draw.");
                AutoApplyDraftFallback(side, candidates, candidateDeckCards, actualPicks);
                return;
            }

            // During the tutorial, hold the draft closed until the intro steps reach
            // the "draft" step. Until then the player should see only the board.
            var tutorial = FindObjectOfType<TutorialSystem>(true);
            if (tutorial != null)
            {
                while (!tutorial.IsReadyForDraft())
                {
                    if (_leaveDuelRequested || _duelState == null) return;
                    await UniTask.Yield();
                }
            }

            GlobalServices.EventBus.Publish(new HintEvent { Tag = "DraftPhaseEnter", Context = _duelState, Mode = GameMode.Combat });

            var mandatoryNames = new List<string>();
            if (!string.IsNullOrEmpty(PendingMandatoryDraftCardName))
            {
                mandatoryNames.Add(PendingMandatoryDraftCardName);
            }
            if (PendingMandatoryDraftCardNames.Count > 0)
            {
                mandatoryNames.AddRange(PendingMandatoryDraftCardNames);
            }
            PendingMandatoryDraftCardName = null;
            PendingMandatoryDraftCardNames.Clear();
            var chosenIndices = await ui.ShowDraftAsync(candidates, actualPicks, mandatoryNames);
            if (chosenIndices == null) return;

            ApplyDraftPicks(side, candidates, candidateDeckCards, chosenIndices);

            GlobalServices.EventBus.Publish(new HintEvent { Tag = "DraftCompleted", Context = _duelState, Mode = GameMode.Combat });
        }

        /// <summary>
        /// Builds the draft candidate list together with a parallel list of deck-backed Card
        /// instances. Slot 0 is always Human (guaranteed, no deck card — Human is the exception
        /// and never consumes the deck). Subsequent slots are sampled without replacement from
        /// the deck's undrawn portion; each backed slot will consume that specific Card when
        /// picked, so the deck actually shrinks turn over turn.
        /// </summary>
        private List<CardDef> BuildDraftCandidates(SideState side, out List<Card> candidateDeckCards)
        {
            var result = new List<CardDef>();
            candidateDeckCards = new List<Card>();

            var humanDef = CardDatabase.GetCard(DraftGuaranteedCardName);
            if (humanDef != null)
            {
                result.Add(humanDef);
                candidateDeckCards.Add(null);
            }
            else
            {
                Debug.LogWarning($"[Draft] Guaranteed draft card '{DraftGuaranteedCardName}' not found in CardDatabase.");
            }

            int randomSlots = DraftCandidateCount - result.Count;

            // Tutorial / scripted hook: forced names come first. They reserve a deck card of the
            // same def if one exists, so picking them still consumes the deck like a normal slot.
            while (randomSlots > 0 && PendingDraftCardNames.Count > 0)
            {
                var name = PendingDraftCardNames[0];
                PendingDraftCardNames.RemoveAt(0);
                if (string.IsNullOrEmpty(name)) continue;
                var def = CardDatabase.GetCard(name);
                if (def == null) continue;
                result.Add(def);
                candidateDeckCards.Add(FindUndrawnUnclaimed(side, def, candidateDeckCards));
                randomSlots--;
            }

            if (side.Deck != null && randomSlots > 0)
            {
                var pool = new List<Card>();
                foreach (var card in side.Deck.UndrawnCards)
                {
                    if (card?.Def == null) continue;
                    if (candidateDeckCards.Contains(card)) continue;
                    pool.Add(card);
                }

                var rng = new System.Random();
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }

                int taken = 0;
                foreach (var card in pool)
                {
                    if (taken >= randomSlots) break;
                    result.Add(card.Def);
                    candidateDeckCards.Add(card);
                    taken++;
                }
            }

            // Shuffle so the guaranteed Human card doesn't always appear in the same slot.
            var orderRng = new System.Random();
            for (int i = result.Count - 1; i > 0; i--)
            {
                int j = orderRng.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
                (candidateDeckCards[i], candidateDeckCards[j]) = (candidateDeckCards[j], candidateDeckCards[i]);
            }

            return result;
        }

        private Card FindUndrawnUnclaimed(SideState side, CardDef def, List<Card> alreadyClaimed)
        {
            if (side?.Deck == null || def == null) return null;
            foreach (var card in side.Deck.UndrawnCards)
            {
                if (card?.Def != def) continue;
                if (alreadyClaimed.Contains(card)) continue;
                return card;
            }
            return null;
        }

        private void ApplyDraftPicks(SideState side, List<CardDef> candidates, List<Card> candidateDeckCards, List<int> pickedIndices)
        {
            foreach (var index in pickedIndices)
            {
                if (index < 0 || index >= candidates.Count) continue;
                var def = candidates[index];
                if (def == null) continue;
                if (side.Hand.Count >= SideState.MaxHandSize)
                {
                    Debug.Log("[Draft] Hand full; ignoring further picks.");
                    break;
                }

                var deckCard = index < candidateDeckCards.Count ? candidateDeckCards[index] : null;
                if (deckCard != null && side.Deck.TakeSpecificUndrawn(deckCard))
                {
                    side.AddCardToHand(deckCard);
                }
                else
                {
                    // Guaranteed Human slot, or the deck card disappeared mid-draft — fall back
                    // to a fresh synthetic instance so the player still gets the card they picked.
                    side.AddCardToHand(new Card(def, _draftInstanceIdCounter--));
                }
            }
        }

        private void AutoApplyDraftFallback(SideState side, List<CardDef> candidates, List<Card> candidateDeckCards, int picksAllowed)
        {
            var indices = new List<int>();
            for (int i = 0; i < candidates.Count && indices.Count < picksAllowed; i++)
            {
                indices.Add(i);
            }
            ApplyDraftPicks(side, candidates, candidateDeckCards, indices);
        }

        private async UniTask ReturnToSeatViewForPlayerTurnAsync()
        {
            var switcher = Camera.main?.GetComponent<DuelCameraSwitcher>();
            if (switcher != null)
            {
                switcher.SetPerspectiveSwitchingEnabled(true);
                await switcher.FocusSeatViewAsync(0.3f);
            }
        }

        private async UniTask RunBoardViewResolutionAsync(Func<UniTask> resolveAsync)
        {
            var switcher = Camera.main?.GetComponent<DuelCameraSwitcher>();
            if (switcher != null)
            {
                switcher.SetPerspectiveSwitchingEnabled(false);
                await switcher.FocusBoardViewAsync(0.38f, true);
            }

            try
            {
                if (resolveAsync != null)
                    await resolveAsync();
            }
            finally
            {
                switcher?.SetBoardViewLocked(false);
            }
        }


        public void QueueAction(IGameAction action)
        {
            if (action == null)
            {
                Debug.LogWarning("[DuelManager] QueueAction called with null - ignored. Возможно у CardCost не назначен _payAction в инспекторе.");
                return;
            }
            _actionQueue.Enqueue(action);
        }

        public void RequestProcessQueuedActions()
        {
            if (_actionQueue.Count == 0) return;
            ProcessActionsAsync().Forget();
        }

        private async UniTask ProcessActionsAsync()
        {
            if (_processingQueuedActions) return;
            _processingQueuedActions = true;
            try
            {
                while (_actionQueue.Count > 0)
                {
                if (_state == CombatState.Paused) break;
                var action = _actionQueue.Dequeue();
                if (action == null)
                {
                    Debug.LogWarning("[DuelManager] Skipping null action in queue");
                    continue;
                }
                Debug.Log($"[DuelManager] Processing action: {action.Description}");
                Debug.Log($"[DuelDebug] Before action '{action.Description}': {DescribeDuelDebugState()}");
                try
                {
                    await action.ExecuteAsync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DuelManager] Action failed: {action.Description}\n{e}");
                }
                await CombatVFX.AwaitCurrentActionAnimationsAsync();
                RemoveDeadNonTownCardsFromBoards();
                Debug.Log($"[DuelDebug] After action '{action.Description}' and cleanup: {DescribeDuelDebugState()}");
                GlobalServices.EventBus.Publish(new ActionExecutedEvent(action));

                bool terminalAfterAction = IsDuelTerminal();
                Debug.Log($"[DuelDebug] Terminal check after action '{action.Description}' => {terminalAfterAction}; {DescribeDuelDebugState()}");
                    if (terminalAfterAction)
                    {
                        CaptureDuelOutcomeIfFinished();
                        await TransitionToOutcomePhaseAsync();
                        return;
                    }
                }
            }
            finally
            {
                _processingQueuedActions = false;
            }
        }

        private async UniTask TransitionToPhaseAsync(PhaseNode targetNode)
        {
            if (targetNode == null)
            {
                Debug.LogError("TransitionToPhaseAsync: targetNode is null");
                return;
            }

            var previousPhase = _duelState.CurrentPhase;
            Debug.Log($"[DuelDebug] TransitionToPhaseAsync requested: {DescribePhase(previousPhase)} -> {DescribePhase(targetNode)}; {DescribeDuelDebugState()}");
            if (previousPhase != null)
                GlobalServices.EventBus.Publish(new PhaseExitEvent(previousPhase.PhaseId));

            _duelState.CurrentPhase = targetNode;
            GlobalServices.EventBus.Publish(new PhaseEnterEvent(targetNode.PhaseId, targetNode.Tags));
            GlobalServices.EventBus.Publish(new HintEvent { Tag = "PhaseEnter", Context = _duelState, Mode = GameMode.Combat });
            Debug.Log($"[Phase] Entered {targetNode.PhaseId} with tags: {string.Join(", ", targetNode.Tags)}");
            Debug.Log($"[DuelDebug] Entered phase details: transitions=[{DescribeTransitions(targetNode)}]; {DescribeDuelDebugState()}");
            _phaseConfirmationReady = false;

            if (targetNode.Tags.Contains("DuelStart"))
            {
                // Players now start with an empty hand. The first hand comes from the draft on
                // StartOfTurn so the player can actually choose what they want to keep.
                // Opponent also starts empty; they auto-draw on StartOfTurn to match the draft pick count.
                Debug.Log("[Phase] DuelStart: both hands begin empty; first draw happens at StartOfTurn.");
            }
            else if (targetNode.Tags.Contains("StartOfTurn"))
            {
                Debug.Log($"[DuelDebug] StartOfTurn branch entered. If this appears after a terminal town state, the fault is before/inside terminal routing. {DescribeDuelDebugState()}");
                await ReturnToSeatViewForPlayerTurnAsync();
                if (_leaveDuelRequested || _duelState == null) return;
                SpawnOnFriendlyDeathAction.ResetRoundTracking();
                QueueAction(new RegenerateHumanResourcesAction(_duelState.PlayerSide));
                QueueAction(new RegenerateHumanResourcesAction(_duelState.OpponentSide));

                QueueAction(new ResetBuildingDamageAction(_duelState.PlayerSide.Board));
                QueueAction(new ResetBuildingDamageAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                Debug.Log("[Phase] Draft phase for player + auto-draw for opponent...");
                QueueAction(new DrawWithGuaranteedHumanAction(_duelState.OpponentSide, DraftPicksAllowed));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                await DraftPlayerHandAsync(_duelState.PlayerSide, DraftPicksAllowed);
                if (_leaveDuelRequested || _duelState == null) return;

                Debug.Log($"[Phase] Player hand: {_duelState.PlayerSide.Hand.Count}, Opponent hand: {_duelState.OpponentSide.Hand.Count}");
            }
            else if (targetNode.Tags.Contains("BuildingPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "BuildingPhaseEnter", Context = _duelState, Mode = GameMode.Combat });

                await ExecuteOpponentBuildingTurnAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                await WaitForPlayerPhaseConfirmationAsync();
                if (_leaveDuelRequested || _duelState == null) return;
            }
            else if (targetNode.Tags.Contains("PlanningPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "PlanningPhaseEnter", Context = _duelState, Mode = GameMode.Combat });

                QueueAction(new RollSpeedAction(_duelState.PlayerSide.Board));
                QueueAction(new RollSpeedAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                await SimulatePlanningAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                await WaitForPlayerPhaseConfirmationAsync();
                if (_leaveDuelRequested || _duelState == null) return;
            }
            else if (targetNode.Tags.Contains("ClashingPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "ClashingPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await RunBoardViewResolutionAsync(ResolveClashesAsync);
                if (_leaveDuelRequested || _duelState == null) return;
            }
            else if (targetNode.Tags.Contains("OneSidedAttackPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "OneSidedAttackPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await RunBoardViewResolutionAsync(ResolveOneSidedAttacksAsync);
                if (_leaveDuelRequested || _duelState == null) return;
                ClearAllPlannedTargets();
            }
            else if (targetNode.Tags.Contains("EndOfTurn"))
            {
                QueueAction(new BuildingDestructionCheckAction(_duelState.PlayerSide.Board));
                QueueAction(new BuildingDestructionCheckAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                if (IsDuelTerminal())
                {
                    CaptureDuelOutcomeIfFinished();
                    await TransitionToOutcomePhaseAsync();
                    return;
                }
            }
            else if (targetNode.Tags.Contains("Loot"))
            {
                Debug.LogError($"[DEBUG] Loot phase entered. Outcome={_duelOutcome}");

                await ShowLootSelectionAsync();
                await ReturnToExplorationAsync();
                return;
            }

            else if (targetNode.Tags.Contains("DuelEnd"))
            {
                Debug.Log("[Phase] DuelEnd – returning to exploration.");
                await ReturnToExplorationAsync();
                return;
            }

            await CheckAutoTransitionsAsync();
        }

        public async UniTask<bool> TransitionToPhaseWithTagAsync(string triggerTag)
        {
            var node = _duelState.CurrentPhase;
            Debug.Log($"[DuelDebug] TransitionToPhaseWithTagAsync('{triggerTag}') from {DescribePhase(node)}. transitions=[{DescribeTransitions(node)}]; {DescribeDuelDebugState()}");
            if (node == null) return false;
            foreach (var trans in node.Transitions)
            {
                if (trans?.Condition == null) continue;
                if (trans.Condition.Type == ConditionType.TagActive &&
                    trans.Condition.TagTrigger == triggerTag)
                {
                    Debug.Log($"[DuelDebug] Matched terminal tag '{triggerTag}' -> {DescribePhase(trans.Target)}");
                    await TransitionToPhaseAsync(trans.Target);
                    return true;
                }
            }

            Debug.LogWarning($"[DuelDebug] No transition matched tag '{triggerTag}' from {DescribePhase(node)}. transitions=[{DescribeTransitions(node)}]");
            return false;
        }

        private bool IsDuelTerminal()
        {
            if (_duelState == null || _encounter == null)
            {
                Debug.Log($"[DuelDebug] IsDuelTerminal => false because state or encounter is missing. stateNull={_duelState == null}; encounterNull={_encounter == null}");
                return false;
            }

            // Town death is the authoritative terminal state for this duel outcome policy.
            // The WinCondition asset/ID remains supported for non-town terminal rules, but
            // tutorial encounters must not get stuck if that lookup/reference is absent.
            var townOutcome = EvaluateDuelOutcome();
            if (townOutcome != DuelOutcome.InProgress)
            {
                Debug.Log($"[DuelDebug] IsDuelTerminal => true from town outcome {townOutcome}. {DescribeDuelDebugState()}");
                return true;
            }

            var winCondition = _encounter.WinCondition ?? WinConditionDatabase.GetWinCondition(_encounter.WinConditionId);
            bool winConditionResult = winCondition?.Check(_duelState) == true;
            Debug.Log($"[DuelDebug] IsDuelTerminal => {winConditionResult} from win condition. conditionId='{_encounter.WinConditionId}'; hasCondition={winCondition != null}; {DescribeDuelDebugState()}");
            return winConditionResult;
        }

        private DuelOutcome EvaluateDuelOutcome()
        {
            if (_duelState == null)
                return DuelOutcome.InProgress;

            // A missing town occupant is also terminal: some board paths remove dead occupants,
            // so null here means destroyed, not merely "unknown", once DuelState exists.
            bool playerAlive = _duelState.PlayerTown?.IsAlive == true;
            bool opponentAlive = _duelState.OpponentTown?.IsAlive == true;

            if (playerAlive && opponentAlive) return DuelOutcome.InProgress;
            if (playerAlive && !opponentAlive) return DuelOutcome.PlayerWon;
            if (!playerAlive && opponentAlive) return DuelOutcome.PlayerLost;
            return DuelOutcome.Draw;
        }

        private void CaptureDuelOutcomeIfFinished()
        {
            if (_duelOutcome != DuelOutcome.InProgress)
            {
                Debug.Log($"[DuelDebug] CaptureDuelOutcomeIfFinished skipped; already captured {_duelOutcome}. {DescribeDuelDebugState()}");
                return;
            }

            _duelOutcome = EvaluateDuelOutcome();
            _duelFinished = _duelOutcome != DuelOutcome.InProgress;
            Debug.Log($"[DuelDebug] CaptureDuelOutcomeIfFinished captured outcome={_duelOutcome}; finished={_duelFinished}; {DescribeDuelDebugState()}");
            PublishDuelResultIfNeeded();
        }

        private void PublishDuelResultIfNeeded()
        {
            if (_duelResultPublished || !_duelFinished || _encounter == null) return;

            bool playerWon = _duelOutcome == DuelOutcome.PlayerWon;
            GlobalServices.EventBus.Publish(new DuelResultEvent
            {
                PlayerWon = playerWon,
                Outcome = _duelOutcome,
                EncounterId = _encounter.EncounterId,
                WinFlag = _encounter.WinFlag,
                LoseFlag = _encounter.LoseFlag
            });
            _duelResultPublished = true;

            if (!string.IsNullOrEmpty(_tableId))
                GlobalServices.SaveSystem.ClearActiveBattle(_tableId);

            Debug.Log($"[DuelManager] Published duel result: {_duelOutcome} for encounter {_encounter.EncounterId}");
        }

        private async UniTask TransitionToOutcomePhaseAsync()
        {
            // Terminal outcomes should enter the phase graph's terminal branch before Combat exits.
            // EndOfTurn has both the normal TurnStart transition and a terminal TagActive transition;
            // choose the terminal tag explicitly so the default transition cannot win by list order.
            Debug.Log($"[DuelDebug] TransitionToOutcomePhaseAsync entered. {DescribeDuelDebugState()}");
            CompleteTutorialIfTerminalDuel();

            string triggerTag = _duelOutcome == DuelOutcome.PlayerLost ? "Defeat" : "WinConditionMet";
            Debug.Log($"[DuelDebug] Trying terminal transition tag '{triggerTag}' for outcome {_duelOutcome}.");
            if (await TransitionToPhaseWithTagAsync(triggerTag))
            {
                Debug.Log($"[DuelDebug] Terminal transition tag '{triggerTag}' succeeded.");
                return;
            }

            if (triggerTag != "WinConditionMet")
            {
                Debug.Log("[DuelDebug] Primary terminal tag failed; trying fallback 'WinConditionMet'.");
                if (await TransitionToPhaseWithTagAsync("WinConditionMet"))
                {
                    Debug.Log("[DuelDebug] Fallback terminal transition tag 'WinConditionMet' succeeded.");
                    return;
                }
            }

            Debug.LogWarning($"[DuelManager] No terminal phase transition found for outcome {_duelOutcome}. Returning to exploration immediately. {DescribeDuelDebugState()}");
            await ReturnToExplorationAsync();
        }

        private void CompleteTutorialIfTerminalDuel()
        {
            var tutorial = FindObjectOfType<TutorialSystem>(true);
            Debug.Log($"[DuelDebug] CompleteTutorialIfTerminalDuel: tutorialFound={tutorial != null}.");
            tutorial?.CompleteTutorialForTerminalDuel();
        }

        private async UniTask ReturnToExplorationAsync()
        {
            Debug.Log($"[DuelDebug] ReturnToExplorationAsync requested. leaveAlreadyRequested={_leaveDuelRequested}; {DescribeDuelDebugState()}");
            if (_leaveDuelRequested) return;

            if (_duelOutcome == DuelOutcome.PlayerLost)
            {
                var victoryScreen = FindObjectOfType<VictoryScreen>(true);
                if (victoryScreen != null)
                {
                    await victoryScreen.ShowDefeatAsync();
                }
            }
            
            var director = ResolveGameDirector();
            Debug.Log($"[DuelDebug] ReturnToExplorationAsync directorFound={director != null}.");
            if (director == null)
            {
                Debug.LogWarning("[DuelManager] Cannot return to exploration: GameDirector service is not available.");
                return;
            }

            _leaveDuelRequested = true;
            await director.PopModeAsync();
        }

        private async UniTask CheckAutoTransitionsAsync()
        {
            var node = _duelState?.CurrentPhase;
            Debug.Log($"[DuelDebug] CheckAutoTransitionsAsync entered from {DescribePhase(node)}. transitions=[{DescribeTransitions(node)}]; {DescribeDuelDebugState()}");
            if (node?.Transitions == null) return;

            bool terminalBeforeDefault = IsDuelTerminal();
            Debug.Log($"[DuelDebug] CheckAutoTransitionsAsync terminal-before-default => {terminalBeforeDefault}; {DescribeDuelDebugState()}");
            if (terminalBeforeDefault)
            {
                CaptureDuelOutcomeIfFinished();
                await TransitionToOutcomePhaseAsync();
                return;
            }

            foreach (var trans in node.Transitions)
            {
                if (trans == null || trans.Target == null) continue;
                if (trans.Condition == null)
                {
                    Debug.LogWarning($"[DuelDebug] Skipping transition with null condition from {DescribePhase(node)} to {DescribePhase(trans.Target)}.");
                    continue;
                }
                Debug.Log($"[DuelDebug] Considering auto transition {DescribePhase(node)} -> {DescribePhase(trans.Target)}; condition={trans.Condition.Type}; tag='{trans.Condition.TagTrigger}'");
                if (trans.Condition.Type != ConditionType.None) continue;
                if (trans.Target == node) continue;

                Debug.Log($"[DuelDebug] Taking default auto transition {DescribePhase(node)} -> {DescribePhase(trans.Target)}.");
                await TransitionToPhaseAsync(trans.Target);
                return;
            }

            Debug.Log($"[DuelDebug] No auto transition taken from {DescribePhase(node)}.");
        }

        private string DescribeDuelDebugState()
        {
            if (_duelState == null)
                return $"state=null; capturedOutcome={_duelOutcome}; finished={_duelFinished}; resultPublished={_duelResultPublished}; leaveRequested={_leaveDuelRequested}";

            return $"phase={DescribePhase(_duelState.CurrentPhase)}; playerTown={DescribeTown(_duelState.PlayerTown)}; opponentTown={DescribeTown(_duelState.OpponentTown)}; townOutcome={EvaluateDuelOutcome()}; capturedOutcome={_duelOutcome}; finished={_duelFinished}; resultPublished={_duelResultPublished}; queue={_actionQueue.Count}; leaveRequested={_leaveDuelRequested}";
        }

        private string DescribePhase(PhaseNode phase)
        {
            if (phase == null) return "<null phase>";
            return $"{phase.PhaseId}[{string.Join(",", phase.Tags ?? new List<string>())}]";
        }

        private string DescribeTransitions(PhaseNode node)
        {
            if (node?.Transitions == null) return "<none>";
            return string.Join(" | ", node.Transitions.Select((trans, index) =>
            {
                if (trans == null) return $"#{index}:<null>";
                var condition = trans.Condition;
                string conditionText = condition == null
                    ? "<null condition>"
                    : $"{condition.Type}:tag='{condition.TagTrigger}' threshold={condition.Threshold}";
                return $"#{index}:{conditionText}->{DescribePhase(trans.Target)}";
            }));
        }

        private string DescribeTown(IGameEntity entity)
        {
            if (entity == null) return "<null>";
            if (entity is BoardCard card)
                return $"{card.SourceCard?.CardName ?? card.SourceCard?.name ?? "<unnamed>"}#id={card.Id} hp={card.Health}/{card.MaxHealth} atk={card.Attack} alive={card.IsAlive}";
            return entity.ToString();
        }

        private async UniTask ResolveClashesAsync()
        {
            var attackers = GetAllAttackers()
                .OrderByDescending(a => a.CurrentSpeed)
                .ToList();

            var resolved = new HashSet<BoardCard>();

            foreach (var card in attackers)
            {
                if (resolved.Contains(card)) continue;

                var target = card.PlannedTarget as BoardCard;
                if (target == null || !target.IsAlive || resolved.Contains(target)) continue;

                if (target.PlannedTarget == card)
                {
                    resolved.Add(card);
                    resolved.Add(target);

                    var forceA = GetClashForce(card);
                    var forceB = GetClashForce(target);

                    bool cardWins = false;
                    bool targetWins = false;
                    bool mutualLoss = false;

                    if (forceA == ClashForce.Win && forceB == ClashForce.Win)
                    {
                        mutualLoss = true;
                    }
                    else if (forceA == ClashForce.Win)
                    {
                        cardWins = true;
                    }
                    else if (forceB == ClashForce.Win)
                    {
                        targetWins = true;
                    }
                    else if (forceA == ClashForce.Lose && forceB == ClashForce.Lose)
                    {
                        mutualLoss = true;
                    }
                    else if (forceA == ClashForce.Lose)
                    {
                        targetWins = true;
                    }
                    else if (forceB == ClashForce.Lose)
                    {
                        cardWins = true;
                    }
                    else
                    {
                        int powerA = GetClashAttack(card);
                        int powerB = GetClashAttack(target);
                        if (powerA > powerB)
                            cardWins = true;
                        else if (powerB > powerA)
                            targetWins = true;
                        else
                            mutualLoss = true;
                    }

                    if (mutualLoss)
                    {
                        card.PlannedTarget = null;
                        target.PlannedTarget = null;
                    }
                    else if (cardWins)
                    {
                        QueueAction(new DamageAction(target, card.Attack, card));
                        RecordGourmetTarget(card, target);
                        QueueRitualistSacrificeIfApplicable(card);
                        target.PlannedTarget = null;
                        GlobalServices.EventBus.Publish(new ClashResolvedEvent(card, target));
                    }
                    else if (targetWins)
                    {
                        QueueAction(new DamageAction(card, target.Attack, target));
                        RecordGourmetTarget(target, card);
                        QueueRitualistSacrificeIfApplicable(target);
                        card.PlannedTarget = null;
                        GlobalServices.EventBus.Publish(new ClashResolvedEvent(target, card));
                    }
                }
            }
            await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
        }

        private void QueueDoubleAttackIfApplicable(BoardCard attacker, IGameEntity target)
        {
            // VampireLoner's old "double attack when alone" rule is gone — it's Elusive now.
        }

        private static void RecordGourmetTarget(BoardCard attacker, IGameEntity target)
        {
            if (!CardBehaviorTags.NeverRepeatsTarget(attacker) || target == null) return;
            if (target is BoardCard bc)
            {
                if (bc.IsTown) attacker.AttackedTownBefore = true;
                else attacker.PreviouslyAttackedCardIds.Add(bc.Id);
            }
        }

        public static bool CanAttackerTarget(BoardCard attacker, IGameEntity target)
        {
            if (attacker == null || target == null) return false;
            if (target is BoardCard bc)
            {
                if (CardBehaviorTags.IsElusive(bc) && IsLonerAloneOnHisVanguard(bc)) return false;
                if (CardBehaviorTags.NeverRepeatsTarget(attacker))
                {
                    if (bc.IsTown && attacker.AttackedTownBefore) return false;
                    if (!bc.IsTown && attacker.PreviouslyAttackedCardIds.Contains(bc.Id)) return false;
                }

                // Buildings act as a wall: each living building covers two Humans behind it
                // (building index 0 → humans 0,1; building index 1 → humans 2,3). The Town can
                // only be reached when at least one Building slot is empty/destroyed.
                var defenderSide = FindSideOfCard(bc);
                if (defenderSide != null && IsTargetShieldedByBuildings(bc, defenderSide))
                    return false;
            }
            return true;
        }

        public static bool IsTargetShieldedByBuildings(BoardCard target, SideState defenderSide)
        {
            if (target == null || defenderSide?.Board == null) return false;
            var buildingRow = defenderSide.Board.BuildingRow;
            int aliveBuildings = buildingRow == null ? 0 : buildingRow.Count(s => s?.Occupant != null && s.Occupant.IsAlive);

            if (target.IsTown)
                return aliveBuildings >= 1;

            var humanRow = defenderSide.Board.HumanRow;
            if (humanRow == null) return false;
            int humanIndex = -1;
            for (int i = 0; i < humanRow.Length; i++)
                if (humanRow[i]?.Occupant == target) { humanIndex = i; break; }
            if (humanIndex < 0) return false;

            // Each building covers two Humans behind it.
            int coveringBuildingIndex = humanIndex / 2;
            if (buildingRow != null && coveringBuildingIndex < buildingRow.Length)
            {
                var shield = buildingRow[coveringBuildingIndex]?.Occupant;
                if (shield != null && shield.IsAlive) return true;
            }
            return false;
        }

        private static SideState FindSideOfCard(BoardCard card)
        {
            var dm = UnityEngine.Object.FindObjectOfType<DuelManager>();
            var state = dm?._duelState;
            if (state == null || card == null) return null;
            if (state.PlayerSide.Board.AllSlots().Any(s => s.Occupant == card)) return state.PlayerSide;
            if (state.OpponentSide.Board.AllSlots().Any(s => s.Occupant == card)) return state.OpponentSide;
            return null;
        }

        // VampireLoner's Elusive only activates when no other allied vanguard cards are alive
        // alongside him. As soon as the player puts a teammate next to him, the loner is fair game.
        private static bool IsLonerAloneOnHisVanguard(BoardCard loner)
        {
            var side = FindSideOfCard(loner);
            if (side == null) return false;
            int alliesAlive = 0;
            foreach (var slot in side.Board.VanguardRow)
            {
                var occ = slot?.Occupant;
                if (occ == null || !occ.IsAlive) continue;
                if (occ == loner) continue;
                alliesAlive++;
            }
            return alliesAlive == 0;
        }

        private void QueueRitualistSacrificeIfApplicable(BoardCard attacker)
        {
            if (!CardBehaviorTags.DiesAfterAttacking(attacker)) return;
            var side = SideLookup.FindSideOf(attacker, _duelState);
            if (side == null) return;
            QueueAction(new SacrificeAction(attacker, side.Board));
        }

        private async UniTask ResolveOneSidedAttacksAsync()
        {
            var attackers = GetAllAttackers()
                        .Where(a => a.PlannedTarget != null && a.PlannedTarget.IsAlive)
                        .OrderByDescending(a => a.CurrentSpeed);

            foreach (var card in attackers)
            {
                QueueAction(new DamageAction(card.PlannedTarget, card.Attack, card));
                RecordGourmetTarget(card, card.PlannedTarget);
                QueueRitualistSacrificeIfApplicable(card);
            }
            await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
        }

        private IEnumerable<BoardCard> GetAllAttackers()
        {
            var all = new List<BoardCard>();
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
                foreach (var slot in side.Board.AllSlots())
                    if (IsAttackCapable(slot.Occupant) && slot.Occupant.PlannedTarget != null)
                        all.Add(slot.Occupant);
            return all;
        }

        private void ClearAllPlannedTargets()
        {
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
                foreach (var slot in side.Board.AllSlots())
                    if (slot.Occupant != null)
                        slot.Occupant.PlannedTarget = null;
        }

        private void RemoveDeadNonTownCardsFromBoards()
        {
            if (_duelState == null) return;

            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
            {
                var board = side?.Board;
                if (board == null) continue;

                var deadCards = board.AllSlots()
                    .Select(slot => slot?.Occupant)
                    .Where(card => card != null && !card.IsTown && !card.IsAlive)
                    .Distinct()
                    .ToList();

                foreach (var deadCard in deadCards)
                {
                    ClearPlannedTargetsPointingTo(deadCard);
                    board.RemoveCard(deadCard);
                    Debug.Log($"[DuelManager] Removed defeated card from board: {deadCard.SourceCard?.CardName ?? deadCard.Id.ToString()}");
                }
            }
        }

        private void ClearPlannedTargetsPointingTo(BoardCard removedCard)
        {
            if (_duelState == null || removedCard == null) return;

            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
            {
                var board = side?.Board;
                if (board == null) continue;

                foreach (var slot in board.AllSlots())
                {
                    if (slot?.Occupant != null && slot.Occupant.PlannedTarget == removedCard)
                    {
                        slot.Occupant.PlannedTarget = null;
                    }
                }
            }
        }

        private static bool IsAttackCapable(BoardCard card)
        {
            return card != null && card.IsAlive && card.Attack > 0;
        }

        private enum ClashForce { None, Win, Lose }

        private ClashForce GetClashForce(BoardCard card)
        {
            bool hasWin = false;
            bool hasLose = false;
            foreach (var enchantment in card.Enchantments)
            {
                foreach (var mod in enchantment.Data.Modifiers)
                {
                    if (mod.Stat == "ClashForce")
                    {
                        if (mod.Value == 1) hasWin = true;
                        else if (mod.Value == 2) hasLose = true;
                    }
                }
            }
            if (hasWin) return ClashForce.Win;
            if (hasLose) return ClashForce.Lose;
            return ClashForce.None;
        }

        private int GetClashAttack(BoardCard card)
        {
            int clashAttack = card.Attack;
            foreach (var enchantment in card.Enchantments)
            {
                foreach (var mod in enchantment.Data.Modifiers)
                {
                    if (mod.Stat == "ClashAttack" && mod.Type == ModifierType.Add)
                        clashAttack += mod.Value;
                }
            }
            return clashAttack;
        }

        private async UniTask ExecuteOpponentBuildingTurnAsync()
        {
            if (_opponentAI == null)
            {
                Debug.LogWarning("[AI] OpponentAI is null, skipping opponent building turn");
                return;
            }

            var enemySide = _duelState.OpponentSide;
            const int maxIterations = 16;

            for (int i = 0; i < maxIterations; i++)
            {
                var decision = _opponentAI.DecideCardToPlay(enemySide, _duelState.PlayerSide);
                if (decision == null) break;

                foreach (var cost in decision.Card.Def.Costs)
                {
                    ICostContext ctx = cost is SacrificeCost sacrificeCost
                        ? new SacrificeCostContext
                        {
                            PlayerSide = enemySide,
                            Amount = sacrificeCost.Amount,
                            Cost = sacrificeCost
                        }
                        : new CostContext { PlayerSide = enemySide, Amount = cost.GetAmount() };
                    QueueAction(cost.GetPaymentAction(ctx));
                }

                // Don't pre-remove from hand here: PlaceCardIntoSlotAction removes the card by Def
                // after a successful placement. Removing first risked deleting a second copy of the
                // same Def, leaving the AI with an empty hand after a couple of turns.
                QueueAction(new PlaceCardIntoSlotAction(enemySide.Board, decision.Card.Def, decision.TargetSlot));
                Debug.Log($"[AI] Opponent plays {decision.Card.Def.CardName} in {decision.Card.Def.RowType}[{decision.TargetSlot.Index}]");

                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
            }
        }

        private async UniTask SimulatePlanningAsync()
        {
            var enemyAttackers = _duelState.OpponentSide.Board.AllSlots()
                .Where(s => IsAttackCapable(s.Occupant))
                .Select(s => s.Occupant)
                .ToArray();

            foreach (var card in enemyAttackers)
            {
                if (card == null || !card.IsAlive) continue;

                var target = _opponentAI?.DecideAttackTarget(card, _duelState.PlayerSide);
                if (target != null)
                {
                    card.PlannedTarget = target;
                    Debug.Log($"[AI] {card.SourceCard.CardName} targets {(target as BoardCard)?.SourceCard.CardName ?? "Town"}");
                }
            }

            await UniTask.Yield();
        }
        private async UniTask ShowLootSelectionAsync()
        {
            CaptureDuelOutcomeIfFinished();
            if (_duelOutcome != DuelOutcome.PlayerWon)
                return;

            var rewardPool = _encounter.RewardCardPool;
            if (rewardPool == null || rewardPool.Count < 3)
            {
                Debug.LogError("RewardCardPool must contain at least 3 cards.");
                return;
            }

            var rng = new System.Random();
            var selected = rewardPool.OrderBy(x => rng.Next()).Take(3).ToList();

            var cardSelectionUI = FindObjectOfType<CardSelectionUI>(true);
            if (cardSelectionUI == null)
            {
                Debug.LogError("CardSelectionUI not found in scene.");
                return;
            }
            CardDef chosen = await cardSelectionUI.ShowAsync(selected);

            if (_playerPersistentDeck != null)
            {
                _playerPersistentDeck.Cards.Add(chosen);
                if (GlobalServices.PlayerData != null && _playerPersistentDeck != null)
                {

                    var cardIds = _playerPersistentDeck.Cards
                        .Select(c => c.CardName)
                        .ToList();

                    GlobalServices.PlayerData.ActiveDeckCardIds = cardIds;

                    var saveSystem = GlobalServices.SaveSystem;
                    if (saveSystem != null)
                    {
                        string json = JsonUtility.ToJson(GlobalServices.PlayerData);
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        await saveSystem.SaveAsync("playerdata.json", bytes);
                        Debug.Log($"[Loot] Колода сохранена: {cardIds.Count} карт");
                    }
                    else
                    {
                        Debug.LogError("[Loot] SaveSystem недоступен!");
                    }
                }
            }
            else
            {
                Debug.LogError("PlayerPersistentDeck not found in DuelManager.");
            }
        }

        private void DetachAllEnchantments(SideState side)
        {
            foreach (var slot in side.Board.VanguardRow)
                if (slot.Occupant != null)
                    foreach (var e in slot.Occupant.Enchantments) e.OnDetach();
            foreach (var slot in side.Board.BuildingRow)
                if (slot.Occupant != null)
                    foreach (var e in slot.Occupant.Enchantments) e.OnDetach();
            foreach (var slot in side.Board.HumanRow)
                if (slot.Occupant != null)
                    foreach (var e in slot.Occupant.Enchantments) e.OnDetach();
            if (side.Board.TownSlot.Occupant != null)
                foreach (var e in side.Board.TownSlot.Occupant.Enchantments) e.OnDetach();
        }
            
        public void SaveCurrentDuel()
        {
            CaptureDuelOutcomeIfFinished();
            if (_duelState != null && _encounter != null && _duelOutcome == DuelOutcome.InProgress)
            {
                var dto = MatchStateDTO.FromDuelState(_duelState);
                string json = JsonUtility.ToJson(dto);
                GlobalServices.SaveSystem.SaveActiveBattle(_tableId, json);
                Debug.Log($"[DuelManager] Duel saved for table {_tableId}");
            }
        }

        private enum CombatState
        {
            PlayerTurnIdle,
            Animating,
            Paused
        }
    }
}
