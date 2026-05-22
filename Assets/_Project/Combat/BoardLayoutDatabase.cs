using System.Collections.Generic;
using Definitions;

namespace Combat
{
    public static class BoardLayoutDatabase
    {
        private static readonly Dictionary<string, BoardLayoutData> _layouts = new();

        public static void RegisterLayout(BoardLayoutData layout)
        {
            if (layout != null && !string.IsNullOrEmpty(layout.name))
                _layouts[layout.name] = layout;
        }

        public static BoardLayoutData GetLayout(string layoutName)
        {
            if (string.IsNullOrEmpty(layoutName)) return null;
            _layouts.TryGetValue(layoutName, out var layout);
            return layout;
        }
    }
}