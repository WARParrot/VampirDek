using System.Linq;
using System.Text;
using Definitions;
using UnityEngine;

namespace Combat.UI
{
    public static class DuelConsoleCommands
    {
        public static string GetDuelStateInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var s = dm.CurrentDuelState;
            string ptHP = s.PlayerTown is BoardCard pc ? $"{pc.Health}/{pc.MaxHealth}" : "?";
            string otHP = s.OpponentTown is BoardCard oc ? $"{oc.Health}/{oc.MaxHealth}" : "?";
            return $"Turn: {s.TurnNumber}\nPhase: {s.CurrentPhase?.PhaseId ?? "none"}\nPlayer Town HP: {ptHP}\nOpponent Town HP: {otHP}";
        }

        public static string GetBoardCardsInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var board = dm.CurrentDuelState.PlayerSide.Board;
            var sb = new StringBuilder();
            sb.AppendLine("=== Player Board ===");
            AppendRow(sb, "Vanguard", board.VanguardRow);
            AppendRow(sb, "Building", board.BuildingRow);
            AppendRow(sb, "Human", board.HumanRow);
            if (board.TownSlot?.Occupant != null)
                sb.AppendLine($"Town: {board.TownSlot.Occupant.SourceCard?.CardName ?? "???"} ({board.TownSlot.Occupant.Health}/{board.TownSlot.Occupant.MaxHealth})");
            return sb.ToString();
        }

        public static string GetHandInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var hand = dm.CurrentDuelState.PlayerSide.Hand;
            if (hand == null || hand.Count == 0) return "Hand is empty.";
            var sb = new StringBuilder();
            sb.AppendLine($"=== Player Hand ({hand.Count} cards) ===");
            for (int i = 0; i < hand.Count; i++)
                sb.AppendLine($"[{i}] {hand[i].Def?.CardName ?? "???"}");
            return sb.ToString();
        }

        public static string GetDeckInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var deck = dm.CurrentDuelState.PlayerSide.Deck;
            if (deck == null || deck.Count == 0) return "Deck is empty.";
            var sb = new StringBuilder();
            sb.AppendLine($"=== Player Deck ({deck.Count} cards) ===");
            foreach (var card in deck)
                sb.AppendLine(card.Def?.CardName ?? "???");
            return sb.ToString();
        }

        public static string GetManaInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var ps = dm.CurrentDuelState.PlayerSide;
            var os = dm.CurrentDuelState.OpponentSide;
            return $"Player Mana: {ps.Mana}/{ps.MaxMana}\nOpponent Mana: {os.Mana}/{os.MaxMana}";
        }

        public static string GetResourcesInfo()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var ps = dm.CurrentDuelState.PlayerSide;
            var os = dm.CurrentDuelState.OpponentSide;
            return $"Player Humans: {ps.HumanResources}\nOpponent Humans: {os.HumanResources}";
        }

        public static string KillCard(string slotSpec)
        {
            var (slot, _) = FindSlot(slotSpec);
            if (slot == null || slot.Occupant == null) return $"Slot '{slotSpec}' not found or empty.";
            var name = slot.Occupant.SourceCard?.CardName ?? "???";
            slot.Occupant.TakeDamage(slot.Occupant.Health, null);
            return $"Killed {name} in slot '{slotSpec}'.";
        }

        public static string HealCard(string slotSpec, int amount)
        {
            var (slot, _) = FindSlot(slotSpec);
            if (slot == null || slot.Occupant == null) return $"Slot '{slotSpec}' not found or empty.";
            slot.Occupant.Heal(amount);
            return $"Healed {slot.Occupant.SourceCard?.CardName} for {amount}. HP: {slot.Occupant.Health}/{slot.Occupant.MaxHealth}";
        }

        public static string DamageCard(string slotSpec, int amount)
        {
            var (slot, _) = FindSlot(slotSpec);
            if (slot == null || slot.Occupant == null) return $"Slot '{slotSpec}' not found or empty.";
            slot.Occupant.TakeDamage(amount, null);
            string alive = slot.Occupant.IsAlive ? $"HP: {slot.Occupant.Health}/{slot.Occupant.MaxHealth}" : "DIED";
            return $"Dealt {amount} damage to {slot.Occupant.SourceCard?.CardName}. {alive}";
        }

        public static string BuffCard(string slotSpec, int amount)
        {
            var (slot, _) = FindSlot(slotSpec);
            if (slot == null || slot.Occupant == null) return $"Slot '{slotSpec}' not found or empty.";
            slot.Occupant.ModifyAttack(amount);
            return $"Buffed {slot.Occupant.SourceCard?.CardName} attack by {amount}. ATK: {slot.Occupant.Attack}";
        }

        public static string DrawCards(int count)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            dm.CurrentDuelState.PlayerSide.DrawCards(count);
            return $"Drew {count} card(s). Hand size: {dm.CurrentDuelState.PlayerSide.Hand.Count}";
        }

        public static string DiscardCard(int index)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var hand = dm.CurrentDuelState.PlayerSide.Hand;
            if (index < 0 || index >= hand.Count) return $"Invalid index {index}. Hand size: {hand.Count}";
            var card = hand[index];
            hand.RemoveAt(index);
            dm.CurrentDuelState.PlayerSide.Graveyard.Add(card);
            return $"Discarded {card.Def?.CardName ?? "???"}.";
        }

        public static string DiscardRandomCard()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            dm.CurrentDuelState.PlayerSide.DiscardRandomCards(1);
            return "Discarded 1 random card.";
        }

        public static string AddCardToHand(string cardName)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var def = CardDatabase.GetCard(cardName);
            if (def == null) return $"Card '{cardName}' not found in database.";
            var card = new Card(def, UnityEngine.Random.Range(10000, 99999));
            dm.CurrentDuelState.PlayerSide.Hand.Add(card);
            return $"Added '{cardName}' to hand. Hand size: {dm.CurrentDuelState.PlayerSide.Hand.Count}";
        }

        public static string ShuffleDeck()
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var deck = dm.CurrentDuelState.PlayerSide.Deck;
            var rng = new System.Random();
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return $"Shuffled deck ({deck.Count} cards).";
        }

        public static string AddCardToDeck(string cardName)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var def = CardDatabase.GetCard(cardName);
            if (def == null) return $"Card '{cardName}' not found in database.";
            var card = new Card(def, UnityEngine.Random.Range(10000, 99999));
            dm.CurrentDuelState.PlayerSide.Deck.Add(card);
            return $"Added '{cardName}' to deck. Deck size: {dm.CurrentDuelState.PlayerSide.Deck.Count}";
        }

        public static string SetMana(int amount)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var ps = dm.CurrentDuelState.PlayerSide;
            ps.Mana = Mathf.Max(0, amount);
            return $"Player mana set to {ps.Mana}.";
        }

        public static string AddMana(int amount)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            var ps = dm.CurrentDuelState.PlayerSide;
            ps.Mana = Mathf.Max(0, ps.Mana + amount);
            return $"Player mana: {ps.Mana}";
        }

        public static string SetHumans(int amount)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            dm.CurrentDuelState.PlayerSide.HumanResources = Mathf.Max(0, amount);
            return $"Player humans set to {dm.CurrentDuelState.PlayerSide.HumanResources}.";
        }

        public static string AddHumans(int amount)
        {
            var dm = GetDuelManager();
            if (dm == null) return "No DuelManager found.";
            dm.CurrentDuelState.PlayerSide.HumanResources = Mathf.Max(0, dm.CurrentDuelState.PlayerSide.HumanResources + amount);
            return $"Player humans: {dm.CurrentDuelState.PlayerSide.HumanResources}";
        }

        private static DuelManager GetDuelManager() => UnityEngine.Object.FindObjectOfType<DuelManager>();

        private static (IBoardSlot slot, string row) FindSlot(string spec)
        {
            var dm = GetDuelManager();
            if (dm == null) return (null, null);
            var board = dm.CurrentDuelState.PlayerSide.Board;

            spec = spec.ToLower();
            if (spec == "town") return (board.TownSlot, "town");

            var parts = spec.Split('_');
            if (parts.Length != 2) return (null, null);
            string row = parts[0];
            if (!int.TryParse(parts[1], out int idx)) return (null, null);

            IBoardSlot[] slots = row switch
            {
                "vanguard" => board.VanguardRow,
                "building" => board.BuildingRow,
                "human" => board.HumanRow,
                _ => null
            };
            if (slots == null || idx < 0 || idx >= slots.Length) return (null, null);
            return (slots[idx], row);
        }

        private static void AppendRow(StringBuilder sb, string rowName, IBoardSlot[] slots)
        {
            sb.Append($"{rowName}: ");
            if (slots == null || slots.All(s => s.IsEmpty))
            {
                sb.AppendLine("empty");
                return;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty)
                    sb.Append($"[{i}] {slot.Occupant.SourceCard?.CardName ?? "???"} ({slot.Occupant.Health}/{slot.Occupant.MaxHealth}) ");
            }
            sb.AppendLine();
        }

        public static string ListCards()
        {
            var allCards = Resources.FindObjectsOfTypeAll<CardDef>();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Available Cards ({allCards.Length}) ===");
            foreach (var c in allCards)
                sb.AppendLine($"  {c.CardName}");
            return sb.ToString();
        }
    }
}