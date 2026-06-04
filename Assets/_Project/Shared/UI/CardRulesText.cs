using System.Collections.Generic;
using System.Linq;
using Definitions;

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
                $"HP {def.Health}"
            };
            if (HasAttack(def)) lines.Add($"Attack: {def.Attack}");
            if (HasSpeed(def)) lines.Add($"Speed roll: {FormatSpeedRange(def)}");

            var passives = FormatInnatePassives(def);
            if (!string.IsNullOrEmpty(passives))
            {
                lines.Add("Passives:");
                lines.AddRange(passives.Split(';').Select(passive => $"- {passive.Trim()}").Where(line => line.Length > 2));
            }
            else
            {
                lines.Add("Passives: none");
            }

            return string.Join("\n", lines.Where(line => !string.IsNullOrEmpty(line)));
        }

        public static string FormatCostLine(CardDef def)
        {
            if (def?.Costs == null || def.Costs.Count == 0) return "Cost: free";
            return $"Cost: {string.Join(" ", def.Costs.Where(c => c != null).Select(c => c.GetCostText()))}";
        }

        private static string FormatCompactCost(CardCost cost)
        {
            switch (cost)
            {
                case ManaCost manaCost:
                    return manaCost.Amount > 0 ? manaCost.Amount.ToString() : string.Empty;
                case HumanResourceCost humanResourceCost:
                    return humanResourceCost.Amount > 0 ? $"{humanResourceCost.Amount} HR" : string.Empty;
                case SacrificeCost sacrificeCost:
                    if (sacrificeCost.Amount <= 0) return string.Empty;
                    if (sacrificeCost.RequiredRowType.ToString() == "Human")
                    {
                        return sacrificeCost.Amount == 1 ? "Human" : $"{sacrificeCost.Amount} Human";
                    }
                    return sacrificeCost.GetCostText();
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

            var name = !string.IsNullOrWhiteSpace(data.DisplayName) ? data.DisplayName : "Passive";
            var details = BuildEnchantmentDetails(data);
            return string.IsNullOrEmpty(details) ? name : $"{name}: {details}";
        }

        private static string BuildEnchantmentDetails(EnchantmentData data)
        {
            var parts = new List<string>();

            if (data.Modifiers != null)
            {
                foreach (var modifier in data.Modifiers.Where(m => m != null))
                {
                    var stat = string.IsNullOrWhiteSpace(modifier.Stat) ? "Stat" : modifier.Stat;
                    var value = modifier.Type == ModifierType.Multiply
                        ? $"x{modifier.Value}"
                        : modifier.Value >= 0 ? $"+{modifier.Value}" : modifier.Value.ToString();
                    parts.Add($"{stat} {value}");
                }
            }

            if (data.Triggers != null)
            {
                foreach (var trigger in data.Triggers.Where(t => t != null && !string.IsNullOrWhiteSpace(t.EventType)))
                {
                    parts.Add($"on {ShortTypeName(trigger.EventType)}");
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
