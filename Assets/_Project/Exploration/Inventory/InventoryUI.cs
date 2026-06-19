using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Shared.Localization;
using Core;

namespace Exploration.Inventory
{
    /// <summary>
    /// RE2-style inventory panel.
    /// - Opens on Tab; pauses world via GlobalServices.IsMenuOpen + Time.timeScale = 0.
    /// - Left: main pocket grid (capacity-limited).
    /// - Right: key items list (uncapped).
    /// - Bottom: description / examine text.
    /// - Action row: Use, Combine, Examine, Discard.
    ///
    /// Combine flow: click [Combine] → select another slot → recipe resolves or aborts.
    /// Use-on-world flow: click [Use] on a slot whose item targets the world →
    ///   inventory closes, <see cref="PendingUseItem"/> is set, next ItemUseTarget interaction
    ///   uses it. (Currently ItemUseTarget auto-uses if item is in inventory, so this is mostly
    ///   for items that need explicit aiming.)
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Slot grid")]
        [SerializeField] private InventorySlotUI _slotPrefab;
        [SerializeField] private RectTransform _mainGrid;
        [SerializeField] private RectTransform _keyItemGrid;

        [Header("Detail panel")]
        [SerializeField] private Text _nameLabel;
        [SerializeField] private Text _descriptionLabel;
        [SerializeField] private Image _previewIcon;

        [Header("Actions")]
        [SerializeField] private Button _useButton;
        [SerializeField] private Button _combineButton;
        [SerializeField] private Button _examineButton;
        [SerializeField] private Button _discardButton;

        [Header("Examine modal")]
        [SerializeField] private GameObject _examineRoot;
        [SerializeField] private Text _examineText;
        [SerializeField] private Image _examineIcon;
        [SerializeField] private Button _examineCloseButton;
        [Tooltip("Optional 3D viewer. When set and the item has an ExaminePrefab, it shows a rotatable model instead of the 2D icon.")]
        [SerializeField] private ExamineView3D _examineView3D;

        [Header("Hint label")]
        [SerializeField] private Text _hintLabel;
        [SerializeField] private Text _slotsCounterLabel;

        [Header("Player binding")]
        [SerializeField] private ExplorationController _player;

        [Header("Root")]
        [Tooltip("Child object that holds all panel visuals. Toggled on/off; this script stays active to listen for Tab.")]
        [SerializeField] private GameObject _panelRoot;

        private readonly List<InventorySlotUI> _mainSlots = new();
        private readonly List<InventorySlotUI> _keySlots = new();
        private InventorySlotUI _selected;
        private bool _combineMode;
        private InventorySlotUI _combineSource;
        private bool _isOpen;

        public static ItemDef PendingUseItem { get; private set; }

        private void Awake()
        {
            if (_useButton != null) _useButton.onClick.AddListener(OnUseClicked);
            if (_combineButton != null) _combineButton.onClick.AddListener(OnCombineClicked);
            if (_examineButton != null) _examineButton.onClick.AddListener(OnExamineClicked);
            if (_discardButton != null) _discardButton.onClick.AddListener(OnDiscardClicked);
            if (_examineCloseButton != null) _examineCloseButton.onClick.AddListener(CloseExamine);
            if (_examineRoot != null) _examineRoot.SetActive(false);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (Inventory.Current != null) Inventory.Current.OnChanged += Refresh;
        }

        private void OnDisable()
        {
            if (Inventory.Current != null) Inventory.Current.OnChanged -= Refresh;
        }

        private void Update()
        {
            // Tab to toggle. Esc closes (and examine modal if open).
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame)
            {
                if (_isOpen) Close();
                else Open();
                return;
            }

