namespace Definitions
{
    public class SacrificeCostContext : ICostContext
    {
        public IPlayerSide PlayerSide { get; set; }
        public int Amount { get; set; }                     // не используется, но интерфейс требует
        public SacrificeCost Cost { get; set; }             // здесь и будет RequiredRowType
    }
}