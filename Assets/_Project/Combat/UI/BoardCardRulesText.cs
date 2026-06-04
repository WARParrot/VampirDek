using System.Collections.Generic;
using System.Linq;
using Definitions;
using Shared.UI;

namespace Combat.UI
{
    public static class BoardCardRulesText
    {
        public static string FormatBoardCardStats(BoardCard card)
        {
            if (card == null) return "Empty";
            var stats = $"HP {card.Health}/{card.MaxHealth}";
            if (ShouldShowAttack(card)) stats += $"   ATK {card.Attack}";
            if (ShouldShowSpeed(card)) stats += $"   SPD {FormatCurrentSpeedValue(card)}";
            return stats;
        }

        public static string FormatBoardCardDetails(BoardCard card)
        {
            if (card == null) return string.Empty;

            var def = card.SourceCard;
            var lines = new List<string>
            {
                def != null && !string.IsNullOrWhiteSpace(def.CardName) ? def.CardName : "Card",
                $"HP: {card.Health}/{card.MaxHealth}"
            };
            if (ShouldShowAttack(card)) lines.Add($"Attack: {card.Attack}");
            if (ShouldShowSpeed(card)) lines.Add($"Speed: {FormatCurrentSpeed(card)}");

            var targetName = FormatTargetName(card);
            if (!string.IsNullOrEmpty(targetName))
            {
                lines.Add($"Target: {targetName}");
            }

            var passives = FormatRuntimePassivesList(card);
            if (passives.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Passives:");
                lines.AddRange(passives.Select(passive => $"- {passive}"));
            }
            else
            {
                lines.Add(string.Empty);
                lines.Add("Passives: none");
            }

            return string.Join("\n", lines);
        }

        public static string FormatRuntimePassives(BoardCard card)
        {
            var passives = FormatRuntimePassivesList(card);
            return passives.Count == 0 ? string.Empty : string.Join("; ", passives);
        }

        public static List<string> FormatRuntimePassivesList(BoardCard card)
        {
            if (card?.Enchantments == null || card.Enchantments.Count == 0) return new List<string>();
            return card.Enchantments
                .Where(e => e?.Data != null)
                .Select(FormatRuntimeEnchantment)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        private static bool ShouldShowAttack(BoardCard card)
        {
            return card != null && card.Attack != 0;
        }

        private static bool ShouldShowSpeed(BoardCard card)
        {
            return card != null && (card.CurrentSpeed != 0 || CardRulesText.HasSpeed(card.SourceCard));
        }

        private static string FormatCurrentSpeedValue(BoardCard card)
        {
            return card.CurrentSpeed != 0 ? card.CurrentSpeed.ToString() : CardRulesText.FormatSpeedRange(card.SourceCard);
        }

        private static string FormatCurrentSpeed(BoardCard card)
        {
            var roll = CardRulesText.FormatSpeedRange(card.SourceCard);
            return card.CurrentSpeed != 0 && !string.IsNullOrEmpty(roll) ? $"{card.CurrentSpeed} (roll {roll})" : FormatCurrentSpeedValue(card);
        }

        private static string FormatTargetName(BoardCard card)
        {
            var targetName = (card.PlannedTarget as BoardCard)?.SourceCard?.CardName;
            if (card.PlannedTarget != null && string.IsNullOrEmpty(targetName)) targetName = "Town";
            return targetName;
        }

        private static string FormatRuntimeEnchantment(RuntimeEnchantment enchantment)
        {
            var text = CardRulesText.FormatEnchantmentData(enchantment.Data);
            if (enchantment.Data.Duration != -1)
            {
                text += $" ({enchantment.DurationLeft} turns)";
            }
            return text;
        }
    }
}
