using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class DiscardRandomCardsAction(IPlayerSide side, int count) : IGameAction
    {
        private IPlayerSide _side = side;
        private int _count = count;
        public string Description => $"Discard {_count} random card(s)";

        public async UniTask ExecuteAsync() => _side.DiscardRandomCards(_count);
    }
}
