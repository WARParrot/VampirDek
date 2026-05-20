using System.Collections.Generic;
using UnityEngine;
using System;

namespace Core
{
    [Serializable]
    public class PersistentGameState
    {
        public string CurrentWorldSceneAddress;
        public Vector3 PlayerPosition;
        public Quaternion PlayerRotation;
        public List<string> CompletedEncounterIds = new();
        public List<string> CollectedCardIds = new();
        public Dictionary<string, bool> Flags = new();
    }
}