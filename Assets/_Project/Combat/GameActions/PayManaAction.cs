using UnityEngine;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class PayManaAction(IPlayerSide side, int amount) : IGameAction
    {
        private readonly IPlayerSide _side = side;
        private readonly int _amount = amount;
        public string Description => $"Pay {_amount} mana";

        public async UniTask ExecuteAsync() => _side.PayMana(_amount);
    }
}
