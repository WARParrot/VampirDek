namespace Definitions
{
    public interface IBoardCard : IGameEntity
    {
        int Id { get; }
        int Health { get; }
        int MaxHealth { get; }
        int Attack { get; }
        bool IsAlive { get; }
        RowType TypeOfRow { get; }
        CardDef SourceCard { get; }
    }
}
