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
                    Message = "Добро пожаловать в бой! Это обучающая дуэль.\n\nВверху экрана вы видите информацию о текущей фазе боя.",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Это ваши ресурсы:\n• HR (Human Resources) - люди для найма юнитов\n• Mana - магическая энергия для заклинаний",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Сейчас ФАЗА СТРОИТЕЛЬСТВА.\n\nВы можете размещать карты на поле боя.\nПопробуйте перетащить карту из руки на поле!",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.CardDragged,
                    TimeToWait = 0f
                },
                new TutorialStep
                {
                    Message = "Отлично! Теперь отпустите карту на подсвеченный слот.\n\nКаждая карта может быть размещена только в определённом ряду:\n• Vanguard (передовая)\n• Building (здания)\n• Human (люди)",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.CardPlaced,
                    TimeToWait = 0f
                },
                new TutorialStep
                {
                    Message = "Превосходно! Карта размещена на поле.\n\nОбратите внимание: при размещении карты были потрачены ресурсы (стоимость карты).",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Когда вы закончите размещать карты, нажмите кнопку подтверждения фазы.\n\nЭто завершит вашу фазу строительства и передаст ход противнику.",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.PhaseConfirmed,
                    TimeToWait = 0f
                },
                new TutorialStep
                {
                    Message = "Теперь ФАЗА ПЛАНИРОВАНИЯ.\n\nВ этой фазе вы выбираете цели для атаки ваших юнитов.\nКликните на вашу карту на поле, затем выберите цель противника.",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.TargetSelected,
                    RequiredPhaseTag = "PlanningPhase"
                },
                new TutorialStep
                {
                    Message = "Отлично! Цель выбрана.\n\nПосле выбора всех целей, подтвердите фазу планирования.",
                    DimScreen = true,
                    CompletionCondition = TutorialStepCondition.PhaseConfirmed,
                    TimeToWait = 0f
                },
                new TutorialStep
                {
                    Message = "ФАЗА СТОЛКНОВЕНИЙ.\n\nКарты, атакующие друг друга, наносят урон одновременно.\nЭто происходит автоматически.",
                    DimScreen = false,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 3f,
                    RequiredPhaseTag = "ClashingPhase"
                },
                new TutorialStep
                {
                    Message = "ФАЗА ОДНОСТОРОННИХ АТАК.\n\nКарты, у которых нет противника напротив, атакуют выбранные цели.\nУрон наносится автоматически.",
                    DimScreen = false,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 3f,
                    RequiredPhaseTag = "OneSidedAttackPhase"
                },
                new TutorialStep
                {
                    Message = "Конец хода!\n\nВ начале следующего хода:\n• Восстанавливаются ресурсы\n• Обе стороны берут по 1 карте\n• Урон зданий сбрасывается",
                    DimScreen = false,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 4f
                },
                new TutorialStep
                {
                    Message = "Обучение завершено!\n\nТеперь вы знаете основы боя. Удачи!",
                    DimScreen = false,
                    CompletionCondition = TutorialStepCondition.TimeElapsed,
                    TimeToWait = 3f
                }
            };
        }
    }
}
