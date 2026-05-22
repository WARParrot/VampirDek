using UnityEngine;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Scene Info/Duel Scene Info")]
    public class DuelSceneInfo : ScriptableObject
    {
        public string SceneId;
        public string AddressableKey;
        public string DisplayName;
    }
}