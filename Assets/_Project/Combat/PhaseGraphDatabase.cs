using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class PhaseGraphDatabase
    {
        private static readonly Dictionary<string, PhaseGraph> _graphs = new();

        public static void RegisterPhaseGraph(PhaseGraph graph)
        {
            if (graph != null && !string.IsNullOrEmpty(graph.name))
                _graphs[graph.name] = graph;
        }

        public static PhaseGraph GetPhaseGraph(string graphName)
        {
            if (string.IsNullOrEmpty(graphName)) return null;
            _graphs.TryGetValue(graphName, out var graph);
            return graph;
        }
    }
}