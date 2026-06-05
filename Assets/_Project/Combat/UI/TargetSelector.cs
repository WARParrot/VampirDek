using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Combat.UI
{
    public class TargetSelector : MonoBehaviour
    {
        [SerializeField] private TutorialSystem _tutorialSystem;

        private DuelManager _duelManager;
        private BoardCard _selectedFriendly;

        [Header("Highlight Colors")]
        public Color FriendlyColor = Color.cyan;
        public Color EnemyColor = Color.red;

        private BoardView _boardView;

        void Start()
        {
            _duelManager = DuelManagerProxy.Instance;
            _boardView = FindObjectOfType<BoardView>(true);

            if (_tutorialSystem == null)
            {
                _tutorialSystem = FindObjectOfType<TutorialSystem>();
            }
        }

        void Update()
        {
            if (_duelManager?.CurrentDuelState == null) return;
            var state = _duelManager.CurrentDuelState;
            var phase = state.CurrentPhase;

            bool inPlanning = phase != null && phase.Tags.Contains("PlanningPhase");
            if (!inPlanning)
            {
                if (_selectedFriendly != null)
                {
                    ClearSelection();
                    _selectedFriendly = null;
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var slotUI = GetBoardSlotUnderMouse();
                if (slotUI == null) return;

                var board = slotUI.Board;
                var card = slotUI.Occupant;

                if (board == state.PlayerSide.Board && card != null && card.IsAlive && card.TypeOfRow == Definitions.RowType.Vanguard)
                {
                    SelectFriendly(card);
                }
                else if (board == state.OpponentSide.Board && _selectedFriendly != null)
                {
                    var provoker = CardBehaviorTags.GetActiveProvokerOn(state.OpponentSide);
                    if (provoker != null && card != provoker)
                    {
                        Debug.Log($"[TargetSelector] Provocation blocks selecting {card?.SourceCard.CardName}");
                        return;
                    }
                    _selectedFriendly.PlannedTarget = card;
                    ClearSelection();
                    _selectedFriendly = null;

                    if (_tutorialSystem != null && _tutorialSystem.IsTutorialActive)
                    {
                        Debug.Log("[TargetSelector] Calling OnTargetSelected for tutorial");
                        _tutorialSystem.OnTargetSelected();
                    }
                    else
                    {
                        Debug.Log($"[TargetSelector] Tutorial not notified. _tutorialSystem={(_tutorialSystem != null ? "OK" : "NULL")}, IsActive={_tutorialSystem?.IsTutorialActive}");
                    }
                }
            }
        }

        private BoardSlotUI GetBoardSlotUnderMouse()
        {
            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            return results.Select(r => r.gameObject.GetComponent<BoardSlotUI>()).FirstOrDefault(s => s != null);
        }

        private void SelectFriendly(BoardCard card)
        {
            if (_selectedFriendly != null) ClearSelection();
            _selectedFriendly = card;
            _boardView.SetCardHighlight(card, FriendlyColor);

            foreach (var slot in _duelManager.CurrentDuelState.OpponentSide.Board.AllSlots())
            {
                if (slot.Occupant != null && slot.Occupant.IsAlive)
                    _boardView.SetCardHighlight(slot.Occupant, EnemyColor);
            }
        }

        private void ClearSelection()
        {
            if (_selectedFriendly != null)
                _boardView.SetCardHighlight(_selectedFriendly, Color.white);
            foreach (var slot in _duelManager.CurrentDuelState.OpponentSide.Board.AllSlots())
                if (slot.Occupant != null)
                    _boardView.SetCardHighlight(slot.Occupant, Color.white);
        }
    }
}