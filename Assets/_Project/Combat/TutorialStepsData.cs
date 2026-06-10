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

                    MessageKey = "tutorial.duel_intro",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "Сейчас справа показан пример карты. На самой карте видны стоимость в правом верхнем углу и название внизу. Нажмите на карту, чтобы открыть подробности: HP, ATK, SPD и эффекты.",

                    MessageKey = "tutorial.card_anatomy",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.ManualAdvance,

                    PreviewCardName = "Vampire",


                    PreviewCaption = "Стоимость — сверху справа. Название — внизу. Клик по карте открывает подробности."

                },

                new TutorialStep

                {

                    Message = "На столе перед вами — не карты, а пустые слоты. В них вы выкладываете карты из руки. Ряды:\n• Vanguard — атакующая линия\n• Building — здания\n• Human — люди (запас HR и жертвы)",

                    MessageKey = "tutorial.board_slots",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "HR — это запас людей. Чаще всего он нужен для зданий.\n\nВ начале хода HR становится равен числу живых карт в вашем ряду Human.",

                    MessageKey = "tutorial.hr_intro",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.ManualAdvance, DynamicArrow = DynamicArrowTarget.PlayerHumanResourcesText

                },

                new TutorialStep

                {

                    Message = "Фаза драфта: в начале каждого хода вам показывают несколько новых карт. Human — исключение и появляется всегда, остальные тянутся из колоды и тратятся при выборе. Возьмите Human (для туториала он обязателен) и одну дополнительную карту.",

                    MessageKey = "tutorial.draft_intro",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.DraftCompleted, RequiredPhaseTag = "StartOfTurn"

                },

                new TutorialStep

                {

                    Message = "Building Phase: выкладываем карты на поле.\n\nСначала сыграйте Human. Он станет платой-жертвой для Vampire.\n\n{PlayableCardHint}",

                    MessageKey = "tutorial.play_human_drag",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardDragged, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableHandCard, PreferredCardName = "Human"

                },

                new TutorialStep

                {

                    Message = "Положите Human в подсвеченный слот ряда Human.\n\n{PlayableCardHint}",

                    MessageKey = "tutorial.play_human_place",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardPlaced, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableBoardSlot, PreferredCardName = "Human"

                },

                new TutorialStep

                {

                    Message = "Теперь сыграйте Vampire. При выкладывании Vampire автоматически заберёт сыгранного Human как жертву-стоимость.\n\n{PlayableCardHint}",

                    MessageKey = "tutorial.play_vampire_drag",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardDragged, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableHandCard, PreferredCardName = "Vampire"

                },

                new TutorialStep

                {

                    Message = "Положите Vampire в подсвеченный слот Vanguard. Human будет принесён в жертву в Vampire, поэтому это нормальное поведение, если Human исчезнет.\n\n{PlayableCardHint}\n\nРяды поля:\n• Vanguard — атакующие карты\n• Building — здания, обычно играются за HR\n• Human — люди для HR, эффектов и жертв",

                    MessageKey = "tutorial.play_vampire_place",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.CardPlaced, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PlayerPlayableBoardSlot, PreferredCardName = "Vampire"

                },

                new TutorialStep

                {

                    Message = "Отлично: Human был использован как жертва, а Vampire вышел в Vanguard. Дальше подтвердим строительство и выберем цель атаки.",

                    MessageKey = "tutorial.confirm_building_intro",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.ManualAdvance, RequiredPhaseTag = "BuildingPhase"

                },

                new TutorialStep

                {

                    Message = "Нажмите подтверждение фазы, чтобы перейти к планированию атак.",

                    MessageKey = "tutorial.confirm_building",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.PhaseConfirmed, TimeToWait = 0f, RequiredPhaseTag = "BuildingPhase", DynamicArrow = DynamicArrowTarget.PhaseConfirmationButton

                },

                new TutorialStep

                {

                    Message = "Planning Phase: здесь назначаются атаки.\n\nЗажмите своего живого бойца с ATK > 0 — например Vampire в Vanguard — и протяните линию к цели.",

                    MessageKey = "tutorial.planning_intro",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.AttackerCardSelected, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.PlayerVanguardCard

                },

                new TutorialStep

                {

                    Message = "Отпустите на живой карте противника. Выбранный Vampire будет атаковать её в бою.",

                    MessageKey = "tutorial.select_target",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.TargetSelected, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.EnemyAnyAliveCard

                },

                new TutorialStep

                {

                    Message = "Здания работают как щит. Пока у противника живо хотя бы одно здание, оно прикрывает двух людей за собой. Но город всё ещё можно бить — пока стоит лишь одно здание. Как только оба здания живы — город под прикрытием, и сначала придётся пробить хотя бы одно из них.",

                    MessageKey = "tutorial.buildings_shield",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "Цель выбрана. Подтвердите Planning Phase — дальше бой разыграется автоматически.",

                    MessageKey = "tutorial.confirm_planning",

                    DimScreen = true, CompletionCondition = TutorialStepCondition.PhaseConfirmed, TimeToWait = 0f, RequiredPhaseTag = "PlanningPhase", DynamicArrow = DynamicArrowTarget.PhaseConfirmationButton

                },

                new TutorialStep

                {

                    Message = "Бой: если две карты атакуют друг друга, происходит столкновение. Урон считается автоматически.",

                    MessageKey = "tutorial.clash_intro",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "Если атака не встречена ответной атакой, карта просто наносит урон выбранной цели.",

                    MessageKey = "tutorial.one_sided_attack",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "Ход почти закончен. На следующем ходу HR обновится по числу живых Human, игроки возьмут карты, а временный урон зданий сбросится.",

                    MessageKey = "tutorial.turn_end",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.ManualAdvance

                },

                new TutorialStep

                {

                    Message = "Основы дуэли разобраны. Теперь можно отойти от стола и осмотреться вокруг — бой сохранится.\n\nЧтобы открыть дальнейший путь, вернитесь к столу и доведите дуэль до победы: разрушьте город противника.\n\nНажмите S, чтобы покинуть стол. Когда будете готовы продолжить бой, подойдите к нему снова и нажмите E.",

                    MessageKey = "tutorial.leave_duel",

                    DimScreen = false, CompletionCondition = TutorialStepCondition.LeaveDuel, TimeToWait = 0f

                }

            };

        }

    }

}
