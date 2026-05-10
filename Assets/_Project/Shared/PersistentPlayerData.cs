using System.Collections.Generic;

[System.Serializable]
public class PersistentPlayerData
{
    public List<string> OwnedCardIds = new();
    public List<string> ActiveDeckCardIds = new();
}