using System.Collections.Generic;
using System.Linq;
using Definitions;
using Shared.Localization;

namespace Shared.UI
{
    public static class CardRulesText
    {
        public static string FormatHandCardSummary(CardDef def)
        {
            return FormatHandCornerCost(def);
        }

        public static string FormatHandCornerCost(CardDef def)
        {
            if (def?.Costs == null || def.Costs.Count == 0) return string.Empty;

            var parts = def.Costs
                .Where(cost => cost != null)
                .Select(FormatCompactCost)
                .Where(text => !string.IsNullOrWhiteSpace(text));

            return string.Join(" ", parts);
        }

        public static string FormatHandCardDetails(CardDef def)
        {
            if (def == null) return string.Empty;

            var lines = new List<string>
            {
                FormatCostLine(def),
                LocalizationService.TFormat("ui.card.hp", "HP {0}", def.Health)
            };
            if (HasAttack(def)) lines.Add(LocalizationService.TFormat("ui.card.attack", "Attack: {0}", def.Attack));
            if (HasSpeed(def)) lines.Add(LocalizationService.TFormat("ui.card.speed_roll", "Speed roll: {0}", FormatSpeedRange(def)));

            var passives = FormatInnatePassives(def);
            if (!string.IsNullOrEmpty(passives))
            {
                lines.Add(LocalizationService.T("ui.card.passives", "Passives:"));
                lines.AddRange(passives.Split(';').Select(passive => $"- {passive.Trim()}").Where(line => line.Length > 2));
            }
            else
            {
                lines.Add(LocalizationService.T("ui.card.passives_none", "Passives: none"));
            }

            if (!string.IsNullOrWhiteSpace(def.Description))
            {
                lines.Add("");
                lines.Add(def.Description.Trim());
            }

            return string.Join("\n", lines.Where(line => line != null));
        }

        public static string FormatCostLine(CardDef def)
        {
            if (def?.Costs == null || def.Costs.Count == 0) return LocalizationService.T("ui.cost.free", "Cost: free");
            return LocalizationService.TFormat("ui.cost.line", "Cost: {0}", string.Join(" ", def.Costs.Where(c => c != null).Select(FormatCostText)));
        }

        public static string FormatCostText(CardCost cost)
        {
            if (cost == null) return string.Empty;
            switch (cost)
            {
                case HumanResourceCost humanResourceCost:
                    return LocalizationService.TFormat("ui.cost.hr", "{0} HR", humanResourceCost.Amount);
                case SacrificeCost sacrificeCost:
                    if (sacrificeCost.Amount <= 0) return string.Empty;
                    var rowName = LocalizationService.RowTypeName(sacrificeCost.RequiredRowType);
                    if (sacrificeCost.RequiredRowType.ToString() == "Human")
                    {
                        return sacrificeCost.Amount == 1
                            ? LocalizationService.T("ui.cost.human.single", "Human")
                            : LocalizationService.TFormat("ui.cost.human.many", "{0} Human", sacrificeCost.Amount);
                    }
                    return sacrificeCost.Amount == 1
                        ? LocalizationService.TFormat("ui.cost.sacrifice.single", "Sacrifice one {0}", rowName)
                        : LocalizationService.TFormat("ui.cost.sacrifice.many", "Sacrifice {1} {0} cards", rowName, sacrificeCost.Amount);
                default:
                    return cost.GetCostText();
            }
        }

        private static string FormatCompactCost(CardCost cost)
        {
            switch (cost)
            {
                case HumanResourceCost humanResourceCost:
                    return humanResourceCost.Amount > 0 ? LocalizationService.TFormat("ui.cost.hr", "{0} HR", humanResourceCost.Amount) : string.Empty;
                case SacrificeCost sacrificeCost:
                    if (sacrificeCost.Amount <= 0) return string.Empty;
                    if (sacrificeCost.RequiredRowType.ToString() == "Human")
                    {
                        return sacrificeCost.Amount == 1
                            ? LocalizationService.T("ui.cost.human.single", "Human")
                            : LocalizationService.TFormat("ui.cost.human.many", "{0} Human", sacrificeCost.Amount);
                    }
                    return FormatCostText(sacrificeCost);
                default:
                    return cost.GetCostText();
            }
        }

        public static bool HasAttack(CardDef def)
        {
            return def != null && def.Attack != 0;
        }

        public static bool HasSpeed(CardDef def)
        {
            return def != null && (def.MinSpeed != 0 || def.MaxSpeed != 0);
        }

        public static string FormatSpeedRange(CardDef def)
        {
            if (!HasSpeed(def)) return string.Empty;
            return def.MinSpeed == def.MaxSpeed ? def.MinSpeed.ToString() : $"{def.MinSpeed}-{def.MaxSpeed}";
        }

        public static string FormatInnatePassives(CardDef def)
        {
            if (def?.InnateEnchantments == null || def.InnateEnchantments.Count == 0) return string.Empty;
            return string.Join("; ", def.InnateEnchantments.Where(e => e != null).Select(FormatEnchantmentData));
        }

        public static string FormatEnchantmentData(EnchantmentData data)
        {
            if (data == null) return string.Empty;

            var name = LocalizationService.EnchantmentName(data);
            var details = BuildEnchantmentDetails(data);
            return string.IsNullOrEmpty(details) ? name : LocalizationService.TFormat("ui.passive.detail", "{0}: {1}", name, details);
        }

        private static string BuildEnchantmentDetails(EnchantmentData data)
        {
            var parts = new List<string>();

            if (data.Modifiers != null)
            {
                foreach (var modifier in data.Modifiers.Where(m => m != null))
                {
                    var stat = LocalizationService.StatName(modifier.Stat);
                    var value = modifier.Type == ModifierType.Multiply
                        ? LocalizationService.TFormat("ui.modifier.multiply", "{0} x{1}", stat, modifier.Value)
                        : LocalizationService.TFormat("ui.modifier.add", "{0} {1}", stat, modifier.Value >= 0 ? $"+{modifier.Value}" : modifier.Value.ToString());
                    parts.Add(value);
                }
            }

            if (data.Triggers != null)
            {
                foreach (var trigger in data.Triggers.Where(t => t != null && !string.IsNullOrWhiteSpace(t.EventType)))
                {
                    parts.Add(LocalizationService.TFormat("ui.trigger.on", "on {0}", ShortTypeName(trigger.EventType)));
                }
            }

            return string.Join(", ", parts);
        }

        private static string ShortTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return string.Empty;
            var lastDot = typeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < typeName.Length ? typeName.Substring(lastDot + 1) : typeName;
        }
    }
}
