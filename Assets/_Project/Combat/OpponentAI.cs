using System.Collections.Generic;
using System.Linq;
using Definitions;

namespace Combat
{
    public enum AIStrategy
    {
        Balanced,
        Aggressive,
        Defensive
    }

    public class CardPlayDecision
    {
        public Card Card { get; }
        public BoardSlot TargetSlot { get; }

        public CardPlayDecision(Card card, BoardSlot targetSlot)
        {
            Card = card;
            TargetSlot = targetSlot;
        }
    }

    public class OpponentAI
    {
        private readonly System.Random _rng;
        private readonly AIStrategy _strategy;
        private readonly float _skillLevel;

        public OpponentAI(AIStrategy strategy = AIStrategy.Balanced, float skillLevel = 0.7f)
        {
            _rng = new System.Random();
            _strategy = strategy;
            _skillLevel = UnityEngine.Mathf.Clamp01(skillLevel);
        }

        public CardPlayDecision DecideCardToPlay(SideState aiSide, SideState playerSide)
        {
            if (aiSide == null || aiSide.Hand == null || aiSide.Board == null) return null;

            var playableCards = GetPlayableCards(aiSide)
                .OrderByDescending(ScoreCard)
                .ToList();

            foreach (var card in playableCards)
            {
                var slot = ChooseSlot(aiSide.Board, card.Def.RowType);
                if (slot != null)
                    return new CardPlayDecision(card, slot);
            }

            return null;
        }

        public IGameEntity DecideAttackTarget(BoardCard attacker, SideState playerSide)
        {
            if (attacker == null || playerSide == null || playerSide.Board == null) return null;

            var provoker = CardBehaviorTags.GetActiveProvokerOn(playerSide);
            if (provoker != null && DuelManager.CanAttackerTarget(attacker, provoker))
                return provoker;

            var liveTargets = playerSide.Board.AllCards()
                .Where(c => c != null && c.IsAlive && DuelManager.CanAttackerTarget(attacker, c))
                .ToList();

            var townTargetable = playerSide.Town != null && DuelManager.CanAttackerTarget(attacker, playerSide.Town);

            if (liveTargets.Count == 0)
                return townTargetable ? playerSide.Town : null;

            IGameEntity townOrNull = townTargetable ? (IGameEntity)playerSide.Town : null;

            if (_strategy == AIStrategy.Aggressive)
                return liveTargets.OrderBy(c => c.Health).FirstOrDefault() ?? townOrNull;

            if (_strategy == AIStrategy.Defensive)
                return liveTargets.OrderByDescending(c => c.Attack).FirstOrDefault() ?? townOrNull;

            // Balanced: prefer a vulnerable unit, occasionally pressure the town so the duel can end.
            if (_rng.NextDouble() > _skillLevel && townOrNull != null)
                return townOrNull;

            return liveTargets
                .OrderBy(c => c.Health)
                .ThenByDescending(c => c.Attack)
                .FirstOrDefault() ?? townOrNull;
        }

        private IEnumerable<Card> GetPlayableCards(SideState side)
        {
            foreach (var card in side.Hand)
            {
                if (card?.Def == null) continue;
                var row = side.Board.GetRow(card.Def.RowType);
                if (row == null || !row.Any(slot => slot != null && slot.IsEmpty)) continue;

                bool canPay = true;
                foreach (var cost in card.Def.Costs)
                {
                    var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
                    if (!cost.CanPay(ctx))
                    {
                        canPay = false;
                        break;
                    }
                }

                if (canPay) yield return card;
            }
        }

        private BoardSlot ChooseSlot(Board board, Definitions.RowType rowType)
        {
            var row = board.GetRow(rowType);
            if (row == null) return null;

            var empty = row.Where(slot => slot != null && slot.IsEmpty).ToList();
            if (empty.Count == 0) return null;

            if (_strategy == AIStrategy.Defensive)
                return empty.Last();
            if (_strategy == AIStrategy.Aggressive)
                return empty.First();

            return empty[_rng.Next(empty.Count)];
        }

        private static int ScoreCard(Card card)
        {
            if (card?.Def == null) return 0;
            // Humans are the AI's fuel for SacrificeCost vampires — keep at least one on the board
            // by giving them high priority when no Human is currently down. Building cards cost HR,
            // so they only get a modest bonus.
            int score = card.Def.Attack + card.Def.Health;
            if (card.Def.RowType == Definitions.RowType.Vanguard) score += 2;
            if (card.Def.RowType == Definitions.RowType.Human) score += 5;
            return score;
        }
    }
}
