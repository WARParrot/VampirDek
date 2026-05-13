using System;
using System.Collections.Generic;

namespace Core
{
    [Serializable]
    public class ModInfo
    {
        public string Name;
        public string Version;
        public string CatalogPath;
        public List<SceneEntry> Scenes;
    }

    [Serializable]
    public class SceneEntry
    {
        public string Type;
        public string Id;
        public string AddressKey;
        public string DisplayName;
    }
}