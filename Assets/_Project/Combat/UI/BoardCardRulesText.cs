using System.Collections.Generic;
using System.Linq;
using Definitions;
using Shared.UI;
using Shared.Localization;

namespace Combat.UI
{
    public static class BoardCardRulesText
    {
        public static string FormatBoardCardStats(BoardCard card)
        {
            if (card == null) return LocalizationService.T("ui.empty", "Empty");
            var stats = LocalizationService.TFormat("ui.card.hp_full", "HP: {0}/{1}", card.Health, card.MaxHealth);
            if (ShouldShowAttack(card)) stats += "   " + LocalizationService.TFormat("ui.card.attack_short", "ATK {0}", card.Attack);
            if (ShouldShowSpeed(card)) stats += "   " + LocalizationService.TFormat("ui.card.speed_short", "SPD {0}", FormatCurrentSpeedValue(card));
            return stats;
        }

        public static string FormatBoardCardDetails(BoardCard card)
        {
            if (card == null) return string.Empty;

            var def = card.SourceCard;
            var lines = new List<string>
            {
                LocalizationService.CardName(def),
                LocalizationService.TFormat("ui.card.hp_full", "HP: {0}/{1}", card.Health, card.MaxHealth)
            };
            if (ShouldShowAttack(card)) lines.Add(LocalizationService.TFormat("ui.card.attack", "Attack: {0}", card.Attack));
            if (ShouldShowSpeed(card)) lines.Add(LocalizationService.TFormat("ui.card.speed", "Speed: {0}", FormatCurrentSpeed(card)));

            var targetName = FormatTargetName(card);
            if (!string.IsNullOrEmpty(targetName))
            {
                lines.Add(LocalizationService.TFormat("ui.card.target", "Target: {0}", targetName));
            }

            var passives = FormatRuntimePassivesList(card);
            if (passives.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(LocalizationService.T("ui.card.passives", "Passives:"));
                lines.AddRange(passives.Select(passive => $"- {passive}"));
            }
            else
            {
                lines.Add(string.Empty);
                lines.Add(LocalizationService.T("ui.card.passives_none", "Passives: none"));
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
            return card.CurrentSpeed != 0 && !string.IsNullOrEmpty(roll) ? LocalizationService.TFormat("ui.card.speed_roll_current", "{0} (roll {1})", card.CurrentSpeed, roll) : FormatCurrentSpeedValue(card);
        }

        private static string FormatTargetName(BoardCard card)
        {
            if (card?.PlannedTarget == null) return string.Empty;
            var targetCard = card.PlannedTarget as BoardCard;
            return targetCard?.SourceCard != null
                ? LocalizationService.CardName(targetCard.SourceCard)
                : LocalizationService.RowTypeName(Definitions.RowType.Town);
        }

        private static string FormatRuntimeEnchantment(RuntimeEnchantment enchantment)
        {
            var text = CardRulesText.FormatEnchantmentData(enchantment.Data);
            if (enchantment.Data.Duration != -1)
            {
                text += $" ({LocalizationService.TFormat("ui.duration.turns", "{0} turns", enchantment.DurationLeft)})";
            }
            return text;
        }
    }
}
