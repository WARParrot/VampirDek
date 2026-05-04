using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;
using UnityEngine;

namespace Combat.UI
{
    public class PlanningPhaseController : MonoBehaviour
    {
        public static PlanningPhaseController Instance { get; private set; }

        private BoardCard _selectedAttacker;

        [Header("Highlight Colors")]
        public Color AttackerHighlight = Color.cyan;
        public Color TargetHighlight = Color.red;

        private BoardView _boardView;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            _boardView = FindObjectOfType<BoardView>(true);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void HandleSlotClick(BoardSlotUI slotUI)
        {
            var duelManager = DuelManagerProxy.Instance;
            Debug.Log($"[Planner] Click slot={slotUI.RowType}[{slotUI.Index}] | dm={duelManager != null}");

            if (duelManager == null)
            {
                Debug.Log("[Planner] DuelManager null - abort");
                return;
            }

            var state = duelManager.CurrentDuelState;
            Debug.Log($"[Planner] DuelState={state != null}");

            if (state == null)
            {
                Debug.Log("[Planner] DuelState null - abort");
                return;
            }

            var phase = state.CurrentPhase;
            Debug.Log($"[Planner] Phase={phase?.PhaseId} | Tags={string.Join(",", phase?.Tags ?? new System.Collections.Generic.List<string>())}");

            if (phase == null || !phase.Tags.Contains("PlanningPhase"))
            {
                Debug.Log("[Planner] Not PlanningPhase - ignore");
                return;
            }

            var card = slotUI.Occupant;
            Debug.Log($"[Planner] Occupant={card?.SourceCard?.CardName} alive={card?.IsAlive}");

            if (card == null || !card.IsAlive)
            {
                Debug.Log("[Planner] No valid occupant - ignore");
                return;
            }

            bool isPlayerSide = slotUI.Board == state.PlayerSide.Board;
            bool isOpponentSide = slotUI.Board == state.OpponentSide.Board;
            Debug.Log($"[Planner] isPlayer={isPlayerSide} isOpponent={isOpponentSide} row={card.TypeOfRow}");

            if (isPlayerSide && card.TypeOfRow == Definitions.RowType.Vanguard)
            {
                Debug.Log("[Planner] Selecting friendly Vanguard");
                if (_selectedAttacker != null) ClearSelection();
                _selectedAttacker = card;
                _boardView.SetCardHighlight(card, AttackerHighlight);

                foreach (var enemySlot in state.OpponentSide.Board.AllSlots())
                {
                    if (enemySlot.Occupant != null && enemySlot.Occupant.IsAlive)
                    {
                        Debug.Log($"[Planner] Highlight enemy {enemySlot.Occupant.SourceCard.CardName}");
                        _boardView.SetCardHighlight(enemySlot.Occupant, TargetHighlight);
                    }
                }
            }
            else if (isOpponentSide && _selectedAttacker != null)
            {
                Debug.Log($"[Planner] Assigning target {card.SourceCard.CardName} to {_selectedAttacker.SourceCard.CardName}");
                _selectedAttacker.PlannedTarget = card;
                ClearSelection();
                _selectedAttacker = null;
            }
            else
            {
                Debug.Log("[Planner] Click condition not met");
            }
}

        private void ClearSelection()
        {
            if (_selectedAttacker != null)
            {
                _boardView.SetCardHighlight(_selectedAttacker, Color.white);
                _selectedAttacker = null;
            }
            var state = DuelManagerProxy.Instance?.CurrentDuelState;
            if (state != null)
            {
                foreach (var slot in state.OpponentSide.Board.AllSlots())
                    if (slot.Occupant != null)
                        _boardView.SetCardHighlight(slot.Occupant, Color.white);
            }
        }
    }
}