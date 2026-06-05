using System.Collections.Generic;
using System.Linq;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Combat.UI
{
    public class PlanningPhaseController : MonoBehaviour
    {
        public static PlanningPhaseController Instance { get; private set; }

        private BoardCard _selectedAttacker;
        private int _lastHandledClickFrame = -1;
        private BoardSlotUI _lastHandledClickSlot;

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

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (!IsPlanningPhase()) return;

            var slot = FindSlotUnderMouse(mouse.position.ReadValue());
            if (slot != null)
            {
                HandleSlotClick(slot);
            }
        }

        public void HandleSlotClick(BoardSlotUI slotUI)
        {
            if (slotUI == null) return;
            if (_lastHandledClickFrame == Time.frameCount && _lastHandledClickSlot == slotUI) return;
            _lastHandledClickFrame = Time.frameCount;
            _lastHandledClickSlot = slotUI;

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

            if (isPlayerSide && IsAttackCapable(card))
            {
                Debug.Log($"[Planner] Selecting friendly attacker {card.SourceCard.CardName} ATK={card.Attack}");
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

                var tutorial = FindObjectOfType<Combat.TutorialSystem>(true);
                if (tutorial != null && tutorial.IsTutorialActive)
                {
                    tutorial.OnAttackerCardSelected();
                }
            }
            else if (isOpponentSide && _selectedAttacker != null)
            {
                var provoker = Combat.CardBehaviorTags.GetActiveProvokerOn(state.OpponentSide);
                if (provoker != null && card != provoker)
                {
                    Debug.Log($"[Planner] Provocation: {provoker.SourceCard.CardName} forces attack target. Ignoring click on {card.SourceCard.CardName}");
                    return;
                }
                Debug.Log($"[Planner] Assigning target {card.SourceCard.CardName} to {_selectedAttacker.SourceCard.CardName}");
                _selectedAttacker.PlannedTarget = card;
                ClearSelection();
                _selectedAttacker = null;

                var tutorial = FindObjectOfType<Combat.TutorialSystem>(true);
                Debug.Log($"[Planner→Tutorial] tutorial={(tutorial != null ? "OK" : "NULL")}, active={tutorial?.IsTutorialActive}, step={tutorial?.CurrentStepIndex}");
                if (tutorial != null && tutorial.IsTutorialActive)
                {
                    tutorial.OnTargetSelected();
                }
            }
            else
            {
                Debug.Log("[Planner] Click condition not met");
            }
        }

        private static bool IsPlanningPhase()
        {
            var phase = DuelManagerProxy.Instance?.CurrentDuelState?.CurrentPhase;
            return phase != null && phase.Tags.Contains("PlanningPhase");
        }

        private BoardSlotUI FindSlotUnderMouse(Vector2 screenPosition)
        {
            // First try Unity's UI pipeline, so authored raycast ordering still wins when it works.
            if (EventSystem.current != null)
            {
                var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, results);
                var raycastSlot = results
                    .Select(result => result.gameObject.GetComponentInParent<BoardSlotUI>())
                    .FirstOrDefault(slot => slot != null);
                if (raycastSlot != null) return raycastSlot;
            }

            // World-space canvases can fail UI raycasts when the event camera is missing/stale.
            // Fall back to testing the actual slot RectTransforms with a camera chosen from the slot's canvas.
            var candidates = FindObjectsOfType<BoardSlotUI>(false);
            BoardSlotUI best = null;
            var bestArea = float.MaxValue;

            foreach (var slot in candidates)
            {
                if (slot == null || !slot.gameObject.activeInHierarchy) continue;
                var rect = slot.transform as RectTransform;
                if (rect == null) continue;

                var camera = GetRectEventCamera(slot);
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

        private static Camera GetRectEventCamera(BoardSlotUI slot)
        {
            var canvas = slot != null ? slot.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return Camera.main;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
        }

        private static bool IsAttackCapable(BoardCard card)
        {
            return card != null && card.IsAlive && card.Attack > 0;
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