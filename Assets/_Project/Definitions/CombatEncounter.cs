using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Encounter")]
    public class CombatEncounter : ScriptableObject
    {
        public string EncounterId;
        public AssetReference DuelScene;
        public BoardLayoutData PlayerBoardLayout;
        public BoardLayoutData OpponentBoardLayout;
        public PhaseGraph PhaseGraph;
        public WinCondition WinCondition;
        public List<EnchantmentData> PermanentGlobalEnchantments;
        public WeatherEntry StartingWeather;
        public DeckData OpponentDeck;
        public List<CardDef> RewardCardPool;
        public List<HintData> Hints;
        public string WinFlag;
        public string LoseFlag;
    }

    [System.Serializable]
    public class WeatherEntry
    {
        public EnchantmentData Weather;
    }
}