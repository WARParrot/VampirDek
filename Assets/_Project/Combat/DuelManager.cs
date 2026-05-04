using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public DuelState CurrentDuelState => _duelState;
        public bool LoadArenaScene = false;
        private bool _playerConfirmedPhase;

        public void ConfirmCurrentPhase()
        {
            Debug.Log("[DuelManager] ConfirmCurrentPhase called");
            _playerConfirmedPhase = true;
        }

        public async UniTask EnterAsync(object context)
        {
            var ctx = (DuelStartContext)context;
            _encounter = ctx.Encounter;
            _tableId = ctx.TableId;
            _duelState = new DuelState(_encounter, ctx.PlayerDeck, _encounter.OpponentDeck);

            if (LoadArenaScene)
            {
                var op = SceneManager.LoadSceneAsync("Arena_Ruins", LoadSceneMode.Additive);
                await op;
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
                string json = JsonUtility.ToJson(dto);
                GlobalServices.SaveSystem.SaveActiveBattle(_tableId, json);
            }

            DetachAllEnchantments(_duelState.PlayerSide);
            DetachAllEnchantments(_duelState.OpponentSide);

            if (_combatScene.isLoaded)
                await SceneManager.UnloadSceneAsync(_combatScene);

            _duelState = null;
            _encounter = null;
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
            Debug.Log($"[Phase] Entered {targetNode.PhaseId} with tags: {string.Join(", ", targetNode.Tags)}");

            if (targetNode.Tags.Contains("DuelStart"))
            {
                Debug.Log("[Phase] Drawing 3 cards...");
                QueueAction(new DrawCardsAction(_duelState.PlayerSide, 3));
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
                if (target.PlannedTarget == card) // mutual targeting
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
