using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Definitions
{
    [Serializable]
    [CreateAssetMenu(menuName = "Encounter")]
    public class CombatEncounter : ScriptableObject
    {
        public string EncounterId;

        public string PlayerBoardLayoutId;
        public string OpponentBoardLayoutId;
        public string PhaseGraphId;
        public string WinConditionId;
        public string OpponentDeckId;
        public List<string> HintIds = new();
        public List<string> RewardCardPool = new();

        public AssetReference DuelScene;

        public string WinFlag;
        public string LoseFlag;
    }
}