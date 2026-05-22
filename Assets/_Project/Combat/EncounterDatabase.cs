using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class EncounterDatabase
    {
        private static readonly Dictionary<string, CombatEncounter> _encounters = new();

        public static void RegisterEncounter(CombatEncounter enc)
        {
            if (enc != null && !string.IsNullOrEmpty(enc.EncounterId))
                _encounters[enc.EncounterId] = enc;
        }

        public static CombatEncounter GetEncounter(string encounterId)
        {
            if (string.IsNullOrEmpty(encounterId)) return null;
            _encounters.TryGetValue(encounterId, out var enc);
            return enc;
        }
    }
}