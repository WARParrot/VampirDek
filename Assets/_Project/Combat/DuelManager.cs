using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        public DeckData _playerPersistentDeck;
        public DuelState CurrentDuelState => _duelState;
        public bool LoadDuelScene = false;
        private bool _playerConfirmedPhase;

        public void ConfirmCurrentPhase()
        {
            Debug.Log("[DuelManager] ConfirmCurrentPhase called");
            _playerConfirmedPhase = true;
        }

        public async UniTask EnterAsync(object context)
        {
            var ctx = (DuelStartContext) context;
            _playerPersistentDeck = ctx.PlayerPersistentDeck;
            _encounter = ctx.Encounter;
            _tableId = ctx.TableId;
            _duelLoadHandle = ctx.DuelSceneHandle;
            MatchStateDTO savedDto = null;
            if (!string.IsNullOrEmpty(ctx.SavedMatchJson))
            {
                savedDto = JsonUtility.FromJson<MatchStateDTO>(ctx.SavedMatchJson);
                ctx.SavedMatchState = savedDto;
            }

            List<CardDef> opponentDeckList = _encounter.OpponentDeck?.Cards;
            var playerDeckList = ctx.PlayerDeck;

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

            if (ctx.SavedMatchState != null)
            {
                _duelState = ctx.SavedMatchState.ToDuelState(_encounter, playerDeckList, opponentDeckList);
                await ResumeFromSaveAsync();
            }
            else
            {
                _duelState = new DuelState(_encounter, playerDeckList, opponentDeckList);
                await TransitionToPhaseAsync(_duelState.CurrentPhase);
            }
        }

        public async UniTask ExitAsync()
        {
            if (!_encounter.WinCondition.Check(_duelState))
            {
                var dto = MatchStateDTO.FromDuelState(_duelState);
                string json = JsonUtility.ToJson(dto);
                GlobalServices.SaveSystem.SaveActiveBattle(_tableId, json);
            }

            DetachAllEnchantments(_duelState.PlayerSide);
            DetachAllEnchantments(_duelState.OpponentSide);

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

        private void OnLeaveDuel(InputAction.CallbackContext ctx)
        {
            GlobalServices.Director.PopModeAsync().Forget();
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

            await TransitionToPhaseAsync(_duelState.CurrentPhase);
        }

        public void QueueAction(IGameAction action) => _actionQueue.Enqueue(action);

        private async UniTask ProcessActionsAsync()
        {
            while (_actionQueue.Count > 0)
            {
                if (_state == CombatState.Paused) break;
                var action = _actionQueue.Dequeue();
                Debug.Log($"[DuelManager] Processing action: {action.Description}");
                await action.ExecuteAsync();
                GlobalServices.EventBus.Publish(new ActionExecutedEvent(action));

                if (_encounter.WinCondition.Check(_duelState))
                {
                    bool playerWon = _duelState.PlayerTown.IsAlive && !_duelState.OpponentTown.IsAlive;
                    if (playerWon)
                        await TransitionToPhaseWithTagAsync("WinConditionMet");
                    else
                        await TransitionToPhaseWithTagAsync("Defeat");
                    return;
                }
            }
        }

        private async UniTask TransitionToPhaseAsync(PhaseNode targetNode)
        {
            if (_duelState.CurrentPhase != null)
                GlobalServices.EventBus.Publish(new PhaseExitEvent(_duelState.CurrentPhase.PhaseId));

            _duelState.CurrentPhase = targetNode;
            GlobalServices.EventBus.Publish(new PhaseEnterEvent(targetNode.PhaseId, targetNode.Tags));
            Debug.Log($"[Phase] Entered {targetNode.PhaseId} with tags: {string.Join(", ", targetNode.Tags)}");

            if (targetNode.Tags.Contains("DuelStart"))
            {
                Debug.Log("[Phase] Drawing 3 cards...");
                QueueAction(new DrawCardsAction(_duelState.PlayerSide, 2));
                await ProcessActionsAsync();
                Debug.Log($"[Phase] Hand count after draw: {_duelState.PlayerSide.Hand.Count}");
            }
            else if (targetNode.Tags.Contains("StartOfTurn"))
            {
                QueueAction(new RegenerateHumanResourcesAction(_duelState.PlayerSide));
                QueueAction(new RegenerateHumanResourcesAction(_duelState.OpponentSide));

                QueueAction(new ResetBuildingDamageAction(_duelState.PlayerSide.Board));
                QueueAction(new ResetBuildingDamageAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();

                Debug.Log("[Phase] Drawing 1 card...");
                QueueAction(new DrawCardsAction(_duelState.PlayerSide, 1));
                await ProcessActionsAsync();
                Debug.Log($"[Phase] Hand count after draw: {_duelState.PlayerSide.Hand.Count}");
            }
            else if (targetNode.Tags.Contains("BuildingPhase"))
            {
                Debug.Log("[Phase] Waiting for player confirmation...");
                _playerConfirmedPhase = false;
                while (!_playerConfirmedPhase)
                {
                    if (_actionQueue.Count > 0)
                    {
                        Debug.Log($"[Phase] Loop iteration - queue count: {_actionQueue.Count}");
                        Debug.Log("[Phase] Processing actions...");
                        await ProcessActionsAsync();
                        Debug.Log("[Phase] Actions processed.");
                    }
                    else
                    {
                        await UniTask.Yield();
                    }
                };
                Debug.Log("[Phase] Confirmed - advancing.");
            }
            else if (targetNode.Tags.Contains("PlanningPhase"))
            {
                QueueAction(new RollSpeedAction(_duelState.PlayerSide.Board));
                QueueAction(new RollSpeedAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();

                Debug.Log("[Phase] Waiting for player confirmation...");
                _playerConfirmedPhase = false;
                await UniTask.WaitUntil(() => _playerConfirmedPhase);
                Debug.Log("[Phase] Confirmed - advancing.");

                await SimulatePlanningAsync();
            }
            else if (targetNode.Tags.Contains("ClashingPhase"))
            {
                await ResolveClashesAsync();
            }
            else if (targetNode.Tags.Contains("OneSidedAttackPhase"))
            {
                await ResolveOneSidedAttacksAsync();
                ClearAllPlannedTargets();
            }
            else if (targetNode.Tags.Contains("EndOfTurn"))
            {
                QueueAction(new BuildingDestructionCheckAction(_duelState.PlayerSide.Board));
                QueueAction(new BuildingDestructionCheckAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
            }
            else if (targetNode.Tags.Contains("Loot"))
            {
                Debug.Log("[Phase] Loot phase entered");
                await ShowLootSelectionAsync();
            }

            else if (targetNode.Tags.Contains("DuelEnd"))
            {
                Debug.Log("[Phase] DuelEnd – завершаем дуэль.");
                await GlobalServices.Director.PopModeAsync();
                return;
            }

            await CheckAutoTransitionsAsync();
        }

        public async UniTask TransitionToPhaseWithTagAsync(string triggerTag)
        {
            var node = _duelState.CurrentPhase;
            if (node == null) return;
            foreach (var trans in node.Transitions)
            {
                if (trans.Condition.Type == ConditionType.TagActive &&
                    trans.Condition.TagTrigger == triggerTag)
                {
                    await TransitionToPhaseAsync(trans.Target);
                    return;
                }
            }
        }

        private async UniTask CheckAutoTransitionsAsync()
        {
            var node = _duelState.CurrentPhase;
            foreach (var trans in node.Transitions)
            {
                if (trans.Condition.Type == ConditionType.None)
                {
                    await TransitionToPhaseAsync(trans.Target);
                    return;
                }
            }
        }

        private async UniTask ResolveClashesAsync()
        {
            var attackers = GetAllVanguardAttackers()
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
        }

        private async UniTask ResolveOneSidedAttacksAsync()
        {
            var attackers = GetAllVanguardAttackers()
                        .Where(a => a.PlannedTarget != null && a.PlannedTarget.IsAlive)
                        .OrderByDescending(a => a.CurrentSpeed);

            foreach (var card in attackers)
            {
                QueueAction(new DamageAction(card.PlannedTarget, card.Attack, card));
            }
            await ProcessActionsAsync();
        }

        private IEnumerable<BoardCard> GetAllVanguardAttackers()
        {
            var all = new List<BoardCard>();
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
                foreach (var slot in side.Board.VanguardRow)
                    if (slot.Occupant != null && slot.Occupant.PlannedTarget != null && slot.Occupant.IsAlive)
                        all.Add(slot.Occupant);
            return all;
        }

        private void ClearAllPlannedTargets()
        {
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
                foreach (var slot in side.Board.VanguardRow)
                    if (slot.Occupant != null)
                        slot.Occupant.PlannedTarget = null;
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

        private async UniTask SimulatePlanningAsync()
        {
            var rng = new System.Random();
            foreach (var slot in _duelState.OpponentSide.Board.VanguardRow)
            {
                if (slot.Occupant != null)
                {
                    var playerVanguard = _duelState.PlayerSide.Board.VanguardRow
                        .Where(s => s.Occupant != null && s.Occupant.IsAlive)
                        .ToArray();
                    if (playerVanguard.Length > 0)
                        slot.Occupant.PlannedTarget = playerVanguard[rng.Next(playerVanguard.Length)].Occupant;
                }
            }
            await UniTask.Yield();
        }
        private async UniTask ShowLootSelectionAsync()
        {
            bool playerWon = _duelState.PlayerTown.IsAlive && !_duelState.OpponentTown.IsAlive;
            if (!playerWon)
                return;

            var rewardPool = _encounter.RewardCardPool;
            if (rewardPool == null || rewardPool.Count < 3)
            {
                Debug.LogError("RewardCardPool должен содержать хотя бы 3 карты!");
                return;
            }

            var rng = new System.Random();
            var selected = rewardPool.OrderBy(x => rng.Next()).Take(3).ToList();

            var cardSelectionUI = FindObjectOfType<CardSelectionUI>(true);
            if (cardSelectionUI == null)
            {
                Debug.LogError("CardSelectionUI не найден в сцене!");
                return;
            }
            CardDef chosen = await cardSelectionUI.ShowAsync(selected);

            if (_playerPersistentDeck != null)
            {
                _playerPersistentDeck.Cards.Add(chosen);
                Debug.Log($"Карта {chosen.CardName} добавлена в колоду игрока.");
            }
            else
            {
                Debug.LogError("PlayerPersistentDeck не задан в DuelManager!");
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

        private enum CombatState
        {
            PlayerTurnIdle,
            Animating,
            Paused
        }
    }
}
