using System.Collections;
using System.Collections.Generic;
using Combat;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.UI;

public class BoardView : MonoBehaviour
{
    private const string GeneratedPrefix = "GeneratedBoardSlot_";

    public GameObject SlotPrefab;
    public GameObject RowPrefab;
    public Transform PlayerBoardContainer;
    public Transform OpponentBoardContainer;

    private readonly Dictionary<string, BoardSlotUI> _slotUIs = new();
    private bool _subscribed = false;
    private Definitions.RowType? _activeDropPreviewRow;

    public void Start()
    {
        StartCoroutine(InitUI());
    }

    private IEnumerator InitUI()
    {
        yield return new WaitUntil(() => DuelManagerProxy.Instance != null && DuelManagerProxy.Instance.CurrentDuelState != null);
        BuildSlots();
        SubscribeEvents();
        RefreshAllSlots();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (_subscribed || GlobalServices.EventBus == null) return;
        GlobalServices.EventBus.Subscribe<PlacedCardEvent>(OnBoardChanged);
        GlobalServices.EventBus.Subscribe<EntityDiedEvent>(OnEntityChanged);
        GlobalServices.EventBus.Subscribe<ActionExecutedEvent>(OnActionExecuted);
        _subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!_subscribed || GlobalServices.EventBus == null) return;
        GlobalServices.EventBus.Unsubscribe<PlacedCardEvent>(OnBoardChanged);
        GlobalServices.EventBus.Unsubscribe<EntityDiedEvent>(OnEntityChanged);
        GlobalServices.EventBus.Unsubscribe<ActionExecutedEvent>(OnActionExecuted);
        _subscribed = false;
    }

    private void OnBoardChanged(PlacedCardEvent evt) => RefreshAllSlots();
    private void OnEntityChanged(EntityDiedEvent evt) => RefreshAllSlots();
    private void OnActionExecuted(ActionExecutedEvent evt) => RefreshAllSlots();

    private void BuildSlots()
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        if (state == null) return;
        if (_slotUIs.Count > 0) return;

        BindSideSlots(state.PlayerSide.Board, PlayerBoardContainer, "Player");
        BindSideSlots(state.OpponentSide.Board, OpponentBoardContainer, "Opponent");
    }

    private void BindSideSlots(Board board, Transform container, string sideName)
    {
        if (board == null || container == null) return;

        ClearGeneratedSlots(container);

        if (TryBindAuthoredSlots(board, container))
        {
            return;
        }

        CreatePrefabSlots(board, container, sideName);
    }

    private bool TryBindAuthoredSlots(Board board, Transform container)
    {
        var rowOrder = GetRows(board);
        var groupedRows = new Dictionary<Definitions.RowType, List<BoardSlotUI>>();
        var groupedCount = 0;

        foreach (var rowType in rowOrder)
        {
            var rowSlots = FindAuthoredSlotsForNamedRow(container, rowType);
            if (rowSlots.Count > 0)
            {
                groupedRows[rowType] = rowSlots;
                groupedCount++;
            }
        }

        if (groupedCount > 0)
        {
            if (groupedCount != rowOrder.Count)
            {
                Debug.LogWarning($"[BoardView] {container.name} has only {groupedCount}/{rowOrder.Count} authored row groups. Add row containers named Vanguard, Building, Human, and Town, or assign SlotPrefab for generated slots.");
                return false;
            }

            foreach (var rowType in rowOrder)
            {
                if (!BindRow(board, rowType, groupedRows[rowType], container.name)) return false;
            }

            return true;
        }

        var flatSlots = FindAuthoredSlots(container);
        var expectedCount = GetExpectedSlotCount(board);
        if (flatSlots.Count == 0) return false;

        if (flatSlots.Count < expectedCount)
        {
            Debug.LogWarning($"[BoardView] {container.name} has {flatSlots.Count} authored BoardSlotUI components, but the board needs {expectedCount}. Add the missing authored slots or assign SlotPrefab.");
            return false;
        }

        var cursor = 0;
        foreach (var rowType in rowOrder)
        {
            var boardSlots = GetBoardSlots(board, rowType);
            var rowSlots = new List<BoardSlotUI>();
            for (var i = 0; i < boardSlots.Length; i++)
            {
                rowSlots.Add(flatSlots[cursor++]);
            }

            if (!BindRow(board, rowType, rowSlots, container.name)) return false;
        }

        return true;
    }

    private bool BindRow(Board board, Definitions.RowType rowType, List<BoardSlotUI> slotUIs, string containerName)
    {
        var boardSlots = GetBoardSlots(board, rowType);
        if (slotUIs.Count < boardSlots.Length)
        {
            Debug.LogWarning($"[BoardView] {containerName}/{rowType} has {slotUIs.Count} authored slots, but the board needs {boardSlots.Length}.");
            return false;
        }

        for (var displayIndex = 0; displayIndex < boardSlots.Length; displayIndex++)
        {
            var boardSlot = boardSlots[displayIndex];
            var slotUI = slotUIs[displayIndex];
            ConfigureSlotUI(board, rowType, boardSlot.Index, slotUI);
        }

        return true;
    }

    private void CreatePrefabSlots(Board board, Transform container, string sideName)
    {
        if (SlotPrefab == null)
        {
            Debug.LogError($"[BoardView] {container.name} has no authored BoardSlotUI children and SlotPrefab is not assigned. Board layout is a scene/prefab responsibility; assign authored slots or a BoardSlot prefab.");
            return;
        }

        if (RowPrefab == null && !HasAllAuthoredRowContainers(board, container))
        {
            Debug.LogError($"[BoardView] {container.name} has no authored row containers and RowPrefab is not assigned. Generated boards require an authored row prefab so layout stays outside BoardView.");
            return;
        }

        foreach (var rowType in GetRows(board))
        {
            var rowParent = FindRowContainer(container, rowType) ?? CreateGeneratedRow(container, sideName, rowType);
            if (rowParent == null) continue;

            var boardSlots = GetBoardSlots(board, rowType);
            for (var displayIndex = 0; displayIndex < boardSlots.Length; displayIndex++)
            {
                var go = Instantiate(SlotPrefab, rowParent);
                go.name = $"{GeneratedPrefix}{sideName}_{rowType}_{displayIndex}";
                
                if (sideName == "Opponent")
                {
                    go.transform.localRotation = Quaternion.Euler(0, 0, 180);
                }

                var slotUI = go.GetComponent<BoardSlotUI>();
                if (slotUI == null)
                {
                    Debug.LogError($"[BoardView] SlotPrefab '{SlotPrefab.name}' must contain BoardSlotUI. Skipping generated slot {sideName}/{rowType}/{displayIndex}.");
                    DestroyBoardObject(go);
                    continue;
                }

                ConfigureSlotUI(board, rowType, boardSlots[displayIndex].Index, slotUI);
            }

            ClearGeneratedRowLabel(rowParent);
        }
    }

    private void ClearGeneratedRowLabel(Transform rowParent)
    {
        // Only touch an explicitly authored RowLabel. GetComponentInChildren can grab the first
        // slot's card/index text, which makes that first board card look larger and adds stray labels.
        var labelTransform = rowParent != null ? rowParent.Find("RowLabel") : null;
        var label = labelTransform != null ? labelTransform.GetComponent<TMPro.TextMeshProUGUI>() : null;
        if (label == null) return;

        label.text = string.Empty;
        label.raycastTarget = false;
    }

    private Transform CreateGeneratedRow(Transform container, string sideName, Definitions.RowType rowType)
    {
        if (RowPrefab == null) return null;

        var row = Instantiate(RowPrefab, container);
        row.name = $"{GeneratedPrefix}{sideName}_{rowType}_Row";
        return row.transform;
    }

    private bool HasAllAuthoredRowContainers(Board board, Transform container)
    {
        foreach (var rowType in GetRows(board))
        {
            if (FindRowContainer(container, rowType) == null) return false;
        }

        return true;
    }

    private void ConfigureSlotUI(Board board, Definitions.RowType rowType, int index, BoardSlotUI slotUI)
    {
        if (slotUI == null) return;

        slotUI.Configure(board, rowType, index);
        if (slotUI.HighlightImage == null)
        {
            var highlight = slotUI.transform.Find("Highlight")?.GetComponent<Image>();
            slotUI.HighlightImage = highlight != null ? highlight : slotUI.GetComponent<Image>();
        }

        var image = slotUI.GetComponent<Image>();
        if (image != null) image.raycastTarget = true;

        _slotUIs[Key(board, rowType, index)] = slotUI;
    }

    private List<BoardSlotUI> FindAuthoredSlotsForNamedRow(Transform container, Definitions.RowType rowType)
    {
        var slots = new List<BoardSlotUI>();
        var rowName = rowType.ToString();

        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (!NameContains(child.name, rowName)) continue;
            AddAuthoredSlots(child, slots);
        }

        return slots;
    }

    private List<BoardSlotUI> FindAuthoredSlots(Transform container)
    {
        var slots = new List<BoardSlotUI>();
        AddAuthoredSlots(container, slots);
        return slots;
    }

    private void AddAuthoredSlots(Transform root, List<BoardSlotUI> slots)
    {
        var found = root.GetComponentsInChildren<BoardSlotUI>(true);
        foreach (var slot in found)
        {
            if (slot == null || slot.name.StartsWith(GeneratedPrefix)) continue;
            slots.Add(slot);
        }
    }

    private Transform FindRowContainer(Transform container, Definitions.RowType rowType)
    {
        var rowName = rowType.ToString();
        for (var i = 0; i < container.childCount; i++)
        {
            var child = container.GetChild(i);
            if (NameContains(child.name, rowName)) return child;
        }

        return null;
    }

    private static bool NameContains(string value, string term)
    {
        return value != null && value.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private List<Definitions.RowType> GetRows(Board board)
    {
        var rows = new List<Definitions.RowType>
        {
            Definitions.RowType.Vanguard,
            Definitions.RowType.Building,
            Definitions.RowType.Human
        };

        if (board?.TownSlot != null) rows.Add(Definitions.RowType.Town);
        return rows;
    }

    private static BoardSlot[] GetBoardSlots(Board board, Definitions.RowType rowType)
    {
        if (board == null) return System.Array.Empty<BoardSlot>();
        return rowType switch
        {
            Definitions.RowType.Vanguard => board.VanguardRow ?? System.Array.Empty<BoardSlot>(),
            Definitions.RowType.Building => board.BuildingRow ?? System.Array.Empty<BoardSlot>(),
            Definitions.RowType.Human => board.HumanRow ?? System.Array.Empty<BoardSlot>(),
            Definitions.RowType.Town => board.TownSlot != null ? new[] { board.TownSlot } : System.Array.Empty<BoardSlot>(),
            _ => System.Array.Empty<BoardSlot>()
        };
    }

    private static int GetExpectedSlotCount(Board board)
    {
        if (board == null) return 0;
        return (board.VanguardRow?.Length ?? 0) +
               (board.BuildingRow?.Length ?? 0) +
               (board.HumanRow?.Length ?? 0) +
               (board.TownSlot != null ? 1 : 0);
    }

    private void ClearGeneratedSlots(Transform container)
    {
        ClearGeneratedSlotsRecursive(container);
    }

    private void ClearGeneratedSlotsRecursive(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(GeneratedPrefix))
            {
                DestroyBoardObject(child.gameObject);
                continue;
            }

            ClearGeneratedSlotsRecursive(child);
        }
    }

    private static void DestroyBoardObject(GameObject go)
    {
        if (go == null) return;
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    public void RefreshAllSlots()
    {
        if (DuelManagerProxy.Instance?.CurrentDuelState == null) return;
        if (_slotUIs.Count == 0) BuildSlots();

        foreach (var slotUI in _slotUIs.Values)
        {
            RefreshSlot(slotUI);
        }
    }

    private void RefreshSlot(BoardSlotUI slotUI)
    {
        if (slotUI == null) return;

        slotUI.SetDisplay(slotUI.Occupant);

        if (_activeDropPreviewRow.HasValue)
        {
            ApplyDropZoneAffordance(slotUI, _activeDropPreviewRow.Value);
            return;
        }

        slotUI.SetSlotAffordance(CardAffordanceState.None);
        slotUI.SetCardAffordance(slotUI.Occupant != null && slotUI.Occupant.PlannedTarget != null
            ? CardAffordanceState.Planned
            : CardAffordanceState.None);
    }

    public void ShowValidDropZones(Definitions.RowType rowType)
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        if (state == null) return;
        if (_slotUIs.Count == 0) BuildSlots();

        _activeDropPreviewRow = rowType;
        foreach (var slotUI in _slotUIs.Values)
        {
            ApplyDropZoneAffordance(slotUI, rowType);
        }
    }

    private void ApplyDropZoneAffordance(BoardSlotUI slotUI, Definitions.RowType rowType)
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        if (state == null || slotUI == null) return;

        bool isPlayerSlot = slotUI.Board == state.PlayerSide.Board;
        var slot = slotUI.Board?.GetSlot(slotUI.RowType, slotUI.Index);
        bool rowMatches = slotUI.RowType == rowType;
        bool canDrop = isPlayerSlot && rowMatches && slot != null && slot.IsEmpty;
        slotUI.IsValidDropTarget = canDrop;

        if (!isPlayerSlot)
        {
            slotUI.SetSlotAffordance(CardAffordanceState.None);
        }
        else if (canDrop)
        {
            slotUI.SetSlotAffordance(CardAffordanceState.Compatible);
        }
        else if (rowMatches && slot != null && !slot.IsEmpty)
        {
            slotUI.SetSlotAffordance(CardAffordanceState.Blocked);
        }
        else
        {
            slotUI.SetSlotAffordance(CardAffordanceState.Incompatible);
        }
    }

    public void HideAllHighlights()
    {
        _activeDropPreviewRow = null;
        foreach (var slotUI in _slotUIs.Values)
        {
            slotUI.IsValidDropTarget = false;
            slotUI.SetSlotAffordance(CardAffordanceState.None);
        }
    }

    public void SetCardHighlight(BoardCard card, Color color)
    {
        if (color == Color.white || color == Color.clear)
            SetCardAffordance(card, CardAffordanceState.None);
        else if (color == Color.cyan)
            SetCardAffordance(card, CardAffordanceState.Selected);
        else if (color == Color.red)
            SetCardAffordance(card, CardAffordanceState.Target);
        else
            SetCardAffordance(card, CardAffordanceState.Planned);
    }

    public void SetCardAffordance(BoardCard card, CardAffordanceState state)
    {
        if (card == null) return;
        var slotUI = FindSlotForCard(card);
        if (slotUI == null) return;
        slotUI.SetCardAffordance(state);
    }

    public BoardSlotUI FindFirstEmptyPlayerSlot(Definitions.RowType rowType)
    {
        var state = DuelManagerProxy.Instance?.CurrentDuelState;
        if (state == null) return null;
        if (_slotUIs.Count == 0) BuildSlots();

        foreach (var slotUI in _slotUIs.Values)
        {
            if (slotUI == null || slotUI.Board != state.PlayerSide.Board) continue;
            if (slotUI.RowType != rowType) continue;
            var slot = slotUI.Board?.GetSlot(slotUI.RowType, slotUI.Index);
            if (slot != null && slot.IsEmpty) return slotUI;
        }

        return null;
    }

    public BoardSlotUI FindFirstOccupiedSlot(Board board, System.Func<BoardCard, bool> filter)
    {
        if (board == null || filter == null) return null;
        if (_slotUIs.Count == 0) BuildSlots();

        foreach (var slotUI in _slotUIs.Values)
        {
            if (slotUI == null || slotUI.Board != board) continue;
            var occupant = slotUI.Occupant;
            if (filter(occupant)) return slotUI;
        }

        return null;
    }

    private BoardSlotUI FindSlotForCard(BoardCard card)
    {
        foreach (var slotUI in _slotUIs.Values)
        {
            if (slotUI.Occupant == card) return slotUI;
        }
        return null;
    }


    public IEnumerable<BoardSlotUI> GetSlotUIs() => _slotUIs.Values;

    private static string Key(Board board, Definitions.RowType row, int index) => $"{board.GetHashCode()}:{row}:{index}";
}
