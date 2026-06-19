namespace Exploration
{
    /// <summary>
    /// Process-lifetime state for the escape-the-room quest. Reset by Bootstrap.
    /// Tracks whether the player drank the antidote from the lockbox before exiting.
    /// </summary>
    public static class EscapeQuestState
    {
        public const string PotionItemId = "potion_antidote";

        public static bool PotionConsumed { get; private set; }

        public static void MarkPotionConsumed() => PotionConsumed = true;

        public static void Reset() => PotionConsumed = false;
    }
}
