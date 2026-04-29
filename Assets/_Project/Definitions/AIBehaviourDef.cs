using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "AI Behavior")]
    public class AIBehaviorDef : ScriptableObject
    {
        public List<AIActionDef> PossibleActions;
        public int InitialAggression = 0;
    }

    [System.Serializable]
    public class AIActionDef
    {
        public string ActionName;
        public int BaseScore;
        public List<AIScorerDef> Scorers;
    }

    [System.Serializable]
    public class AIScorerDef
    {
        public string StatName;
        public float Multiplier;
    }
}
