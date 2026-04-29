using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class DrawCardsAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly int _count;
        public string Description => $"Draw {_count} card(s)";
        public DrawCardsAction(IPlayerSide side, int count) { _side = side; _count = count; }
        public async UniTask ExecuteAsync() => _side.DrawCards(_count);
    }
}
