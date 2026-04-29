namespace Definitions
{
    public interface IDuelState
    {
        IGameEntity PlayerTown { get; }
        IGameEntity OpponentTown { get; }
        int TurnNumber { get; }
    }
}
