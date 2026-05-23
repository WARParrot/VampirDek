namespace Definitions
{
    public interface ICostContext
    {
        IPlayerSide PlayerSide { get; }
        int Amount { get; }
    }
}
