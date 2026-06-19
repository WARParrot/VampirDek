using System;
using System.Collections.Generic;
using UnityEngine;
using Core;

namespace Exploration.Inventory
{
    [Serializable]
    public class InventorySlot
    {
        public ItemDef Item;
        public int Count;

        public bool IsEmpty => Item == null || Count <= 0;
        public int OccupiedSize => IsEmpty ? 0 : Item.SlotSize;
    }

    /// <summary>
    /// Runtime player inventory. RE2-style: limited slot pocket for regular items + an
    /// uncapped key-item pocket. Survives scene loads via DontDestroyOnLoad.
    /// Listeners hook OnChanged to refresh UI.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public static Inventory Current { get; private set; }

        [Header("Capacity")]
        [Tooltip("Total slot units in the main pocket. Big items take more than one.")]
        [SerializeField, Min(1)] private int _mainPocketSlots = 8;

        [Header("Recipes")]
        [SerializeField] private List<CombineRecipe> _recipes = new();

        [Header("Save/Load")]
        [Tooltip("Required to resolve ItemDef from id on load. Assign or auto-load from Resources/ItemRegistry.")]
        [SerializeField] private ItemRegistry _registry;

        private readonly List<InventorySlot> _main = new();
        private readonly List<InventorySlot> _keyItems = new();

        public event Action OnChanged;

        public int MainPocketCapacity => _mainPocketSlots;
        public IReadOnlyList<InventorySlot> MainSlots => _main;
        public IReadOnlyList<InventorySlot> KeyItems => _keyItems;

        public int UsedMainSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _main.Count; i++) n += _main[i].OccupiedSize;
                return n;
            }
        }

        public int FreeMainSlots => Mathf.Max(0, _mainPocketSlots - UsedMainSlots);

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            DontDestroyOnLoad(gameObject);

            if (_registry == null) _registry = Resources.Load<ItemRegistry>("ItemRegistry");
        }

        public ItemRegistry Registry => _registry;

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public bool CanAdd(ItemDef item, int count = 1)
        {
            if (item == null || count <= 0) return false;
            if (item.IsKeyItem) return true;

            if (item.IsStackable)
            {
                int remaining = count;
                foreach (var s in _main)
                {
                    if (s.Item == item && s.Count < item.MaxStack)
                        remaining -= (item.MaxStack - s.Count);
                    if (remaining <= 0) return true;
                }
                int newStacks = Mathf.CeilToInt(remaining / (float)item.MaxStack);
                return newStacks * item.SlotSize <= FreeMainSlots;
            }

            return count * item.SlotSize <= FreeMainSlots;
        }

        public bool Add(ItemDef item, int count = 1)
        {
            if (!CanAdd(item, count)) return false;

            if (item.IsKeyItem)
            {
                var existing = _keyItems.Find(s => s.Item == item);
                if (existing != null) existing.Count += count;
                else _keyItems.Add(new InventorySlot { Item = item, Count = count });
                OnChanged?.Invoke();
                return true;
            }

            int remaining = count;

            if (item.IsStackable)
            {
                foreach (var s in _main)
                {
                    if (remaining <= 0) break;
                    if (s.Item == item && s.Count < item.MaxStack)
                    {
                        int room = item.MaxStack - s.Count;
                        int take = Mathf.Min(room, remaining);
                        s.Count += take;
                        remaining -= take;
                    }
                }
            }

            while (remaining > 0)
            {
                int take = item.IsStackable ? Mathf.Min(item.MaxStack, remaining) : 1;
                _main.Add(new InventorySlot { Item = item, Count = take });
                remaining -= take;
            }

            OnChanged?.Invoke();
            return true;
        }

        public bool Has(ItemDef item, int count = 1)
        {
            if (item == null) return false;
            int found = 0;
            foreach (var s in _keyItems) if (s.Item == item) found += s.Count;
            foreach (var s in _main) if (s.Item == item) found += s.Count;
            return found >= count;
        }

        public bool Remove(ItemDef item, int count = 1)
        {
            if (!Has(item, count)) return false;
            int remaining = count;

            for (int i = _main.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_main[i].Item != item) continue;
                int take = Mathf.Min(_main[i].Count, remaining);
                _main[i].Count -= take;
                remaining -= take;
                if (_main[i].Count <= 0) _main.RemoveAt(i);
            }
            for (int i = _keyItems.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (_keyItems[i].Item != item) continue;
                int take = Mathf.Min(_keyItems[i].Count, remaining);
                _keyItems[i].Count -= take;
                remaining -= take;
                if (_keyItems[i].Count <= 0) _keyItems.RemoveAt(i);
            }

            OnChanged?.Invoke();
            return true;
        }

        public bool Discard(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty) return false;
            if (slot.Item.IsKeyItem) return false;
            slot.Count = 0;
            slot.Item = null;
            _main.RemoveAll(s => s.IsEmpty);
            OnChanged?.Invoke();
            return true;
        }

        public CombineRecipe FindRecipe(ItemDef a, ItemDef b)
        {
            foreach (var r in _recipes)
                if (r != null && r.Matches(a, b)) return r;
            return null;
        }

        public bool TryCombine(ItemDef a, ItemDef b)
        {
            var recipe = FindRecipe(a, b);
            if (recipe == null) return false;
            if (!Has(a) || !Has(b)) return false;

            // Reserve space: removing the inputs frees their slots first, so the output should fit.
            if (recipe.ConsumeBoth) { Remove(a); Remove(b); }
            else { Remove(b); }

            if (!Add(recipe.Output))
            {
                // Edge case: somehow output won't fit. Refund.
                if (recipe.ConsumeBoth) { Add(a); Add(b); } else { Add(b); }
                return false;
            }
            return true;
        }

        // --- Serialization helpers (used by save system) ----------------------

        public List<PersistentGameState.InventoryEntry> Serialize()
        {
            var list = new List<PersistentGameState.InventoryEntry>(_main.Count + _keyItems.Count);
            foreach (var s in _main)
                if (!s.IsEmpty) list.Add(new PersistentGameState.InventoryEntry { ItemId = s.Item.Id, Count = s.Count, IsKey = false });
            foreach (var s in _keyItems)
                if (!s.IsEmpty) list.Add(new PersistentGameState.InventoryEntry { ItemId = s.Item.Id, Count = s.Count, IsKey = true });
            return list;
        }

        public void LoadFrom(List<PersistentGameState.InventoryEntry> entries)
        {
            _main.Clear();
            _keyItems.Clear();
            if (entries == null) { OnChanged?.Invoke(); return; }
            foreach (var e in entries)
            {
                var def = _registry != null ? _registry.Resolve(e.ItemId) : null;
                if (def == null) continue;
                if (e.IsKey || def.IsKeyItem)
                    _keyItems.Add(new InventorySlot { Item = def, Count = e.Count });
                else
                    _main.Add(new InventorySlot { Item = def, Count = e.Count });
            }
            OnChanged?.Invoke();
        }
    }
}
