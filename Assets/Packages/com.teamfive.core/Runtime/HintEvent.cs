namespace Core
{
    public struct HintEvent : IGameEvent
    {
        public string Tag;
        public object Context;
        public GameMode Mode;
    }
}