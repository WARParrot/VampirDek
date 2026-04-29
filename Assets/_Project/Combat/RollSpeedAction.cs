using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat
{
    public class RollSpeedAction : IGameAction
    {
        private Board _board;
        public string Description => "Roll speed for all cards";

        public RollSpeedAction(Board board) => _board = board;

        public async UniTask ExecuteAsync()
        {
            foreach (var card in _board.AllCards())
                card.CurrentSpeed = UnityEngine.Random.Range(card.SourceCard.MinSpeed, card.SourceCard.MaxSpeed + 1);
        }
    }
}
