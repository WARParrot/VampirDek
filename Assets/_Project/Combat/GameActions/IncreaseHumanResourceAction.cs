using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class IncreaseHumanResourceAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly int _amount;

        public string Description => $"Increase Human Resources by {_amount}";

        public IncreaseHumanResourceAction(IPlayerSide side, int amount)
        {
            _side = side;
            _amount = amount;
        }

        public async UniTask ExecuteAsync()
        {
            _side.HumanResources += _amount;
        }
    }
}