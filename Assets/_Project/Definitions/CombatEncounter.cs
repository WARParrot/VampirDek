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
        public List<HintData> Hints;
    }

    [System.Serializable]
    public class WeatherEntry
    {
        public EnchantmentData Weather;
    }
}