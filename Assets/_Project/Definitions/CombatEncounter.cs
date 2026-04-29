using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Encounter")]
    public class CombatEncounter : ScriptableObject
    {
        public string EncounterId;
        public BoardLayoutData PlayerBoardLayout;
        public BoardLayoutData OpponentBoardLayout;
        public PhaseGraph PhaseGraph;
        public WinCondition WinCondition;
        public List<EnchantmentData> PermanentGlobalEnchantments;
        public WeatherEntry StartingWeather;
        public List<CardDef> OpponentDeck;
    }

    [System.Serializable]
    public class WeatherEntry
    {
        public EnchantmentData Weather;
    }
}
