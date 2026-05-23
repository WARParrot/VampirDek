namespace Core
{
    public static class DuelEndedSignal
    {
        public static System.Action OnDuelEnded;
        public static void Raise() => OnDuelEnded?.Invoke();
    }
}