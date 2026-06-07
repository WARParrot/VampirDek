using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Novel/Dialogue Node")]
public class DialogueNode : ScriptableObject
{
    [TextArea(3, 10)]
    public string Text;
    public string TextKey;
    public string SpeakerName;
    public string SpeakerNameKey;
    public string SpeakerPortraitName;
    public string BackgroundName;
    public ChoiceOption[] Choices;
    public DialogueNode NextNode;
}