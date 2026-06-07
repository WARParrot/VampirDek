namespace Core
{
    public enum DuelOutcome
    {
        InProgress,
        PlayerWon,
        PlayerLost,
        Draw
    }

    public struct DuelResultEvent : IGameEvent
    {
        public bool PlayerWon;
        public DuelOutcome Outcome;
        public string EncounterId;
        public string WinFlag;
        public string LoseFlag;
    }
}