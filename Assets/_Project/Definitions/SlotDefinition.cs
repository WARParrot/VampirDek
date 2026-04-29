using System;

public enum RowType { Vanguard, Building, Human, Town }

[Serializable]
public class SlotDefinition
{
    public RowType Row;
    public int Index;
}
