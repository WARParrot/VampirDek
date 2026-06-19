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
        /// <summary>Encounters cleared in the current replay run. Cleared when a new night/run starts.</summary>
        public List<string> CompletedEncounterIds = new();

        /// <summary>All encounters the player has ever cleared across replay runs; never used to block replay.</summary>
        public List<string> LifetimeCompletedEncounterIds = new();

        public List<string> CollectedCardIds = new();
        public Dictionary<string, bool> Flags = new();

        /// <summary>Infinite replay loop switch. Default true so old saves opt into the replayable structure.</summary>
        public bool EndlessReplayEnabled = true;

        /// <summary>One-based run counter. Incremented when the player continues from an ending into the next night.</summary>
        public int ReplayRunNumber = 1;

        /// <summary>Seed for deterministic per-run variations such as reward choices.</summary>
        public int ReplayRunSeed;

        /// <summary>Set after a run ending; the next run starts only when the player interacts with a WorldPortal.</summary>
        public bool AwaitingNextNightPortal;

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