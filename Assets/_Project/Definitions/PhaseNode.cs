using UnityEngine;
using System.Collections.Generic;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Phase Node")]
    public class PhaseNode : ScriptableObject
    {
        public string PhaseId;
        public List<string> Tags = new();
        public List<PhaseTransition> Transitions = new();
    }

    [System.Serializable]
    public class PhaseTransition
    {
        public PhaseNode Target;
        public TransitionCondition Condition;
    }

    [System.Serializable]
    public class TransitionCondition
    {
        public ConditionType Type;
        public float Threshold;
        public string TagTrigger;
    }

    public enum ConditionType { None, HealthPercentage, TurnsElapsed, TagActive }
}
