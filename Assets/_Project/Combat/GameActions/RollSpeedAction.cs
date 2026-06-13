using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;

namespace Combat
{
    public class RollSpeedAction : IGameAction
    {
        private Board _board;
        public string Description => "Roll speed for all cards";

        public RollSpeedAction(Board board) => _board = board;

        public UniTask ExecuteAsync()
        {
            foreach (var card in _board.AllCards())
            {
                int min = card.SourceCard.MinSpeed;
                int max = card.SourceCard.MaxSpeed;
                // If the card has a fixed speed (Min == Max), give it a ±1 spread so each roll
                // has variance. Always clamp to >= 1.
                if (min == max)
                {
                    min = Mathf.Max(1, min - 1);
                    max = max + 1;
                }
                else
                {
                    min = Mathf.Max(1, min);
                }
                card.CurrentSpeed = UnityEngine.Random.Range(min, max + 1);
            }
            return UniTask.CompletedTask;
        }
    }
}
