using System.Collections.Generic;

namespace Core
{
    public class SceneRegistry
    {
        public List<WorldSceneInfo> WorldScenes { get; } = new();
        public List<DuelSceneInfo> DuelScenes { get; } = new();
    }

    public class WorldSceneInfo
    {
        public string SceneId;
        public string AddressableKey;
        public string DisplayName;
        public List<string> RequiredFlags = new();
    }

    public class DuelSceneInfo
    {
        public string SceneId;
        public string AddressableKey;
        public string DisplayName;
    }
}