using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Exploration.Inventory
{
    public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _countLabel;
        [SerializeField] private GameObject _selectionFrame;

        private InventorySlot _slot;
        private bool _isKeyItem;
        private System.Action<InventorySlotUI> _onClick;

        public InventorySlot Slot => _slot;
        public bool IsKeyItem => _isKeyItem;
        public bool IsEmpty => _slot == null || _slot.IsEmpty;

        public void Bind(InventorySlot slot, bool isKeyItem, System.Action<InventorySlotUI> onClick)
        {
            _slot = slot;
            _isKeyItem = isKeyItem;
            _onClick = onClick;
            Refresh();
            SetSelected(false);
        }

        public void BindEmpty(System.Action<InventorySlotUI> onClick)
        {
            _slot = null;
            _isKeyItem = false;
            _onClick = onClick;
            Refresh();
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (_selectionFrame != null) _selectionFrame.SetActive(selected);
        }

        private void Refresh()
        {
            bool empty = IsEmpty;
            if (_icon != null)
            {
                _icon.enabled = !empty;
                if (!empty) _icon.sprite = _slot.Item.Icon;
            }
            if (_countLabel != null)
            {
                bool showCount = !empty && _slot.Count > 1;
                _countLabel.enabled = showCount;
                if (showCount) _countLabel.text = _slot.Count.ToString();
            }
        }

        public void OnPointerClick(PointerEventData eventData) => _onClick?.Invoke(this);
    }
}
