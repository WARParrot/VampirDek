using System.Collections.Generic;
using UnityEngine;
using System;

namespace Core
{
    [Serializable]
    public class PersistentGameState
    {
        public string CurrentWorldSceneAddress;
        public string ActiveDuelTableId;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public List<string> CompletedEncounterIds = new();
        public List<string> CollectedCardIds = new();
        public Dictionary<string, bool> Flags = new();

        [Serializable]
        public class InventoryEntry
        {
            public string ItemId;
            public int Count;
            public bool IsKey;
        }

        public List<InventoryEntry> Inventory = new();
    }
}