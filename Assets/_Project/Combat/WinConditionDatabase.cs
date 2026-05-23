using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class WinConditionDatabase
    {
        private static readonly Dictionary<string, WinCondition> _conditions = new();

        public static void RegisterWinCondition(WinCondition condition)
        {
            if (condition != null && !string.IsNullOrEmpty(condition.name))
                _conditions[condition.name] = condition;
        }

        public static WinCondition GetWinCondition(string conditionName)
        {
            if (string.IsNullOrEmpty(conditionName)) return null;
            _conditions.TryGetValue(conditionName, out var cond);
            return cond;
        }
    }
}