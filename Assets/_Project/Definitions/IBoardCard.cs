namespace Definitions
{
    public interface IBoardCard
    {
        int Id { get; }
        int Health { get; }
        int MaxHealth { get; }
        int Attack { get; }
        bool IsAlive { get; }
        RowType RowType { get; }
        CardDef SourceCard { get; }
    }
}
