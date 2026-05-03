namespace Definitions
{
    public class CostContext : ICostContext
    {
        public IPlayerSide PlayerSide { get; set; }
        public int Amount { get; set; }
    }
}
