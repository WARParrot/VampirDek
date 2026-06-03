namespace Definitions
{
    public class SacrificeCostContext : ICostContext
    {
        public IPlayerSide PlayerSide { get; set; }
        public int Amount { get; set; }
        public SacrificeCost Cost { get; set; }

        public RowType RequiredRowType => Cost != null ? Cost.RequiredRowType : RowType.Human;
        public int RequiredAmount => Cost != null ? Cost.Amount : Amount;
    }
}
