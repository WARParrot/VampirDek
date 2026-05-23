using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class HintDatabase
    {
        private static readonly Dictionary<string, HintData> _hints = new();

        public static void RegisterHint(HintData hint)
        {
            if (hint != null && !string.IsNullOrEmpty(hint.name))
                _hints[hint.name] = hint;
        }

        public static HintData GetHint(string hintName)
        {
            if (string.IsNullOrEmpty(hintName)) return null;
            _hints.TryGetValue(hintName, out var hint);
            return hint;
        }

        public static IEnumerable<HintData> GetAllHints() => _hints.Values;
    }
}