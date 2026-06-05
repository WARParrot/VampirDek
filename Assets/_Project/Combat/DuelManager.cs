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

        public DeckData _playerPersistentDeck;
        public DuelState CurrentDuelState => _duelState;
        public string TableId => _tableId;
        public bool LoadDuelScene = false;
        private bool _playerConfirmedPhase;
        private DuelOutcome _duelOutcome = DuelOutcome.InProgress;
        private bool _duelFinished = false;
        private GameDirector _director;

        // AI System
        private OpponentAI _opponentAI;

        public void ConfirmCurrentPhase()
        {
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

            if (_duelFinished && _encounter != null)
            {
                bool playerWon = _duelOutcome == DuelOutcome.PlayerWon;
                GlobalServices.EventBus.Publish(new DuelResultEvent
                {
                    PlayerWon = playerWon,
                    Outcome = _duelOutcome,
                    EncounterId = _encounter.EncounterId,
                    WinFlag = _encounter.WinFlag,
                    LoseFlag = _encounter.LoseFlag
                });
            }

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

            ResumeCurrentPhaseWithoutEntryEffects();

            boardView?.RefreshAllSlots();
            handUI?.RefreshHandImmediately();
        }

        private void ResumeCurrentPhaseWithoutEntryEffects()
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

        private async UniTask ProcessActionsAsync()
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
                try
                {
                    await action.ExecuteAsync();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DuelManager] Action failed: {action.Description}\n{e}");
                }
                RemoveDeadNonTownCardsFromBoards();
                GlobalServices.EventBus.Publish(new ActionExecutedEvent(action));

                if (IsDuelTerminal())
                {
                    CaptureDuelOutcomeIfFinished();
                    await TransitionToOutcomePhaseAsync();
                    return;
                }
            }
        }

        private async UniTask TransitionToPhaseAsync(PhaseNode targetNode)
        {
            if (targetNode == null)
            {
                Debug.LogError("TransitionToPhaseAsync: targetNode is null");
                return;
            }

            if (_duelState.CurrentPhase != null)
                GlobalServices.EventBus.Publish(new PhaseExitEvent(_duelState.CurrentPhase.PhaseId));

            _duelState.CurrentPhase = targetNode;
            GlobalServices.EventBus.Publish(new PhaseEnterEvent(targetNode.PhaseId, targetNode.Tags));
            GlobalServices.EventBus.Publish(new HintEvent { Tag = "PhaseEnter", Context = _duelState, Mode = GameMode.Combat });
            Debug.Log($"[Phase] Entered {targetNode.PhaseId} with tags: {string.Join(", ", targetNode.Tags)}");

            if (targetNode.Tags.Contains("DuelStart"))
            {
                Debug.Log("[Phase] Drawing starting cards...");
                QueueAction(new DrawCardsAction(_duelState.PlayerSide, 2));
                QueueAction(new DrawCardsAction(_duelState.OpponentSide, 2));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
                Debug.Log($"[Phase] Player hand: {_duelState.PlayerSide.Hand.Count}, Opponent hand: {_duelState.OpponentSide.Hand.Count}");
            }
            else if (targetNode.Tags.Contains("StartOfTurn"))
            {
                QueueAction(new RegenerateHumanResourcesAction(_duelState.PlayerSide));
                QueueAction(new RegenerateHumanResourcesAction(_duelState.OpponentSide));

                QueueAction(new ResetBuildingDamageAction(_duelState.PlayerSide.Board));
                QueueAction(new ResetBuildingDamageAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                Debug.Log("[Phase] Drawing turn cards...");
                QueueAction(new DrawCardsAction(_duelState.PlayerSide, 1));
                QueueAction(new DrawCardsAction(_duelState.OpponentSide, 1));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
                Debug.Log($"[Phase] Player hand: {_duelState.PlayerSide.Hand.Count}, Opponent hand: {_duelState.OpponentSide.Hand.Count}");
            }
            else if (targetNode.Tags.Contains("BuildingPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "BuildingPhaseEnter", Context = _duelState, Mode = GameMode.Combat });

                await ExecuteOpponentBuildingTurnAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                Debug.Log("[Phase] Waiting for player confirmation...");
                _playerConfirmedPhase = false;
                while (!_playerConfirmedPhase)
                {
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
                Debug.Log("[Phase] Confirmed - advancing.");
            }
            else if (targetNode.Tags.Contains("PlanningPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "PlanningPhaseEnter", Context = _duelState, Mode = GameMode.Combat });

                QueueAction(new RollSpeedAction(_duelState.PlayerSide.Board));
                QueueAction(new RollSpeedAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;

                Debug.Log("[Phase] Waiting for player confirmation...");
                _playerConfirmedPhase = false;
                await UniTask.WaitUntil(() => _playerConfirmedPhase);
                Debug.Log("[Phase] Confirmed - advancing.");

                await SimulatePlanningAsync();
            }
            else if (targetNode.Tags.Contains("ClashingPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "ClashingPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await ResolveClashesAsync();
                if (_leaveDuelRequested || _duelState == null) return;
            }
            else if (targetNode.Tags.Contains("OneSidedAttackPhase"))
            {
                GlobalServices.EventBus.Publish(new HintEvent { Tag = "OneSidedAttackPhaseEnter", Context = _duelState, Mode = GameMode.Combat });
                await ResolveOneSidedAttacksAsync();
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
                Debug.Log("[Phase] Loot phase entered");
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
            if (node == null) return false;
            foreach (var trans in node.Transitions)
            {
                if (trans.Condition.Type == ConditionType.TagActive &&
                    trans.Condition.TagTrigger == triggerTag)
                {
                    await TransitionToPhaseAsync(trans.Target);
                    return true;
                }
            }

            return false;
        }

        private bool IsDuelTerminal()
        {
            if (_duelState == null || _encounter == null) return false;
            return (_encounter.WinCondition ?? WinConditionDatabase.GetWinCondition(_encounter.WinConditionId))?.Check(_duelState) == true;
        }

        private DuelOutcome EvaluateDuelOutcome()
        {
            if (_duelState?.PlayerTown == null || _duelState?.OpponentTown == null)
                return DuelOutcome.InProgress;

            bool playerAlive = _duelState.PlayerTown.IsAlive;
            bool opponentAlive = _duelState.OpponentTown.IsAlive;

            if (playerAlive && opponentAlive) return DuelOutcome.InProgress;
            if (playerAlive && !opponentAlive) return DuelOutcome.PlayerWon;
            if (!playerAlive && opponentAlive) return DuelOutcome.PlayerLost;
            return DuelOutcome.Draw;
        }

        private void CaptureDuelOutcomeIfFinished()
        {
            if (_duelOutcome != DuelOutcome.InProgress) return;

            _duelOutcome = EvaluateDuelOutcome();
            _duelFinished = _duelOutcome != DuelOutcome.InProgress;
        }

        private async UniTask TransitionToOutcomePhaseAsync()
        {
            string triggerTag = _duelOutcome == DuelOutcome.PlayerWon ? "WinConditionMet" : "Defeat";
            bool transitioned = await TransitionToPhaseWithTagAsync(triggerTag);
            if (!transitioned)
            {
                Debug.LogWarning($"[DuelManager] No '{triggerTag}' transition found. Returning to exploration immediately.");
                await ReturnToExplorationAsync();
            }
        }

        private async UniTask ReturnToExplorationAsync()
        {
            if (_leaveDuelRequested) return;

            var director = ResolveGameDirector();
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
            if (node?.Transitions == null) return;

            foreach (var trans in node.Transitions)
            {
                if (trans == null || trans.Target == null) continue;
                if (trans.Condition.Type != ConditionType.None) continue;
                if (trans.Target == node) continue;

                await TransitionToPhaseAsync(trans.Target);
                return;
            }
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
                        target.PlannedTarget = null;
                        GlobalServices.EventBus.Publish(new ClashResolvedEvent(card, target));
                    }
                    else if (targetWins)
                    {
                        QueueAction(new DamageAction(card, target.Attack, target));
                        card.PlannedTarget = null;
                        GlobalServices.EventBus.Publish(new ClashResolvedEvent(target, card));
                    }
                }
            }
            await ProcessActionsAsync();
                if (_leaveDuelRequested || _duelState == null) return;
        }

        private async UniTask ResolveOneSidedAttacksAsync()
        {
            var attackers = GetAllAttackers()
                        .Where(a => a.PlannedTarget != null && a.PlannedTarget.IsAlive)
                        .OrderByDescending(a => a.CurrentSpeed);

            foreach (var card in attackers)
            {
                QueueAction(new DamageAction(card.PlannedTarget, card.Attack, card));
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

                enemySide.Hand.Remove(decision.Card);
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
