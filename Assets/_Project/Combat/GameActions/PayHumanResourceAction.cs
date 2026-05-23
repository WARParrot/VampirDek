using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class PayHumanResourceAction : IGameAction
    {
        private IPlayerSide _side;
        private int _amount;
        public string Description => $"Pay {_amount} human resources";

        public PayHumanResourceAction(IPlayerSide side, int amount)
        {
            _side = side;
            _amount = amount;
        }

        public async UniTask ExecuteAsync()
        {
            _side.PayHumanResources(_amount);
        }
    }
}
