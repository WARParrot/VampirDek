using UnityEngine;
using Core;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Hint Data")]
    public class HintData : ScriptableObject
    {
        [TextArea(2, 5)]
        public string Message;
        public string EventTag;
        public HintCondition Condition;
        public bool ShownOncePerGame = true;

        public GameMode AllowedMode = GameMode.Both;
    }
}