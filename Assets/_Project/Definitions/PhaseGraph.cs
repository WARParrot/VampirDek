using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Phase Graph")]
    public class PhaseGraph : ScriptableObject
    {
        public List<PhaseNode> Nodes;
        public PhaseNode StartingNode;
    }

    [System.Serializable]
    public class PhaseNode
    {
        public string PhaseId;
        public List<string> Tags;
        public List<PhaseTransition> Transitions;
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
