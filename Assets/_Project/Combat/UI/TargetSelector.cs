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
        [SerializeField] private BoardView _boardView;

        private DuelManager _duelManager;
        private BoardCard _selectedFriendly;
        private bool _mouseReleasedSinceCheck;
        private Vector2 _mouseReleasePosition;

        [Header("Highlight Colors")]
        public Color FriendlyColor = Color.cyan;
        public Color EnemyColor = Color.red;

        void Start()
        {
            _duelManager = DuelManagerProxy.Instance;
            ResolveReferences();
        }

        void Update()
        {
            ResolveReferences();
            CaptureMouseEdges();

            if (_duelManager?.CurrentDuelState == null)
            {
                ResetCapturedMouseEdges();
                return;
            }
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
                ResetCapturedMouseEdges();
                return;
            }

            // PlanningPhaseController is the newer click owner. When it exists, do not run
            // this legacy selector's duplicate UI raycast / rect-scan fallback on release.
            if (PlanningPhaseController.Instance != null)
            {
                ResetCapturedMouseEdges();
                return;
            }

            if (Consume(ref _mouseReleasedSinceCheck))
            {
                var slotUI = GetBoardSlotUnderMouse(_mouseReleasePosition);
                if (slotUI == null) return;

                // Forward polling/raycast clicks into the same controller used by BoardSlotUI.OnPointerClick.
                // This preserves a robust click fallback without keeping a second selected-attacker state.
                if (PlanningPhaseController.Instance != null)
                {
                    PlanningPhaseController.Instance.HandleSlotClick(slotUI);
                    return;
                }

                var board = slotUI.Board;
                var card = slotUI.Occupant;

                if (board == state.PlayerSide.Board && IsAttackCapable(card))
                {
                    SelectFriendly(card);
                }
                else if (board == state.OpponentSide.Board && _selectedFriendly != null && card != null && card.IsAlive)
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

        private void CaptureMouseEdges()
        {
            if (!Input.GetMouseButtonUp(0)) return;
            _mouseReleasedSinceCheck = true;
            _mouseReleasePosition = Input.mousePosition;
        }

        private void ResolveReferences()
        {
            _duelManager ??= DuelManagerProxy.Instance;
            _boardView ??= BoardView.Current;
            _tutorialSystem ??= TutorialSystem.Current;
        }

        private void ResetCapturedMouseEdges()
        {
            _mouseReleasedSinceCheck = false;
        }

        private static bool Consume(ref bool value)
        {
            if (!value) return false;
            value = false;
            return true;
        }

        private BoardSlotUI GetBoardSlotUnderMouse(Vector2 mousePosition)
        {

            if (EventSystem.current != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { position = mousePosition };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, results);
                var raycastSlot = results
                    .Select(r => r.gameObject.GetComponentInParent<BoardSlotUI>())
                    .FirstOrDefault(s => s != null);
                if (raycastSlot != null) return raycastSlot;
            }

            // Some tutorial/global UI can still sit above the board in the GraphicRaycaster even
            // when it is visually transparent. When that happens, fall back to the authored slot
            // rectangles instead of making the player click repeatedly until a raycast slips through.
            return FindSlotByRect(mousePosition);
        }

        private BoardSlotUI FindSlotByRect(Vector2 screenPosition)
        {
            var candidates = _boardView != null ? _boardView.GetSlotUIs() : System.Array.Empty<BoardSlotUI>();
            BoardSlotUI best = null;
            var bestArea = float.MaxValue;

            foreach (var slot in candidates)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                var rect = slot.transform as RectTransform;
                if (rect == null) continue;

                var canvas = slot.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;

                if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, camera)) continue;

                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                var area = Mathf.Abs((corners[2].x - corners[0].x) * (corners[2].y - corners[0].y));
                if (area < bestArea)
                {
                    best = slot;
                    bestArea = area;
                }
            }

            return best;
        }

        private static bool IsAttackCapable(BoardCard card)
        {
            return card != null && card.IsAlive && card.Attack > 0;
        }

        private void SelectFriendly(BoardCard card)
        {
            if (_selectedFriendly != null) ClearSelection();
            _selectedFriendly = card;
            _boardView.SetCardHighlight(card, FriendlyColor);

            ResolveReferences();
            if (_tutorialSystem != null && _tutorialSystem.IsTutorialActive)
            {
                _tutorialSystem.OnAttackerCardSelected();
            }

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