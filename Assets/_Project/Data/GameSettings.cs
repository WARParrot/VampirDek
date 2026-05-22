using System.Collections.Generic;
using Definitions;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Settings")]
public class GameSettings : ScriptableObject
{
    public List<HintData> GlobalHints;
}