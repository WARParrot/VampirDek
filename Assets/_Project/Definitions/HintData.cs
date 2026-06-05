using UnityEngine;
using Core;

namespace Definitions
{
    [CreateAssetMenu(menuName = "Hint Data")]
    public class HintData : ScriptableObject
    {
        [TextArea(2, 5)]
        public string Message;
        [Tooltip("Stable localization key for this hint message. Leave empty to use hint.<asset name>.message with Message as fallback.")]
        public string MessageKey;
        public string EventTag;
        public HintCondition Condition;
        public bool ShownOncePerGame = true;

        public GameMode AllowedMode = GameMode.Both;
    }
}