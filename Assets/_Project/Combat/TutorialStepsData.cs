using System.Collections.Generic;
using UnityEngine;
namespace Combat
{
    /// <summary>
    /// ScriptableObject для хранения предустановленных шагов туториала
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialStepsData", menuName = "Tutorial/Tutorial Steps Data")]
    public class TutorialStepsData : ScriptableObject
    {
        [SerializeField] private List<TutorialStep> _steps = new List<TutorialStep>();
        public List<TutorialStep> Steps => _steps;
        /// <summary>
        /// Создаёт стандартный набор шагов для обучающего боя
        /// </summary>
        public static List<TutorialStep> CreateDefaultSteps()
        {
            return new List<TutorialStep>
            {
                new TutorialStep
                {
                    Message = "Первая дуэль. Задача простая: защитить свой город и разрушить город противника.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 7f
                },
                new TutorialStep
                {
                    Message = "HR — это запас людей. Чаще всего он нужен для зданий.\n\nВ начале хода HR становится равен числу живых карт в вашем ряду Human.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 8f, DynamicArrow = DynamicArrowTarget.PlayerHumanResourcesText
                },
                new TutorialStep
                {
                    Message = "Building Phase: выкладываем карты на поле.\n\nСначала сыграйте Human. Human пригодится как жертва для Vampire и увеличит HR на следующем ходу.\n\n{PlayableCardHint}",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardDragged, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableHandCard, PreferredCardName = "Human"
                },
                new TutorialStep
                {
                    Message = "Положите Human в подсвеченный слот ряда Human.\n\n{PlayableCardHint}",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardPlaced, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableBoardSlot, PreferredCardName = "Human"
                },
                new TutorialStep
                {
                    Message = "Теперь сыграйте Vampire. Это боец Vanguard: основного атакующего ряда.\n\nVanguard-карты часто требуют жертвовать Human-картами.\n\n{PlayableCardHint}",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardDragged, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableHandCard, PreferredCardName = "Vampire"
                },
                new TutorialStep
                {
                    Message = "Положите Vampire в подсвеченный слот Vanguard.\n\n{PlayableCardHint}\n\nРяды поля:\n• Vanguard — атакующие карты\n• Building — здания, обычно играются за HR\n• Human — люди для HR, эффектов и жертв",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardPlaced, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableBoardSlot, PreferredCardName = "Vampire"
                },
                new TutorialStep
                {
                    Message = "Отлично: на поле есть Human и атакующий Vampire. Дальше подтвердим строительство и выберем цель атаки.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 5f, RequiredPhaseTag = "BuildingPhase"
                },
                new TutorialStep
                {
                    Message = "Нажмите подтверждение фазы, чтобы перейти к планированию атак.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.PhaseConfirmed, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PhaseConfirmationButton
                },
                new TutorialStep
                {
                    Message = "Planning Phase: здесь назначаются атаки.\n\nНажмите своего живого бойца с ATK > 0 — например Vampire в Vanguard.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.AttackerCardSelected, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.PlayerVanguardCard
                },
                new TutorialStep
                {
                    Message = "Теперь нажмите живую карту противника. Выбранный Vampire будет атаковать её в бою.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.TargetSelected, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.EnemyAnyAliveCard
                },
                new TutorialStep
                {
                    Message = "Цель выбрана. Подтвердите Planning Phase — дальше бой разыграется автоматически.",
                    DimScreen = true, CompletionCondition = TutorialStepCondition.PhaseConfirmed, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.PhaseConfirmationButton
                },
                new TutorialStep
                {
                    Message = "Бой: если две карты атакуют друг друга, происходит столкновение. Урон считается автоматически.",
                    DimScreen = false, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Если атака не встречена ответной атакой, карта просто наносит урон выбранной цели.",
                    DimScreen = false, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Ход почти закончен. На следующем ходу HR обновится по числу живых Human, игроки возьмут карты, а временный урон зданий сбросится.",
                    DimScreen = false, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 7f
                },
                new TutorialStep
                {
                    Message = "Обучение завершено. Теперь управление свободно.\n\nПлан простой: стройте поле, берегите Human, атакуйте из Vanguard и разрушьте город противника.",
                    DimScreen = false, CompletionCondition = TutorialStepCondition.TimeElapsed, TimeToWait = 4f
                }
            };
        }
    }
}
