using System.Collections.Generic;
using UnityEngine;

namespace Exploration.Inventory
{
    /// <summary>
    /// Singleton ScriptableObject mapping ItemDef.Id -> ItemDef.
    /// Required so save data (which only stores ids as strings) can be resolved back to live SOs.
    /// Place one instance in a Resources folder (or assign manually to Inventory) and drag every
    /// ItemDef into the Items list.
    /// </summary>
    [CreateAssetMenu(menuName = "VampirDek/Inventory/Item Registry", fileName = "ItemRegistry")]
    public class ItemRegistry : ScriptableObject
    {
        public List<ItemDef> Items = new();

        private Dictionary<string, ItemDef> _map;

        public ItemDef Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureMap();
            return _map.TryGetValue(id, out var def) ? def : null;
        }

        private void EnsureMap()
        {
            if (_map != null && _map.Count == Items.Count) return;
            _map = new Dictionary<string, ItemDef>(Items.Count);
            foreach (var it in Items)
            {
                if (it == null || string.IsNullOrEmpty(it.Id)) continue;
                _map[it.Id] = it;
            }
        }

        public void RebuildIndex() { _map = null; EnsureMap(); }
    }
}
