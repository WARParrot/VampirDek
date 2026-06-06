using System.Collections.Generic;
using System.Text;
using Combat;
using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TargetPlanArrowsUI : MonoBehaviour
{
    private const float RebuildIntervalSeconds = 0.08f;
    private const float SourceEndpointInsetPixels = 8f;
    private const float ShaftTargetInsetPixels = 18f;
    private const float ArrowHeadTargetInsetPixels = 6f;

    private static TargetPlanArrowsUI _instance;

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private BoardView _boardView;
    private readonly List<ArrowView> _arrows = new();
    private readonly List<TextMeshProUGUI> _clashLabels = new();
    private string _lastSignature = string.Empty;
    private float _nextRebuildAt;

    private static readonly Color EnemyPlanColor = new(1f, 0.22f, 0.16f, 0.78f);
    private static readonly Color PlayerPlanColor = new(0.15f, 0.78f, 1f, 0.78f);
    private static readonly Color ClashColor = new(1f, 0.86f, 0.12f, 0.95f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        var go = new GameObject("[TargetPlanArrowsUI]");
        _instance = go.AddComponent<TargetPlanArrowsUI>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        ClearArrows();
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        var phase = state?.CurrentPhase;
        bool inPlanning = phase != null && phase.Tags.Contains("PlanningPhase");
        if (!inPlanning || state == null)
        {
            ClearArrows();
            _lastSignature = string.Empty;
            return;
        }

        if (_boardView == null) _boardView = FindObjectOfType<BoardView>(true);
        if (_boardView == null || !EnsureCanvas()) return;

        if (Time.unscaledTime >= _nextRebuildAt)
        {
            _nextRebuildAt = Time.unscaledTime + RebuildIntervalSeconds;
            var signature = BuildSignature(state);
            if (signature != _lastSignature)
            {
                _lastSignature = signature;
                RebuildArrows(state);
            }
        }

    }

    private void LateUpdate()
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        var phase = state?.CurrentPhase;
        bool inPlanning = phase != null && phase.Tags.Contains("PlanningPhase");
        if (!inPlanning || state == null || _canvas == null) return;

        // Run geometry after camera movement/canvas camera assignment so perspective changes
        // don't leave the arrows one frame behind or pivoted around stale screen positions.
        UpdateArrowGeometry();
        UpdateClashLabels();
    }

    private bool EnsureCanvas()
    {
        if (_canvas != null) return true;

        var go = new GameObject("TargetPlanArrowsCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9997;
        _canvasRect = _canvas.transform as RectTransform;
        go.AddComponent<CanvasScaler>();
        var raycaster = go.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
        return true;
    }

    private string BuildSignature(DuelState state)
    {
        var sb = new StringBuilder();
        AppendSideSignature(sb, state.OpponentSide, "E");
        AppendSideSignature(sb, state.PlayerSide, "P");
        return sb.ToString();
    }

    private static void AppendSideSignature(StringBuilder sb, SideState side, string prefix)
    {
        if (side?.Board == null) return;
        foreach (var slot in side.Board.AllSlots())
        {
            var card = slot?.Occupant;
            if (!IsAttackCapable(card) || card.PlannedTarget == null || !card.PlannedTarget.IsAlive) continue;
            sb.Append(prefix).Append(card.Id).Append("->");
            if (card.PlannedTarget is BoardCard targetCard)
                sb.Append(targetCard.Id);
            else
                sb.Append(card.PlannedTarget.GetHashCode());
            bool clash = card.PlannedTarget is BoardCard target && target.PlannedTarget == card;
            if (clash) sb.Append("!");
            sb.Append(';');
        }
    }

    private void RebuildArrows(DuelState state)
    {
        ClearArrows();
        AddSideArrows(state.OpponentSide, true);
        AddSideArrows(state.PlayerSide, false);
    }

    private void AddSideArrows(SideState side, bool enemyPlan)
    {
        if (side?.Board == null) return;
        foreach (var slot in side.Board.AllSlots())
        {
            var card = slot?.Occupant;
            if (!IsAttackCapable(card) || card.PlannedTarget == null || !card.PlannedTarget.IsAlive) continue;

            var targetSlot = FindSlotFor(card.PlannedTarget);
            var sourceSlot = FindSlotFor(card);
            if (sourceSlot == null || targetSlot == null || sourceSlot == targetSlot) continue;

            bool isClash = card.PlannedTarget is BoardCard targetCard && targetCard.PlannedTarget == card;
            var color = isClash ? ClashColor : (enemyPlan ? EnemyPlanColor : PlayerPlanColor);
            var arrow = CreateArrow(card, card.PlannedTarget, sourceSlot, targetSlot, color, isClash, enemyPlan);
            _arrows.Add(arrow);

            if (isClash && card.PlannedTarget is BoardCard other && card.Id < other.Id)
                _clashLabels.Add(CreateClashLabel(card, other));
        }
    }

    private ArrowView CreateArrow(BoardCard source, IGameEntity target, BoardSlotUI sourceSlot, BoardSlotUI targetSlot, Color color, bool isClash, bool enemyPlan)
    {
        var root = new GameObject(enemyPlan ? "EnemyPlanArrow" : "PlayerPlanArrow");
        root.transform.SetParent(_canvas.transform, false);
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var shaftGo = new GameObject("Shaft");
        shaftGo.transform.SetParent(root.transform, false);
        var shaft = shaftGo.AddComponent<Image>();
        shaft.raycastTarget = false;
        shaft.color = color;
        shaft.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        shaft.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        shaft.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var headGo = new GameObject("Head");
        headGo.transform.SetParent(root.transform, false);
        var head = headGo.AddComponent<TextMeshProUGUI>();
        head.text = "➤";
        head.fontSize = isClash ? 30f : 24f;
        head.fontStyle = FontStyles.Bold;
        head.alignment = TextAlignmentOptions.Center;
        head.raycastTarget = false;
        head.color = color;
        head.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        head.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        head.rectTransform.sizeDelta = new Vector2(44f, 44f);

        return new ArrowView
        {
            Root = root,
            Shaft = shaft.rectTransform,
            ShaftImage = shaft,
            Head = head.rectTransform,
            HeadText = head,
            Source = source,
            Target = target,
            SourceSlot = sourceSlot,
            TargetSlot = targetSlot,
            IsClash = isClash
        };
    }

    private TextMeshProUGUI CreateClashLabel(BoardCard a, BoardCard b)
    {
        var go = new GameObject("PlannedClashLabel");
        go.transform.SetParent(_canvas.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "CLASH";
        tmp.fontSize = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.color = ClashColor;
        tmp.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmp.rectTransform.sizeDelta = new Vector2(128f, 34f);
        tmp.gameObject.AddComponent<ClashLabelBinding>().Configure(a, b, this);
        return tmp;
    }

    private void UpdateArrowGeometry()
    {
        for (int i = _arrows.Count - 1; i >= 0; i--)
        {
            var arrow = _arrows[i];
            if (arrow.Source == null || arrow.Target == null || !arrow.Source.IsAlive || !arrow.Target.IsAlive)
            {
                DestroyArrow(arrow);
                _arrows.RemoveAt(i);
                continue;
            }

            var from = SlotCenterOnOverlay(arrow.SourceSlot);
            var to = SlotCenterOnOverlay(arrow.TargetSlot);
            var delta = to - from;
            float length = delta.magnitude;
            if (length < 8f)
            {
                arrow.Root.SetActive(false);
                continue;
            }

            arrow.Root.SetActive(true);
            var direction = delta / length;
            var sourceInset = Mathf.Min(SourceEndpointInsetPixels, length * 0.12f);
            var shaftTargetInset = Mathf.Min(ShaftTargetInsetPixels, length * 0.18f);
            var headTargetInset = Mathf.Min(ArrowHeadTargetInsetPixels, length * 0.08f);
            var shaftFrom = from + direction * sourceInset;
            var shaftTo = to - direction * shaftTargetInset;
            var headPosition = to - direction * headTargetInset;
            var shaftDelta = shaftTo - shaftFrom;
            var shaftLength = shaftDelta.magnitude;
            if (shaftLength < 4f)
            {
                arrow.Root.SetActive(false);
                continue;
            }

            var mid = (shaftFrom + shaftTo) * 0.5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float thickness = arrow.IsClash ? 8f : 5f;

            arrow.Shaft.anchoredPosition = mid;
            arrow.Shaft.sizeDelta = new Vector2(shaftLength, thickness);
            arrow.Shaft.localRotation = Quaternion.Euler(0f, 0f, angle);
            arrow.Head.anchoredPosition = headPosition;
            arrow.Head.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private Vector2 SlotCenterOnOverlay(BoardSlotUI slot)
    {
        var screenPoint = SlotCenterOnScreen(slot);
        if (_canvasRect == null) _canvasRect = _canvas != null ? _canvas.transform as RectTransform : null;
        if (_canvasRect == null) return screenPoint;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPoint, null, out var localPoint)
            ? localPoint
            : screenPoint;
    }

    private static Vector2 SlotCenterOnScreen(BoardSlotUI slot)
    {
        if (slot == null) return Vector2.zero;
        var rect = slot.transform as RectTransform;
        var mainCamera = Camera.main;
        if (rect != null)
        {
            // Project the live world-space rect through the live main camera. This intentionally
            // avoids cached Canvas.worldCamera values, because DuelCameraSwitcher updates world
            // canvas cameras in LateUpdate while the camera itself is moving between perspectives.
            // Average actual world corners instead of relying on local rect center so authored
            // slot pivots/scales still produce a visual center that matches the board.
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            return RectTransformUtility.WorldToScreenPoint(mainCamera, center);
        }

        if (mainCamera != null) return mainCamera.WorldToScreenPoint(slot.transform.position);
        return slot.transform.position;
    }

    private BoardSlotUI FindSlotFor(IGameEntity entity)
    {
        if (entity == null) return null;
        if (_boardView == null) _boardView = FindObjectOfType<BoardView>(true);
        if (_boardView == null) return null;
        foreach (var ui in _boardView.GetSlotUIs())
        {
            if (ui != null && ui.Occupant == entity) return ui;
        }
        return null;
    }

    private Vector2 PlannedClashCenter(BoardCard a, BoardCard b)
    {
        var aSlot = FindSlotFor(a);
        var bSlot = FindSlotFor(b);
        if (aSlot == null || bSlot == null) return Vector2.zero;
        return (SlotCenterOnOverlay(aSlot) + SlotCenterOnOverlay(bSlot)) * 0.5f + new Vector2(0f, 28f);
    }

    private void UpdateClashLabels()
    {
        foreach (var label in _clashLabels)
        {
            if (label == null) continue;
            var binding = label.GetComponent<ClashLabelBinding>();
            binding?.RefreshPosition();
        }
    }

    private void ClearArrows()
    {
        foreach (var arrow in _arrows) DestroyArrow(arrow);
        _arrows.Clear();

        foreach (var label in _clashLabels)
            if (label != null) Destroy(label.gameObject);
        _clashLabels.Clear();
    }

    private static void DestroyArrow(ArrowView arrow)
    {
        if (arrow?.Root != null) Destroy(arrow.Root);
    }

    private static bool IsAttackCapable(BoardCard card)
    {
        return card != null && card.IsAlive && card.Attack > 0;
    }

    private sealed class ArrowView
    {
        public GameObject Root;
        public RectTransform Shaft;
        public Image ShaftImage;
        public RectTransform Head;
        public TextMeshProUGUI HeadText;
        public BoardCard Source;
        public IGameEntity Target;
        public BoardSlotUI SourceSlot;
        public BoardSlotUI TargetSlot;
        public bool IsClash;
    }

    private sealed class ClashLabelBinding : MonoBehaviour
    {
        private BoardCard _a;
        private BoardCard _b;
        private TargetPlanArrowsUI _owner;
        private TextMeshProUGUI _label;

        public void Configure(BoardCard a, BoardCard b, TargetPlanArrowsUI owner)
        {
            _a = a;
            _b = b;
            _owner = owner;
            _label = GetComponent<TextMeshProUGUI>();
        }

        private void LateUpdate() => RefreshPosition();

        public void RefreshPosition()
        {
            if (_owner == null || _label == null || _a == null || _b == null) return;
            _label.rectTransform.anchoredPosition = _owner.PlannedClashCenter(_a, _b);
        }
    }
}
