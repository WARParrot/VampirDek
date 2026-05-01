using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine.SceneManagement;

namespace Combat
{
    public class DuelManager : IGameMode
    {
        private readonly Queue<IGameAction> _actionQueue = new();
        private CombatState _state = CombatState.PlayerTurnIdle;
        private DuelState _duelState;
        private CombatEncounter _encounter;
        private string _tableId;
        private Scene _combatScene;

        public DuelState CurrentDuelState => _duelState;

        public async UniTask EnterAsync(object context)
        {
            var ctx = (DuelStartContext)context;
            _encounter = ctx.Encounter;
            _tableId = ctx.TableId;
            _duelState = new DuelState(_encounter, ctx.PlayerDeck, _encounter.OpponentDeck);

            if (LoadArenaScene)
            {
                await SceneManager.LoadSceneAsync("Arena_Ruins", LoadSceneMode.Additive).ToUniTask();
                _combatScene = SceneManager.GetSceneByName("Arena_Ruins");
            }

            foreach (var data in _encounter.PermanentGlobalEnchantments)
            {
                var runtime = EnchantmentFactory.Create(data, null);
                runtime.OnAttach();
            }

            await TransitionToPhaseAsync(_duelState.CurrentPhase);
        }

        public async UniTask ExitAsync()
        {
            if (!_encounter.WinCondition.Check(_duelState))
            {
                var dto = MatchStateDTO.FromDuelState(_duelState);
                /*GlobalServices.SaveSystem.SaveActiveBattle(_tableId, dto);*/
            }
            if (_combatScene.isLoaded) await SceneManager.UnloadSceneAsync(_combatScene).ToUniTask();
            _duelState = null;
        }

        public async UniTask OnPauseAsync() => _state = CombatState.Paused;
        public async UniTask OnResumeAsync() => _state = CombatState.PlayerTurnIdle;

        public void QueueAction(IGameAction action) => _actionQueue.Enqueue(action);

        private async UniTask ProcessActionsAsync()
        {
            while (_actionQueue.Count > 0)
            {
                if (_state == CombatState.Paused) break;
                var action = _actionQueue.Dequeue();
                await action.ExecuteAsync();
                GlobalServices.EventBus.Publish(new ActionExecutedEvent(action));

                if (_encounter.WinCondition.Check(_duelState))
                {
                    await TransitionToPhaseWithTagAsync("WinConditionMet");
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

            // Phase-specific automatic actions
            if (targetNode.Tags.Contains("StartOfTurn"))
            {
                // Roll speed for both sides
                QueueAction(new RollSpeedAction(_duelState.PlayerSide.Board));
                QueueAction(new RollSpeedAction(_duelState.OpponentSide.Board));
                // Regenerate human resources
                QueueAction(new RegenerateHumanResourcesAction(_duelState.PlayerSide));
                QueueAction(new RegenerateHumanResourcesAction(_duelState.OpponentSide));
                // Reset building damage accumulators
                QueueAction(new ResetBuildingDamageAction(_duelState.PlayerSide.Board));
                QueueAction(new ResetBuildingDamageAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
            }
            else if (targetNode.Tags.Contains("BuildingPhase"))
            {
                // Enable player building input (UI will handle)
            }
            else if (targetNode.Tags.Contains("PlanningPhase"))
            {
                // Planning will be handled by UI and AI controller; we'll let the manager wait for external completion.
                // For now, transition automatically after a simulated delay.
                await SimulatePlanningAsync();
            }
            else if (targetNode.Tags.Contains("ClashingPhase"))
            {
                await ResolveClashesAsync();
            }
            else if (targetNode.Tags.Contains("OneSidedAttackPhase"))
            {
                await ResolveOneSidedAttacksAsync();
            }
            else if (targetNode.Tags.Contains("EndOfTurn"))
            {
                QueueAction(new BuildingDestructionCheckAction(_duelState.PlayerSide.Board));
                QueueAction(new BuildingDestructionCheckAction(_duelState.OpponentSide.Board));
                await ProcessActionsAsync();
            }

            await CheckAutoTransitionsAsync();
        }

        public async UniTask TransitionToPhaseWithTagAsync(string triggerTag)
        {
            var node = _duelState.CurrentPhase;
            foreach (var trans in node.Transitions)
            {
                if (trans.Condition.Type == ConditionType.TagActive && trans.Condition.TagTrigger == triggerTag)
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
            var allAttackers = new List<BoardCard>();
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
            {
                foreach (var slot in side.Board.VanguardRow)
                    if (slot.Occupant != null && slot.Occupant.PlannedTarget != null)
                        allAttackers.Add(slot.Occupant);
            }

            allAttackers.Sort((a, b) => b.CurrentSpeed.CompareTo(a.CurrentSpeed));

            var resolved = new HashSet<BoardCard>();
            foreach (var card in allAttackers)
            {
                if (resolved.Contains(card)) continue;
                var target = card.PlannedTarget as BoardCard;
                if (target == null || resolved.Contains(target)) continue;
                if (target.PlannedTarget == card)
                {
                    resolved.Add(card);
                    resolved.Add(target);
                    QueueAction(new ClashAction(card, target));
                }
            }
            await ProcessActionsAsync();
        }

        private async UniTask ResolveOneSidedAttacksAsync()
        {
            var allAttackers = new List<BoardCard>();
            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
            {
                foreach (var slot in side.Board.VanguardRow)
                    if (slot.Occupant != null && slot.Occupant.PlannedTarget != null && slot.Occupant.PlannedTarget.IsAlive)
                        allAttackers.Add(slot.Occupant);
            }

            foreach (var card in allAttackers)
            {
                var target = card.PlannedTarget;
                QueueAction(new DamageAction(target, card.Attack, card));
            }
            await ProcessActionsAsync();

            foreach (var side in new[] { _duelState.PlayerSide, _duelState.OpponentSide })
            {
                foreach (var slot in side.Board.VanguardRow)
                    if (slot.Occupant != null)
                        slot.Occupant.PlannedTarget = null;
            }
        }

        private async UniTask SimulatePlanningAsync()
        {
            // Opponent AI chooses targets (simple random for now)
            var rng = new Random();
            foreach (var slot in _duelState.OpponentSide.Board.VanguardRow)
            {
                if (slot.Occupant != null)
                {
                    var playerVanguard = _duelState.PlayerSide.Board.VanguardRow.Where(s => s.Occupant != null).ToArray();
                    if (playerVanguard.Length > 0)
                        slot.Occupant.PlannedTarget = playerVanguard[rng.Next(playerVanguard.Length)].Occupant;
                }
            }
            // Player planning is done via UI; we skip for now
            await UniTask.Yield();
        }

        // Win condition check after actions already handled.

        private enum CombatState { PlayerTurnIdle, Animating, Paused }
        private bool LoadArenaScene = false; // for testing
    }
}
