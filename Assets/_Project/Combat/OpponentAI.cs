using System.Collections.Generic;
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

            Card bestCard = null;
            int bestScore = int.MinValue;
            foreach (var card in aiSide.Hand)
            {
                if (!CanPlayCard(aiSide, card)) continue;

                int score = ScoreCard(card);
                if (bestCard == null || score > bestScore)
                {
                    bestCard = card;
                    bestScore = score;
                }
            }

            if (bestCard == null) return null;

            var slot = ChooseSlot(aiSide.Board, bestCard.Def.RowType);
            return slot != null ? new CardPlayDecision(bestCard, slot) : null;
        }

        public IGameEntity DecideAttackTarget(BoardCard attacker, SideState playerSide)
        {
            if (attacker == null || playerSide == null || playerSide.Board == null) return null;

            var provoker = CardBehaviorTags.GetActiveProvokerOn(playerSide);
            if (provoker != null && DuelManager.CanAttackerTarget(attacker, provoker, playerSide))
                return provoker;

            BoardCard bestTarget = null;
            foreach (var candidate in playerSide.Board.AllCards())
            {
                if (candidate == null || !candidate.IsAlive) continue;
                if (!DuelManager.CanAttackerTarget(attacker, candidate, playerSide)) continue;

                if (bestTarget == null || IsBetterTarget(candidate, bestTarget))
                {
                    bestTarget = candidate;
                }
            }

            var townTargetable = playerSide.Town != null && DuelManager.CanAttackerTarget(attacker, playerSide.Town, playerSide);
            IGameEntity townOrNull = townTargetable ? playerSide.Town : null;

            if (bestTarget == null)
                return townOrNull;

            // Balanced: prefer a vulnerable unit, occasionally pressure the town so the duel can end.
            if (_strategy == AIStrategy.Balanced && _rng.NextDouble() > _skillLevel && townOrNull != null)
                return townOrNull;

            return bestTarget ?? townOrNull;
        }

        private bool IsBetterTarget(BoardCard candidate, BoardCard currentBest)
        {
            if (_strategy == AIStrategy.Aggressive)
                return candidate.Health < currentBest.Health;

            if (_strategy == AIStrategy.Defensive)
                return candidate.Attack > currentBest.Attack;

            if (candidate.Health != currentBest.Health)
                return candidate.Health < currentBest.Health;

            return candidate.Attack > currentBest.Attack;
        }

        private static bool CanPlayCard(SideState side, Card card)
        {
            if (card?.Def == null) return false;
            var row = side.Board.GetRow(card.Def.RowType);
            if (!HasEmptySlot(row)) return false;

            foreach (var cost in card.Def.Costs)
            {
                var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
                if (!cost.CanPay(ctx)) return false;
            }

            return true;
        }

        private static bool HasEmptySlot(BoardSlot[] row)
        {
            if (row == null) return false;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] != null && row[i].IsEmpty) return true;
            }
            return false;
        }

        private BoardSlot ChooseSlot(Board board, Definitions.RowType rowType)
        {
            var row = board.GetRow(rowType);
            if (row == null) return null;

            if (_strategy == AIStrategy.Defensive)
            {
                for (int i = row.Length - 1; i >= 0; i--)
                {
                    if (row[i] != null && row[i].IsEmpty) return row[i];
                }
                return null;
            }

            if (_strategy == AIStrategy.Aggressive)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (row[i] != null && row[i].IsEmpty) return row[i];
                }
                return null;
            }

            int emptyCount = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] != null && row[i].IsEmpty) emptyCount++;
            }
            if (emptyCount == 0) return null;

            int selected = _rng.Next(emptyCount);
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == null || !row[i].IsEmpty) continue;
                if (selected-- == 0) return row[i];
            }

            return null;
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