            if (_isOpen && kb.escapeKey.wasPressedThisFrame)
            {
                if (_examineRoot != null && _examineRoot.activeSelf) CloseExamine();
                else Close();
            }
        }

        public void Open()
        {
            if (Inventory.Current == null) { Debug.LogWarning("[InventoryUI] No Inventory."); return; }
            _isOpen = true;
            if (_panelRoot != null) _panelRoot.SetActive(true);

            GlobalServices.IsMenuOpen = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _player?.Deactivate();
            _combineMode = false;
            _combineSource = null;
            _selected = null;
            Refresh();
        }

        public void Close()
        {
            _isOpen = false;
            if (_examineRoot != null) _examineRoot.SetActive(false);
            if (_panelRoot != null) _panelRoot.SetActive(false);

            Time.timeScale = 1f;
            GlobalServices.IsMenuOpen = false;

            _player?.Activate();
        }

        private void Refresh()
        {
            if (Inventory.Current == null) return;
            BuildMainGrid();
            BuildKeyGrid();
            RefreshDetail();
            RefreshHint();
        }

        private void BuildMainGrid()
        {
            if (_mainGrid == null || _slotPrefab == null) return;

            var inv = Inventory.Current;
            // Make the visual grid match capacity: one cell per slot-unit. Items with SlotSize > 1
            // are rendered as a single cell (visual size handled by GridLayout, item just claims
            // multiple capacity units in the data).
            // We render: one cell per occupied entry + empty cells filling remaining capacity.

            int visualCells = inv.MainSlots.Count + inv.FreeMainSlots;
            EnsureCells(_mainGrid, _mainSlots, visualCells);

            int i = 0;
            for (; i < inv.MainSlots.Count; i++)
                _mainSlots[i].Bind(inv.MainSlots[i], false, OnSlotClicked);
            for (; i < _mainSlots.Count; i++)
                _mainSlots[i].BindEmpty(OnSlotClicked);
        }

        private void BuildKeyGrid()
        {
            if (_keyItemGrid == null || _slotPrefab == null) return;
            var inv = Inventory.Current;
            EnsureCells(_keyItemGrid, _keySlots, inv.KeyItems.Count);
            for (int i = 0; i < inv.KeyItems.Count; i++)
                _keySlots[i].Bind(inv.KeyItems[i], true, OnSlotClicked);
        }

        private void EnsureCells(RectTransform parent, List<InventorySlotUI> list, int count)
        {
            while (list.Count < count)
            {
                var ui = Instantiate(_slotPrefab, parent);
                list.Add(ui);
            }
            for (int i = 0; i < list.Count; i++)
                list[i].gameObject.SetActive(i < count);
        }

        private void OnSlotClicked(InventorySlotUI ui)
        {
            if (_combineMode)
            {
                if (ui == _combineSource || ui.IsEmpty || ui.IsKeyItem || _combineSource == null || _combineSource.IsEmpty)
                {
                    // Cancel
                    _combineMode = false;
                    _combineSource?.SetSelected(false);
                    _combineSource = null;
                    RefreshHint();
                    return;
                }
                TryCombine(_combineSource.Slot.Item, ui.Slot.Item);
                _combineMode = false;
                _combineSource = null;
                return;
            }

            if (_selected != null) _selected.SetSelected(false);
            _selected = ui;
            if (_selected != null) _selected.SetSelected(true);
            RefreshDetail();
        }

        private void TryCombine(ItemDef a, ItemDef b)
        {
            if (Inventory.Current.TryCombine(a, b))
            {
                _hintLabel?.GetComponent<Text>(); // no-op accessor for safety
                if (_hintLabel != null) _hintLabel.text = LocalizationService.T("inventory.combine_ok", "Combined.");
            }
            else
            {
                if (_hintLabel != null) _hintLabel.text = LocalizationService.T("inventory.combine_fail", "These don't combine.");
            }
        }

        private void RefreshDetail()
        {
            bool has = _selected != null && !_selected.IsEmpty;
            var item = has ? _selected.Slot.Item : null;

            if (_nameLabel != null) _nameLabel.text = has ? LocalizationService.T(item.DisplayNameKey, item.DisplayNameFallback) : "";
            if (_descriptionLabel != null) _descriptionLabel.text = has ? LocalizationService.T(item.DescriptionKey, item.DescriptionFallback) : "";
            if (_previewIcon != null)
            {
                _previewIcon.enabled = has;
                if (has) _previewIcon.sprite = item.Icon;
            }

            if (_useButton != null) _useButton.interactable = has;
            if (_examineButton != null) _examineButton.interactable = has;
            if (_combineButton != null) _combineButton.interactable = has && !_selected.IsKeyItem;
            if (_discardButton != null) _discardButton.interactable = has && !_selected.IsKeyItem;
        }

        private void RefreshHint()
        {
            if (_hintLabel != null)
            {
                if (_combineMode)
                    _hintLabel.text = LocalizationService.T("inventory.combine_pick", "Выбери второй предмет для соединения.");
                else
                    _hintLabel.text = LocalizationService.T("inventory.hint", "TAB / ESC — закрыть   •   ЛКМ по предмету — выбрать");
            }
            if (_slotsCounterLabel != null && Inventory.Current != null)
            {
                _slotsCounterLabel.text = $"СЛОТЫ  {Inventory.Current.UsedMainSlots} / {Inventory.Current.MainPocketCapacity}";
            }
        }

        private void OnUseClicked()
        {
            if (_selected == null || _selected.IsEmpty) return;
            var item = _selected.Slot.Item;

            // Self-consumable: drink the antidote here, no world target needed.
            if (item.Id == EscapeQuestState.PotionItemId)
            {
                EscapeQuestState.MarkPotionConsumed();
                Inventory.Current.Remove(item);
                if (_hintLabel != null) _hintLabel.text = "Вы выпили зелье.";
                _selected = null;
                RefreshDetail();
                return;
            }

            PendingUseItem = item;
            Close();
        }

        private void OnCombineClicked()
        {
            if (_selected == null || _selected.IsEmpty || _selected.IsKeyItem) return;
            _combineMode = true;
            _combineSource = _selected;
            RefreshHint();
        }

        private void OnExamineClicked()
        {
            if (_selected == null || _selected.IsEmpty) return;
            if (_examineRoot == null) return;
            _examineRoot.SetActive(true);
            var item = _selected.Slot.Item;
            string caption = LocalizationService.T(item.ExamineTextKey, item.ExamineTextFallback);
            if (_examineText != null) _examineText.text = caption;

            // Potion uses a procedural 3D viewer spawned by InventoryExamine3D.cs.
            // Note has no icon at all — examine modal shows only the text block.
            bool wants3D = item.Id == EscapeQuestState.PotionItemId
                           || (_examineView3D != null && item.ExaminePrefab != null);

            if (_examineIcon != null)
            {
                bool showIcon = !wants3D && item.Icon != null;
                _examineIcon.enabled = showIcon;
                if (showIcon) _examineIcon.sprite = item.Icon;
            }

            if (wants3D)
            {
                if (item.Id == EscapeQuestState.PotionItemId)
                {
                    // Attach inside the inner frame (next to the icon slot) so the close
                    // button stays on top and clickable.
                    var frame = _examineRoot.transform.Find("ExamineFrame");
                    Potion3DExamineView.Show(frame != null ? frame : _examineRoot.transform, caption);
                }
                else
                    _examineView3D.Show(item, caption);
            }
            else
            {
                if (_examineView3D != null) _examineView3D.Hide();
                Potion3DExamineView.Hide();
            }
        }

        private void CloseExamine()
        {
            if (_examineView3D != null) _examineView3D.Hide();
            Potion3DExamineView.Hide();
            if (_examineRoot != null) _examineRoot.SetActive(false);
        }

        private void OnDiscardClicked()
        {
            if (_selected == null || _selected.IsEmpty || _selected.IsKeyItem) return;
            Inventory.Current.Discard(_selected.Slot);
            _selected = null;
            RefreshDetail();
        }

        public static void ClearPendingUseItem() => PendingUseItem = null;
    }
}
