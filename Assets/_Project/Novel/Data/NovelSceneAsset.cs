using UnityEngine;

[CreateAssetMenu(menuName = "Novel/Scene Asset")]
public class NovelSceneAsset : ScriptableObject
{
    public DialogueNode StartingNode;
    public DialogueNode[] AllNodes;
}
