using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core;
using Combat;
using Combat.UI;
using Definitions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardView : MonoBehaviour
{
    public GameObject SlotPrefab;
    public Transform PlayerBoardContainer;
    public Transform OpponentBoardContainer;

    [Header("Empty slot texts")]
    [SerializeField] private string _emptySlotName = "New Text";
    [SerializeField] private string _emptySlotStats = "";

    private DuelManager _duelManager;
    private Dictionary<string, BoardSlotUI> _slotUIs = new();
    private bool _subscribed = false;

    public void Start()
    {
        StartCoroutine(InitUI());
    }

    IEnumerator InitUI()
    {
        Debug.Log("[BoardView] InitUI started - waiting for proxy...");
        yield return new WaitUntil(() => DuelManagerProxy.Instance != null);
        Debug.Log("[BoardView] Proxy acquired.");

        yield return new WaitUntil(() => DuelManagerProxy.Instance.CurrentDuelState != null);
        Debug.Log("[BoardView] DuelState ready. Building layout...");

        _duelManager = DuelManagerProxy.Instance;
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

        SyncExistingCards();
    }

    void SyncExistingCards()
    {
        foreach (var slot in _duelManager.CurrentDuelState.PlayerSide.Board.AllSlots())
            if (slot.Occupant != null) UpdateSlotUIForEntity(slot.Occupant);
        foreach (var slot in _duelManager.CurrentDuelState.OpponentSide.Board.AllSlots())
            if (slot.Occupant != null) UpdateSlotUIForEntity(slot.Occupant);
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

    public void RefreshAllSlots()
    {
        if (_duelManager?.CurrentDuelState == null) return;

        foreach (var slot in _duelManager.CurrentDuelState.PlayerSide.Board.AllSlots())
            UpdateSlotUIForEntity(slot.Occupant);

        foreach (var slot in _duelManager.CurrentDuelState.OpponentSide.Board.AllSlots())
            UpdateSlotUIForEntity(slot.Occupant);
    }

    private void InitializeLayout(BoardLayoutData layout, Transform container, bool isPlayer)
    {
        if (layout == null)
        {
            Debug.LogError("BoardLayoutData is null - check your CombatEncounter asset.");
            return;
        }

        foreach (Transform child in container)
            Destroy(child.gameObject);

        var rows = new (string name, int count, Definitions.RowType type)[]
        {
            ("Vanguard", layout.VanguardSlotsCount, Definitions.RowType.Vanguard),
            ("Building",  layout.BuildingSlotsCount, Definitions.RowType.Building),
            ("Human",     layout.HumanSlotsCount, Definitions.RowType.Human),
            ("Town",      1, Definitions.RowType.Town)
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
                catch (Exception e)
                {
                    Debug.LogError($"Failed to instantiate SlotPrefab. Is it assigned in the Inspector? Error: {e.Message}");
                    return;
                }

                var indexTextComponent = slot.transform.Find("SlotIndex");
                if (indexTextComponent == null)
                {
                    var tmp = slot.GetComponentInChildren<TextMeshProUGUI>();
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

                // Инициализация заглушек для имени и статов (пустой слот)
                var cn = slot.transform.Find("CardName");
                if (cn != null)
                {
                    var tmp = cn.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = _emptySlotName;
                }

                var cs = slot.transform.Find("CardStats");
                if (cs != null)
                {
                    var tmp = cs.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = _emptySlotStats;
                }

                var ui = slot.GetComponent<BoardSlotUI>();
                if (ui != null)
                {
                    ui.Board = isPlayer ? _duelManager.CurrentDuelState.PlayerSide.Board : _duelManager.CurrentDuelState.OpponentSide.Board;
                    ui.RowType = rowType;
                    ui.Index = i;
                }
                else
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
        Debug.Log($"[BoardView] ShowValidDropZones for {cardRowType}. Total slots: {_slotUIs.Count}");
        foreach (var kv in _slotUIs)
        {
            var parts = kv.Key.Split('_');
            if (parts.Length != 3)
            {
                Debug.LogWarning($"Bad key: {kv.Key}");
                continue;
            }
            bool isPlayer = parts[0] == "P";
            if (!isPlayer)
            {
                kv.Value.IsValidDropTarget = false;
                kv.Value.SetHighlight(false);
                continue;
            }

            if (!Enum.TryParse(parts[1], out Definitions.RowType slotRow))
            {
                Debug.LogWarning($"Unrecognised RowType in key: {kv.Key}");
                continue;
            }

            if (slotRow != cardRowType)
            {
                kv.Value.IsValidDropTarget = false;
                kv.Value.SetHighlight(false);
                continue;
            }

            int index = int.Parse(parts[2]);
            bool empty = IsSlotEmpty(_duelManager.CurrentDuelState.PlayerSide.Board, slotRow, index);
            kv.Value.IsValidDropTarget = empty;
            kv.Value.SetHighlight(empty);
            if (empty) Debug.Log($"[BoardView] Highlighted {kv.Key}");
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

    public void SetCardHighlight(BoardCard card, Color color)
    {
        foreach (var ui in _slotUIs.Values)
        {
            if (ui.Occupant == card)
            {
                ui.HighlightImage.color = color;
                return;
            }
        }
    }

    private bool IsSlotEmpty(Board board, Definitions.RowType row, int index)
    {
        bool empty = false;
        switch (row)
        {
            case Definitions.RowType.Vanguard:
                bool inBounds = index < board.VanguardRow.Length;
                empty = inBounds && board.VanguardRow[index].IsEmpty;
                Debug.Log($"[IsSlotEmpty] Vanguard[{index}] - inBounds={inBounds}, occupant={board.VanguardRow[index]?.Occupant?.SourceCard?.CardName ?? "none"}, IsEmpty={empty}");
                return empty;
            case Definitions.RowType.Building:
                bool bInBounds = index < board.BuildingRow.Length;
                empty = bInBounds && board.BuildingRow[index].IsEmpty;
                Debug.Log($"[IsSlotEmpty] Building[{index}] - inBounds={bInBounds}, IsEmpty={empty}");
                return empty;
            case Definitions.RowType.Human:
                bool hInBounds = index < board.HumanRow.Length;
                empty = hInBounds && board.HumanRow[index].IsEmpty;
                Debug.Log($"[IsSlotEmpty] Human[{index}] - inBounds={hInBounds}, IsEmpty={empty}");
                return empty;
            case Definitions.RowType.Town:
                empty = board.TownSlot.IsEmpty;
                Debug.Log($"[IsSlotEmpty] Town - occupant={board.TownSlot?.Occupant?.SourceCard?.CardName ?? "none"}, IsEmpty={empty}");
                return empty;
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

        var nameText = ui.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
        var statsText = ui.transform.Find("CardStats")?.GetComponent<TextMeshProUGUI>();

        if (card == null || !card.IsAlive)
        {
            if (nameText != null) nameText.text = _emptySlotName;
            if (statsText != null) statsText.text = _emptySlotStats;
        }
        else
        {
            if (nameText != null) nameText.text = card.SourceCard.CardName;
            if (statsText != null) statsText.text = $"{card.Health}/{card.MaxHealth} ATK{card.Attack}";
        }
    }
}