using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Scene Info/World Scene Info")]
    public class WorldSceneInfo : ScriptableObject
    {
        public string SceneId;
        public string AddressableKey;
        public string DisplayName;
        public List<string> RequiredFlags = new();
    }
}