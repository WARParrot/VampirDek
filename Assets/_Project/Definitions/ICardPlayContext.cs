namespace Definitions
{
    public interface ICardPlayContext
    {
        IPlayerSide PlayerSide { get; }
        IPlayerSide OpponentSide { get; }
        IGameEntity Source { get; }
        IGameEntity Target { get; }
    }
}
