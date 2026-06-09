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

    private const string ForecastHintPrefKey = "vampirdek.hint.damage_forecast_shown";

    private Canvas _canvas;
    private RectTransform _canvasRect;
    private BoardView _boardView;
    private readonly List<ArrowView> _arrows = new();
    private readonly List<TextMeshProUGUI> _clashLabels = new();
    private string _lastSignature = string.Empty;
    private float _nextRebuildAt;
    private GameObject _forecastHintRoot;
    private float _forecastHintHideAt;

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
        UpdateForecastHint();
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
        // Disable the canvas while we tear down + rebuild children. Otherwise Destroy() is
        // deferred to end-of-frame and the just-added new Graphics race the still-pending
        // dirty-list entries of the old ones inside CanvasUpdateRegistry.PerformUpdate
        // (the "IndexedSet OOB" Unity UGUI bug).
        bool wasEnabled = _canvas != null && _canvas.enabled;
        if (_canvas != null) _canvas.enabled = false;
        ClearArrows();
        AddSideArrows(state.OpponentSide, true);
        AddSideArrows(state.PlayerSide, false);
        ComputeDamageStacking();
        if (_canvas != null) _canvas.enabled = wasEnabled;
    }

    private void ComputeDamageStacking()
    {
        // Group arrows by their target. Within each group, order by attacker speed descending
        // (highest speed strikes first). Each arrow inherits the cumulative damage of all
        // earlier-striking attackers on the same target, so its forecast displays the HP the
        // target will have AFTER preceding attackers have already landed.
        var byTarget = new Dictionary<IGameEntity, List<ArrowView>>();
        foreach (var a in _arrows)
        {
            if (a.Target == null) continue;
            if (!byTarget.TryGetValue(a.Target, out var list))
                byTarget[a.Target] = list = new List<ArrowView>();
            list.Add(a);
        }

        foreach (var kv in byTarget)
        {
            var list = kv.Value;
            // Stable sort by speed desc; ties keep insertion order.
            list.Sort((x, y) => (y.Source?.CurrentSpeed ?? 0).CompareTo(x.Source?.CurrentSpeed ?? 0));
            int cumulative = 0;
            for (int i = 0; i < list.Count; i++)
            {
                list[i].DamageBefore = cumulative;
                list[i].OrderIndex = i;
                cumulative += Mathf.Max(0, list[i].Source?.Attack ?? 0);
            }
        }
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
        shaft.maskable = false;
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
        head.maskable = false;
        head.color = color;
        head.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        head.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        head.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        head.rectTransform.sizeDelta = new Vector2(44f, 44f);

        // Damage forecast badge — shows resulting HP of the target if this attack lands.
        var badgeGo = new GameObject("ForecastBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeGo.transform.SetParent(root.transform, false);
        var badgeImg = badgeGo.GetComponent<Image>();
        badgeImg.color = new Color(0.05f, 0.03f, 0.08f, 0.92f);
        badgeImg.raycastTarget = false;
        badgeImg.maskable = false;
        var badgeRect = badgeImg.rectTransform;
        badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
        badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.sizeDelta = new Vector2(58f, 26f);
        var badgeOl = badgeGo.AddComponent<Outline>();
        badgeOl.effectColor = color;
        badgeOl.effectDistance = new Vector2(1.2f, -1.2f);

        var badgeTextGo = new GameObject("Text", typeof(RectTransform));
        badgeTextGo.transform.SetParent(badgeGo.transform, false);
        var badgeText = badgeTextGo.AddComponent<TextMeshProUGUI>();
        badgeText.fontSize = 18f;
        badgeText.fontStyle = FontStyles.Bold;
        badgeText.alignment = TextAlignmentOptions.Center;
        badgeText.raycastTarget = false;
        badgeText.maskable = false;
        badgeText.color = new Color(1f, 0.96f, 0.85f, 1f);
        var btRect = badgeText.rectTransform;
        btRect.anchorMin = Vector2.zero;
        btRect.anchorMax = Vector2.one;
        btRect.pivot = new Vector2(0.5f, 0.5f);
        btRect.offsetMin = new Vector2(2f, 0f);
        btRect.offsetMax = new Vector2(-2f, 0f);

        return new ArrowView
        {
            Root = root,
            Shaft = shaft.rectTransform,
            ShaftImage = shaft,
            Head = head.rectTransform,
            HeadText = head,
            ForecastBadge = badgeRect,
            ForecastBadgeImage = badgeImg,
            ForecastText = badgeText,
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
        tmp.maskable = false;
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

            UpdateForecastBadge(arrow, to, direction);
        }

        TryShowForecastHint();
    }

    private void UpdateForecastBadge(ArrowView arrow, Vector2 targetCenter, Vector2 direction)
    {
        if (arrow.ForecastBadge == null || arrow.ForecastText == null) return;
        var src = arrow.Source;
        var tgt = arrow.Target;
        if (src == null || tgt == null)
        {
            if (arrow.LastActive)
            {
                arrow.ForecastBadge.gameObject.SetActive(false);
                arrow.LastActive = false;
            }
            return;
        }

        int incoming = Mathf.Max(0, src.Attack);
        // Effective HP at the moment THIS arrow strikes: original HP minus damage dealt by
        // earlier-striking attackers on the same target.
        int effectiveHpBefore = Mathf.Max(0, tgt.Health - arrow.DamageBefore);
        int targetHpAfter = Mathf.Max(0, effectiveHpBefore - incoming);
        bool alreadyDead = effectiveHpBefore <= 0;
        bool targetDies = !alreadyDead && incoming >= effectiveHpBefore;

        string label;
        Color textColor;
        if (alreadyDead)
        {
            // First attacker already killed the target — this arrow is "overkill".
            label = $"<color=#9c9aa0>overkill</color>";
            textColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        }
        else if (targetDies)
        {
            label = $"{effectiveHpBefore} -> <color=#ff5e4a>KO</color>";
            textColor = new Color(1f, 0.42f, 0.32f, 1f);
        }
        else
        {
            label = $"{effectiveHpBefore} -> {targetHpAfter}";
            textColor = new Color(1f, 0.95f, 0.85f, 1f);
        }
        // Mark the order so player can see who hits first.
        if (arrow.OrderIndex > 0 && !alreadyDead)
            label = $"<size=10><color=#9c9aa0>#{arrow.OrderIndex + 1}</color></size>  " + label;

        // If this is a clash, append an initiative read so the player can see who likely
        // strikes first. Comparing source speed range to target speed range; src wins if
        // the worst-case src roll still beats the best-case target roll, etc.
        if (arrow.IsClash && tgt is BoardCard targetBc && src != null)
        {
            int srcMin = src.SourceCard != null ? src.SourceCard.MinSpeed : src.CurrentSpeed;
            int srcMax = src.SourceCard != null ? src.SourceCard.MaxSpeed : src.CurrentSpeed;
            int tMin = targetBc.SourceCard != null ? targetBc.SourceCard.MinSpeed : targetBc.CurrentSpeed;
            int tMax = targetBc.SourceCard != null ? targetBc.SourceCard.MaxSpeed : targetBc.CurrentSpeed;

            string initColor;
            string initIcon;
            if (srcMin > tMax)        { initColor = "#7ad48b"; initIcon = "SPD+"; }   // strictly first
            else if (srcMax < tMin)   { initColor = "#ff7a6e"; initIcon = "SPD-"; }   // strictly second
            else                      { initColor = "#ffd864"; initIcon = "SPD="; }   // overlap

            label += $"\n<size=12><color={initColor}>{initIcon} {srcMin}-{srcMax} vs {tMin}-{tMax}</color></size>";
            var wanted = new Vector2(96f, 42f);
            if (arrow.LastSize != wanted) { arrow.ForecastBadge.sizeDelta = wanted; arrow.LastSize = wanted; }
        }
        else
        {
            bool needsWide = arrow.OrderIndex > 0 || alreadyDead;
            var wanted = new Vector2(needsWide ? 80f : 58f, 26f);
            if (arrow.LastSize != wanted) { arrow.ForecastBadge.sizeDelta = wanted; arrow.LastSize = wanted; }
        }
        Color bgColor = targetDies
            ? new Color(0.22f, 0.05f, 0.06f, 0.96f)
            : new Color(0.05f, 0.03f, 0.08f, 0.92f);

        // Only write to UI when something actually changed — touching Canvas every frame
        // is what triggers the IndexedSet OOB bug in CanvasUpdateRegistry.
        if (label != arrow.LastLabel)
        {
            arrow.ForecastText.text = label;
            arrow.LastLabel = label;
        }
        if (textColor != arrow.LastColor)
        {
            arrow.ForecastText.color = textColor;
            arrow.LastColor = textColor;
        }
        if (arrow.ForecastBadgeImage != null && bgColor != arrow.LastBgColor)
        {
            arrow.ForecastBadgeImage.color = bgColor;
            arrow.LastBgColor = bgColor;
        }

        // Place the badge offset perpendicular to the arrow direction, near the target end.
        var perp = new Vector2(-direction.y, direction.x);
        var badgePos = targetCenter + perp * 28f - direction * 28f;
        arrow.ForecastBadge.anchoredPosition = badgePos;
        if (!arrow.LastActive)
        {
            arrow.ForecastBadge.gameObject.SetActive(true);
            arrow.LastActive = true;
        }
    }

    private void TryShowForecastHint()
    {
        if (_arrows.Count == 0) return;
        if (PlayerPrefs.GetInt(ForecastHintPrefKey, 0) == 1) return;
        if (_forecastHintRoot != null) return;

        var go = new GameObject("ForecastHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -160f);
        rt.sizeDelta = new Vector2(720f, 70f);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.05f, 0.03f, 0.08f, 0.94f);
        img.raycastTarget = false;
        img.maskable = false;
        var ol = go.AddComponent<Outline>();
        ol.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.95f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        var tipGo = new GameObject("Text", typeof(RectTransform));
        tipGo.transform.SetParent(go.transform, false);
        var tip = tipGo.AddComponent<TextMeshProUGUI>();
        tip.text = "Цифры у стрелки = HP цели <b>до</b> -> <b>после</b> удара.  <color=#ff5e4a><b>KO</b></color> — карта погибнет.  Если в цель бьют двое, у второй стрелки показано HP <b>после</b> первого удара (с пометкой <color=#9c9aa0>#2</color>).";
        tip.fontSize = 20f;
        tip.alignment = TextAlignmentOptions.Center;
        tip.color = new Color(1f, 0.95f, 0.88f, 1f);
        tip.enableWordWrapping = true;
        tip.raycastTarget = false;
        tip.maskable = false;
        tip.richText = true;
        var tipRect = tip.rectTransform;
        tipRect.anchorMin = Vector2.zero;
        tipRect.anchorMax = Vector2.one;
        tipRect.offsetMin = new Vector2(20f, 8f);
        tipRect.offsetMax = new Vector2(-20f, -8f);

        _forecastHintRoot = go;
        _forecastHintHideAt = Time.unscaledTime + 6f;
        PlayerPrefs.SetInt(ForecastHintPrefKey, 1);
        PlayerPrefs.Save();
    }

    private void UpdateForecastHint()
    {
        if (_forecastHintRoot == null) return;
        if (Time.unscaledTime >= _forecastHintHideAt)
        {
            Destroy(_forecastHintRoot);
            _forecastHintRoot = null;
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
        {
            if (label == null) continue;
            // Deactivate FIRST so the Graphic unregisters from the canvas dirty-list
            // synchronously, then schedule destroy. Prevents IndexedSet OOB.
            label.gameObject.SetActive(false);
            Destroy(label.gameObject);
        }
        _clashLabels.Clear();

        if (_forecastHintRoot != null)
        {
            _forecastHintRoot.SetActive(false);
            Destroy(_forecastHintRoot);
            _forecastHintRoot = null;
        }
    }

    private static void DestroyArrow(ArrowView arrow)
    {
        if (arrow?.Root == null) return;
        // SetActive(false) first — synchronous unregister from CanvasUpdateRegistry,
        // then defer the actual destroy.
        arrow.Root.SetActive(false);
        Object.Destroy(arrow.Root);
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
        public RectTransform ForecastBadge;
        public Image ForecastBadgeImage;
        public TextMeshProUGUI ForecastText;
        public BoardCard Source;
        public IGameEntity Target;
        public BoardSlotUI SourceSlot;
        public BoardSlotUI TargetSlot;
        public bool IsClash;
        // Damage already dealt to Target by earlier attackers this turn (sum of their Attack).
        // When two cards attack the same target, the second arrow's forecast shows the HP
        // AFTER the first attacker landed, not the raw target HP.
        public int DamageBefore;
        public int OrderIndex; // 0 = first attacker on this target

        // Last-applied UI state — avoid touching Canvas every frame when nothing changed.
        public string LastLabel;
        public Color LastColor;
        public Color LastBgColor;
        public Vector2 LastSize;
        public bool LastActive;
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
