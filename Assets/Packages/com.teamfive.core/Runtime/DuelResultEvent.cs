namespace Core
{
    public struct DuelResultEvent : IGameEvent
    {
        public bool PlayerWon;
        public string EncounterId;
        public string WinFlag;
        public string LoseFlag;
    }
}