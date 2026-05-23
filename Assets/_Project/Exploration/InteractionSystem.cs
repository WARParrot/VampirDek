using System;

namespace Exploration
{
    public static class InteractionSystem
    {
        public static event Action<EncounterPoint> OnEncounterTriggered;
        public static void TriggerEncounter(EncounterPoint point) => OnEncounterTriggered?.Invoke(point);
    }
}