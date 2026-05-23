using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class PayManaAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly int _amount;
        public string Description => $"Pay {_amount} mana";
        public PayManaAction(IPlayerSide side, int amount) { _side = side; _amount = amount; }
        public async UniTask ExecuteAsync() => _side.PayMana(_amount);
    }
}
