using UnityEngine;
using System.Collections.Generic;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Phase Graph")]
    public class PhaseGraph : ScriptableObject
    {
        public List<PhaseNode> Nodes = new();
        public PhaseNode StartingNode;
    }
}
