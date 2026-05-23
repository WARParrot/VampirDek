using System.Collections.Generic;
using UnityEngine;

namespace Definitions
{
    public class SceneRegistry
    {
        public List<WorldSceneInfo> WorldScenes { get; } = new();
        public List<DuelSceneInfo> DuelScenes { get; } = new();
    }
}