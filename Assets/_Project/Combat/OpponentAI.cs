using System.Collections.Generic;
using System.Linq;
using Definitions;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Улучшенная система искусственного интеллекта для противника
    /// Поддерживает различные стратегии и уровни сложности
    /// </summary>
    public class OpponentAI
    {
        private readonly System.Random _rng;
        private AIStrategy _strategy;
        private float _skillLevel; // 0.0 - 1.0

        public OpponentAI(AIStrategy strategy = AIStrategy.Balanced, float skillLevel = 0.7f)
        {
            _rng = new System.Random();
            _strategy = strategy;
            _skillLevel = Mathf.Clamp01(skillLevel);
        }

        /// <summary>
        /// Выбирает карту для игры в фазе строительства
        /// </summary>
        public CardPlayDecision DecideCardToPlay(SideState aiSide, SideState playerSide)
        {
            var playableCards = GetPlayableCards(aiSide);

            if (playableCards.Count == 0)
                return null;

            return _strategy switch
            {
                AIStrategy.Aggressive => ChooseAggressiveCard(playableCards, aiSide, playerSide),
                AIStrategy.Defensive => ChooseDefensiveCard(playableCards, aiSide, playerSide),
                AIStrategy.Balanced => ChooseBalancedCard(playableCards, aiSide, playerSide),
                AIStrategy.Random => ChooseRandomCard(playableCards, aiSide),
                _ => ChooseBalancedCard(playableCards, aiSide, playerSide)
            };
        }

        /// <summary>
        /// Выбирает цель для атаки в фазе планирования
        /// </summary>
        public IGameEntity DecideAttackTarget(BoardCard attacker, SideState playerSide)
        {
            var playerVanguard = playerSide.Board.VanguardRow
                .Where(s => s.Occupant != null && s.Occupant.IsAlive)
                .Select(s => s.Occupant)
                .ToList();

            var playerTown = playerSide.Board.TownSlot?.Occupant;

            // Случайное решение для низкого уровня навыка
            if (_rng.NextDouble() > _skillLevel)
            {
                var allTargets = new List<IGameEntity>();
                if (playerTown != null && playerTown.IsAlive)
                    allTargets.Add(playerTown);
                allTargets.AddRange(playerVanguard);

                return allTargets.Count > 0 ? allTargets[_rng.Next(allTargets.Count)] : null;
            }

            return _strategy switch
            {
                AIStrategy.Aggressive => DecideAggressiveTarget(attacker, playerVanguard, playerTown),
                AIStrategy.Defensive => DecideDefensiveTarget(attacker, playerVanguard, playerTown),
                AIStrategy.Balanced => DecideBalancedTarget(attacker, playerVanguard, playerTown),
                _ => DecideBalancedTarget(attacker, playerVanguard, playerTown)
            };
        }

        private List<Card> GetPlayableCards(SideState side)
        {
            return side.Hand
                .Where(c => c.Def.Costs.All(cost =>
                {
                    var ctx = new CostContext { PlayerSide = side, Amount = cost.GetAmount() };
                    return cost.CanPay(ctx);
                }))
                .Cast<Card>()
                .ToList();
        }

        private CardPlayDecision ChooseAggressiveCard(List<Card> playableCards, SideState aiSide, SideState playerSide)
        {
            // Приоритет: высокая атака в авангарде
            var vanguardCards = playableCards
                .Where(c => c.Def.RowType == Definitions.RowType.Vanguard)
                .OrderByDescending(c => c.Def.Attack)
                .ToList();

            if (vanguardCards.Count > 0)
            {
                var card = vanguardCards[0];
                var slot = aiSide.Board.VanguardRow.FirstOrDefault(s => s.IsEmpty);
                if (slot != null)
                    return new CardPlayDecision { Card = card, TargetSlot = slot };
            }

            return ChooseAnyPlayableCard(playableCards, aiSide);
        }

        private CardPlayDecision ChooseDefensiveCard(List<Card> playableCards, SideState aiSide, SideState playerSide)
        {
            // Приоритет: здания и юниты с высоким здоровьем
            var buildingCards = playableCards
                .Where(c => c.Def.RowType == Definitions.RowType.Building)
                .OrderByDescending(c => c.Def.Health)
                .ToList();

            if (buildingCards.Count > 0)
            {
                var card = buildingCards[0];
                var slot = aiSide.Board.BuildingRow.FirstOrDefault(s => s.IsEmpty);
                if (slot != null)
                    return new CardPlayDecision { Card = card, TargetSlot = slot };
            }

            return ChooseAnyPlayableCard(playableCards, aiSide);
        }

        private CardPlayDecision ChooseBalancedCard(List<Card> playableCards, SideState aiSide, SideState playerSide)
        {
            // Сбалансированная стратегия: оценка по соотношению атака/здоровье
            var scoredCards = playableCards
                .Select(c => new
                {
                    Card = c,
                    Score = EvaluateCardValue(c.Def, aiSide, playerSide)
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            if (scoredCards.Count > 0)
            {
                var best = scoredCards[0].Card;
                var row = aiSide.Board.GetRow(best.Def.RowType);
                var slot = row?.FirstOrDefault(s => s.IsEmpty);
                if (slot != null)
                    return new CardPlayDecision { Card = best, TargetSlot = slot };
            }

            return null;
        }

        private CardPlayDecision ChooseRandomCard(List<Card> playableCards, SideState aiSide)
        {
            if (playableCards.Count == 0) return null;

            var card = playableCards[_rng.Next(playableCards.Count)];
            var row = aiSide.Board.GetRow(card.Def.RowType);
            var slot = row?.FirstOrDefault(s => s.IsEmpty);

            if (slot != null)
                return new CardPlayDecision { Card = card, TargetSlot = slot };

            return null;
        }

        private CardPlayDecision ChooseAnyPlayableCard(List<Card> playableCards, SideState aiSide)
        {
            foreach (var card in playableCards)
            {
                var row = aiSide.Board.GetRow(card.Def.RowType);
                var slot = row?.FirstOrDefault(s => s.IsEmpty);
                if (slot != null)
                    return new CardPlayDecision { Card = card, TargetSlot = slot };
            }
            return null;
        }

        private float EvaluateCardValue(CardDef card, SideState aiSide, SideState playerSide)
        {
            float score = 0f;

            // Базовая оценка по характеристикам
            score += card.Attack * 1.5f;
            score += card.Health * 1.0f;

            // Бонус за тип ряда в зависимости от ситуации
            if (card.RowType == Definitions.RowType.Vanguard)
            {
                // Больше ценим авангард если у противника мало защиты
                var playerVanguardCount = playerSide.Board.VanguardRow.Count(s => s.Occupant != null);
                score += (3 - playerVanguardCount) * 2f;
            }
            else if (card.RowType == Definitions.RowType.Building)
            {
                // Ценим здания если у нас их мало
                var aiBuildingCount = aiSide.Board.BuildingRow.Count(s => s.Occupant != null);
                score += (3 - aiBuildingCount) * 1.5f;
            }

            return score;
        }

        private IGameEntity DecideAggressiveTarget(BoardCard attacker, List<BoardCard> playerVanguard, IGameEntity playerTown)
        {
            // Агрессивная стратегия: атакуем город если путь свободен
            if (playerVanguard.Count == 0 && playerTown != null && playerTown.IsAlive)
                return playerTown;

            // Иначе атакуем самого слабого
            if (playerVanguard.Count > 0)
                return playerVanguard.OrderBy(v => v.Health).First();

            return playerTown;
        }

        private IGameEntity DecideDefensiveTarget(BoardCard attacker, List<BoardCard> playerVanguard, IGameEntity playerTown)
        {
            // Защитная стратегия: атакуем самого сильного противника
            if (playerVanguard.Count > 0)
                return playerVanguard.OrderByDescending(v => v.Attack).First();

            return playerTown;
        }

        private IGameEntity DecideBalancedTarget(BoardCard attacker, List<BoardCard> playerVanguard, IGameEntity playerTown)
        {
            // Сбалансированная стратегия
            if (playerVanguard.Count == 0 && playerTown != null && playerTown.IsAlive)
                return playerTown;

            if (playerVanguard.Count > 0)
            {
                // Атакуем того, кого можем убить за один удар
                var killable = playerVanguard.FirstOrDefault(v => v.Health <= attacker.Attack);
                if (killable != null)
                    return killable;

                // Иначе атакуем самого слабого
                return playerVanguard.OrderBy(v => v.Health).First();
            }

            return playerTown;
        }
    }

    /// <summary>
    /// Стратегия поведения ИИ
    /// </summary>
    public enum AIStrategy
    {
        Aggressive,  // Агрессивная: фокус на атаке
        Defensive,   // Защитная: фокус на защите
        Balanced,    // Сбалансированная
        Random       // Случайная
    }

    /// <summary>
    /// Решение ИИ о том, какую карту сыграть
    /// </summary>
    public class CardPlayDecision
    {
        public Card Card;
        public BoardSlot TargetSlot;
    }
}
