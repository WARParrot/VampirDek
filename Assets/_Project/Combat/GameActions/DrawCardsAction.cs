using UnityEngine;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class DrawCardsAction(IPlayerSide side, int count) : IGameAction
    {
        private readonly IPlayerSide _side = side;
        private readonly int _count = count;
        public string Description => $"Draw {_count} card(s)";

        public async UniTask ExecuteAsync() => _side.DrawCards(_count);
    }
}
