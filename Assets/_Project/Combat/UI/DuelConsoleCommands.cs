using System.Linq;
using System.Text;
using Combat;
using Definitions;
using UnityEngine;

namespace Combat.UI
{
    public static class DuelConsoleCommands
    {
        public static string GetDuelStateInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var state = duelManager.CurrentDuelState;
            
            string playerTownHP = state.PlayerTown is BoardCard pc ? $"{pc.Health}/{pc.MaxHealth}" : "?";
            string opponentTownHP = state.OpponentTown is BoardCard oc ? $"{oc.Health}/{oc.MaxHealth}" : "?";
            
            return $"Turn: {state.TurnNumber}\nPhase: {state.CurrentPhase?.PhaseId ?? "none"}\nPlayer Town HP: {playerTownHP}\nOpponent Town HP: {opponentTownHP}";
        }

        public static string GetBoardCardsInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var board = duelManager.CurrentDuelState.PlayerSide.Board;
            var sb = new StringBuilder();
            sb.AppendLine("=== Player Board ===");
            AppendRow(sb, "Vanguard", board.VanguardRow);
            AppendRow(sb, "Building", board.BuildingRow);
            AppendRow(sb, "Human", board.HumanRow);
            if (board.TownSlot?.Occupant != null)
                sb.AppendLine($"Town: {board.TownSlot.Occupant.SourceCard?.name ?? "???"}");
            return sb.ToString();
        }

        private static void AppendRow(StringBuilder sb, string rowName, IBoardSlot[] slots)
        {
            sb.Append($"{rowName}: ");
            if (slots == null || slots.All(s => s.IsEmpty))
            {
                sb.AppendLine("empty");
                return;
            }
            foreach (var slot in slots)
            {
                if (!slot.IsEmpty)
                    sb.Append($"[{slot.Occupant.SourceCard?.name ?? "???"}] ");
            }
            sb.AppendLine();
        }

        public static string GetHandInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var hand = duelManager.CurrentDuelState.PlayerSide.Hand;
            if (hand == null || hand.Count == 0) return "Hand is empty.";
            var sb = new StringBuilder();
            sb.AppendLine($"=== Player Hand ({hand.Count} cards) ===");
            foreach (var card in hand)
                sb.AppendLine(card.Def?.name ?? "???");
            return sb.ToString();
        }

        public static string GetDeckInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var deck = duelManager.CurrentDuelState.PlayerSide.Deck;
            if (deck == null || deck.Count == 0) return "Deck is empty.";
            var sb = new StringBuilder();
            sb.AppendLine($"=== Player Deck ({deck.Count} cards) ===");
            foreach (var card in deck)
                sb.AppendLine(card.Def?.name ?? "???");
            return sb.ToString();
        }

        public static string GetManaInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var ps = duelManager.CurrentDuelState.PlayerSide;
            var os = duelManager.CurrentDuelState.OpponentSide;
            return $"Player Mana: {ps.Mana}/{ps.MaxMana}\nOpponent Mana: {os.Mana}/{os.MaxMana}";
        }

        public static string GetResourcesInfo()
        {
            var duelManager = Object.FindObjectOfType<DuelManager>();
            if (duelManager == null) return "No DuelManager found.";
            var ps = duelManager.CurrentDuelState.PlayerSide;
            var os = duelManager.CurrentDuelState.OpponentSide;
            return $"Player Humans: {ps.HumanResources}\nOpponent Humans: {os.HumanResources}";
        }
    }
}