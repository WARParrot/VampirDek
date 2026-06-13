namespace Combat
{
    /// <summary>
    /// Условие завершения шага туториала
    /// </summary>
    public enum TutorialStepCondition
    {
        None,
        TimeElapsed,
        PhaseEntered,
        CardDragged,
        CardPlaced,
        TargetSelected,
        PhaseConfirmed,
        ActionExecuted,
        ManualAdvance,
        AttackerCardSelected,
        LeaveDuel,
        DraftCompleted,
    }
}
