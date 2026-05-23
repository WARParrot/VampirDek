using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class DiscardRandomCardsAction : IGameAction
    {
        private readonly IPlayerSide _side;
        private readonly int _count;
        public string Description => $"Discard {_count} random card(s)";
        public DiscardRandomCardsAction(IPlayerSide side, int count) { _side = side; _count = count; }
        public async UniTask ExecuteAsync() => _side.DiscardRandomCards(_count);
    }
}
