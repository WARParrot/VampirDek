using System;
using System.Collections.Generic;

namespace Core
{
    [Serializable]
    public class ModInfo
    {
        public string name;
        public string version;
        public string catalogPath;
        public List<SceneEntry> scenes;

        public List<string> cards;
        public List<string> decks;
        public List<string> encounters;
        public List<string> enchantments;
        public List<string> layouts;
        public List<string> phasegraphs;
        public List<string> winconditions;
        public List<string> hints;
    }

    [Serializable]
    public class SceneEntry
    {
        public string type;
        public string id;
        public string addressKey;
        public string displayName;
    }
}