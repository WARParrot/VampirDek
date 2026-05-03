using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Combat;
using Definitions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardView : MonoBehaviour
{
    public GameObject SlotPrefab;
    public Transform PlayerBoardContainer;
    public Transform OpponentBoardContainer;

    private DuelManager _duelManager;
    private Dictionary<string, BoardSlotUI> _slotUIs = new();
    private bool _subscribed = false;

    public void SetDuelManager(DuelManager duelManager)
    {
        _duelManager = duelManager;
        StartCoroutine(InitUI());
    }

    IEnumerator InitUI()
    {
        yield return new WaitUntil(() =>
            GlobalServices.EventBus != null &&
            (GlobalServices.Director.CurrentMode as DuelManager)?.CurrentDuelState != null);

        _duelManager = GlobalServices.Director.CurrentMode as DuelManager;
        var encounter = _duelManager.CurrentDuelState.Encounter;

        InitializeLayout(encounter.PlayerBoardLayout, PlayerBoardContainer, true);
        InitializeLayout(encounter.OpponentBoardLayout, OpponentBoardContainer, false);

        if (!_subscribed)
        {
            GlobalServices.EventBus.Subscribe<PlacedCardEvent>(OnCardPlaced);
            GlobalServices.EventBus.Subscribe<TownPlacedEvent>(OnTownPlaced);
            GlobalServices.EventBus.Subscribe<EntityDiedEvent>(OnEntityDied);
            _subscribed = true;
        }
    }

    void OnDisable()
    {
        if (_subscribed && GlobalServices.EventBus != null)
        {
            GlobalServices.EventBus.Unsubscribe<PlacedCardEvent>(OnCardPlaced);
            GlobalServices.EventBus.Unsubscribe<TownPlacedEvent>(OnTownPlaced);
            GlobalServices.EventBus.Unsubscribe<EntityDiedEvent>(OnEntityDied);
            _subscribed = false;
        }
    }

    private void InitializeLayout(BoardLayoutData layout, Transform container, bool isPlayer)
{
    if (layout == null)
    {
        Debug.LogError("BoardLayoutData is null – check your CombatEncounter asset.");
        return;
    }

    foreach (Transform child in container)
        Destroy(child.gameObject);

    var rows = new (string name, int count, RowType type)[]
    {
        ("Vanguard", layout.VanguardSlotsCount, RowType.Vanguard),
        ("Building",  layout.BuildingSlotsCount, RowType.Building),
        ("Human",     layout.HumanSlotsCount, RowType.Human),
        ("Town",      1, RowType.Town)
    };

    foreach (var (rowName, count, rowType) in rows)
    {
        var rowObj = new GameObject(rowName, typeof(HorizontalLayoutGroup));
        rowObj.transform.SetParent(container, false);

        var layoutRow = rowObj.GetComponent<HorizontalLayoutGroup>();
        layoutRow.spacing = 5f;
        layoutRow.childAlignment = TextAnchor.MiddleCenter;

        layoutRow.childControlHeight = false;
        layoutRow.childControlWidth = false;

        layoutRow.childForceExpandHeight = false;
        layoutRow.childForceExpandWidth = false;

        for (int i = 0; i < count; i++)
        {
            GameObject slot;
            try
            {
                slot = Instantiate(SlotPrefab, rowObj.transform);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to instantiate SlotPrefab. Is it assigned in the Inspector? Error: {e.Message}");
                return;
            }

            var indexTextComponent = slot.transform.Find("SlotIndex");
            if (indexTextComponent == null)
            {
                // Try TMPro first, then legacy Text
                var tmp = slot.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp == null)
                    Debug.LogError("BoardSlot prefab has no 'SlotIndex' child with a Text or TextMeshPro component.", slot);
                else
                    tmp.text = $"{rowName[0]}{i}";
            }
            else
            {
                var txt = indexTextComponent.GetComponent<Text>();
                if (txt == null)
                {
                    var tmp = indexTextComponent.GetComponent<TextMeshProUGUI>();
                    if (tmp == null)
                        Debug.LogError("'SlotIndex' child has neither Text nor TextMeshPro component.", slot);
                    else
                        tmp.text = $"{rowName[0]}{i}";
                }
                else
                {
                    txt.text = $"{rowName[0]}{i}";
                }
            }

            var ui = slot.GetComponent<BoardSlotUI>();
            if (ui == null)
            {
                Debug.LogError("BoardSlot prefab is missing BoardSlotUI component.", slot);
                continue;
            }

            string key = $"{(isPlayer ? "P" : "O")}_{rowType}_{i}";
            _slotUIs[key] = ui;
        }
    }

    Debug.Log($"Board layout initialized for {(isPlayer ? "Player" : "Opponent")}.");
}

    public void ShowValidDropZones(Definitions.RowType cardRowType)
    {
        foreach (var kv in _slotUIs)
        {
            var parts = kv.Key.Split('_');
            if (parts.Length != 3) continue;
            bool isPlayer = parts[0] == "P";
            Definitions.RowType slotRow;
            if (!System.Enum.TryParse(parts[1], out slotRow)) continue;
            int index = int.Parse(parts[2]);

            bool isValid = false;
            if (isPlayer && cardRowType == slotRow)
            {
                var board = _duelManager.CurrentDuelState.PlayerSide.Board;
                isValid = IsSlotEmpty(board, slotRow, index);
            }
            kv.Value.IsValidDropTarget = isValid;
            kv.Value.SetHighlight(isValid);
        }
    }

    public void HideAllHighlights()
    {
        foreach (var ui in _slotUIs.Values)
        {
            ui.SetHighlight(false);
            ui.IsValidDropTarget = false;
        }
    }

    private bool IsSlotEmpty(Board board, Definitions.RowType row, int index)
    {
        switch (row)
        {
            case Definitions.RowType.Vanguard: return index < board.VanguardRow.Length && board.VanguardRow[index].IsEmpty;
            case Definitions.RowType.Building: return index < board.BuildingRow.Length && board.BuildingRow[index].IsEmpty;
            case Definitions.RowType.Human:    return index < board.HumanRow.Length && board.HumanRow[index].IsEmpty;
            case Definitions.RowType.Town:     return board.TownSlot.IsEmpty;
        }
        return false;
    }

    private void OnCardPlaced(PlacedCardEvent e) => UpdateSlotUIForEntity(e.Card);
    private void OnTownPlaced(TownPlacedEvent e) => UpdateSlotUIForEntity(e.Town);
    private void OnEntityDied(EntityDiedEvent e) => UpdateSlotUIForEntity(e.Entity as BoardCard);

    private void UpdateSlotUIForEntity(BoardCard card)
    {
        if (card == null) return;
        var state = _duelManager.CurrentDuelState;
        FindAndUpdate(state.PlayerSide.Board, card);
        FindAndUpdate(state.OpponentSide.Board, card);
    }

    private void FindAndUpdate(Board board, BoardCard card)
    {
        if (card == null) return;
        if (board.TownSlot.Occupant == card) { UpdateSlotUI(board, Definitions.RowType.Town, 0, card); return; }
        void CheckRow(BoardSlot[] slots, Definitions.RowType row)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Occupant == card) { UpdateSlotUI(board, row, i, card); return; }
        }
        CheckRow(board.VanguardRow, Definitions.RowType.Vanguard);
        CheckRow(board.BuildingRow, Definitions.RowType.Building);
        CheckRow(board.HumanRow, Definitions.RowType.Human);
    }

    private void UpdateSlotUI(Board board, Definitions.RowType row, int index, BoardCard card)
    {
        bool isPlayer = board == _duelManager?.CurrentDuelState?.PlayerSide.Board;
        string key = $"{(isPlayer ? "P" : "O")}_{row}_{index}";
        if (!_slotUIs.TryGetValue(key, out var ui)) return;

        var nameText = ui.transform.Find("CardName").GetComponent<TextMeshProUGUI>();
        var statsText = ui.transform.Find("CardStats").GetComponent<TextMeshProUGUI>();
        if (card == null || !card.IsAlive)
        {
            nameText.text = statsText.text = "";
        }
        else
        {
            nameText.text = card.SourceCard.CardName;
            statsText.text = $"{card.Health}/{card.MaxHealth} ATK{card.Attack}";
        }
    }
}
