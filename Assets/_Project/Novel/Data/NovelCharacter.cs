using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "Novel/Character")]
public class NovelCharacter : ScriptableObject
{
    public string Name;
    public AssetReferenceSprite Portrait;
}
