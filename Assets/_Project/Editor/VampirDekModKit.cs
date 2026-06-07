#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Definitions;
using UnityEditor;
using UnityEngine;

namespace VampirDek11.EditorTools
{
    public static class VampirDekModKit
    {
        private const string ModsRoot = "Mods";
        private const string ExampleModDir = ModsRoot + "/ExampleBloodCult";
        private const string ReportDir = "Assets/_Project/Editor/GeneratedReports";
        private const string ReportPath = ReportDir + "/modding-proof-report.md";

        [MenuItem("Tools/VampirDek11/Mods/Create Example Blood Cult Mod")]
        public static void CreateExampleBloodCultMod()
        {
            WriteExampleModFiles();
            AssetDatabase.Refresh();
            Debug.Log($"[VampirDekModKit] Wrote example mod at {ExampleModDir}");
        }

        [MenuItem("Tools/VampirDek11/Mods/Validate Mods & Write Report")]
        public static void ValidateModsAndWriteReport()
        {
            var report = BuildValidationReport();
            Directory.CreateDirectory(ReportDir);
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[VampirDekModKit] Wrote modding proof report: {ReportPath}");
        }

        [MenuItem("Tools/VampirDek11/Mods/Open Mods Folder")]
        public static void OpenModsFolder()
        {
            Directory.CreateDirectory(ModsRoot);
            EditorUtility.RevealInFinder(ModsRoot);
        }

        private static string BuildValidationReport()
        {
            var builder = new StringBuilder();
            var errors = new List<string>();
            var warnings = new List<string>();
            var infos = new List<string>();

            builder.AppendLine("# VampirDek modding proof report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AppendLine();

            if (!Directory.Exists(ModsRoot))
            {
                errors.Add("Mods folder does not exist. Run Tools/VampirDek11/Mods/Create Example Blood Cult Mod.");
                return Finish(builder, errors, warnings, infos);
            }

            var builtInCards = new HashSet<string>(FindAssets<CardDef>().Where(c => c != null).Select(c => c.CardName), StringComparer.OrdinalIgnoreCase);
            var modCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modDecks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modEncounters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modDir in Directory.GetDirectories(ModsRoot))
            {
                var modName = Path.GetFileName(modDir);
                var modInfo = Path.Combine(modDir, "modinfo.json");
                builder.AppendLine($"## {modName}");
                if (!File.Exists(modInfo))
                {
                    errors.Add($"{modName}: missing modinfo.json.");
                    continue;
                }
                infos.Add($"{modName}: manifest present.");

                foreach (var cardFile in JsonFiles(modDir, "cards"))
                {
                    var cardName = ExtractString(File.ReadAllText(cardFile), "CardName");
                    if (string.IsNullOrWhiteSpace(cardName))
                        errors.Add($"{cardFile}: missing CardName.");
                    else if (!modCards.Add(cardName) || builtInCards.Contains(cardName))
                        errors.Add($"{cardFile}: duplicate card id '{cardName}'.");
                }

                var allCards = new HashSet<string>(builtInCards, StringComparer.OrdinalIgnoreCase);
                allCards.UnionWith(modCards);

                foreach (var deckFile in JsonFiles(modDir, "decks"))
                {
                    var deckId = Path.GetFileNameWithoutExtension(deckFile);
                    modDecks.Add(deckId);
                    foreach (var cardName in ExtractArray(File.ReadAllText(deckFile), "CardNames"))
                    {
                        if (!allCards.Contains(cardName))
                            errors.Add($"{deckFile}: references missing card '{cardName}'.");
                    }
                }

                foreach (var encounterFile in JsonFiles(modDir, "encounters"))
                {
                    var json = File.ReadAllText(encounterFile);
                    var encounterId = ExtractString(json, "EncounterId");
                    if (string.IsNullOrWhiteSpace(encounterId))
                        errors.Add($"{encounterFile}: missing EncounterId.");
                    else
                        modEncounters.Add(encounterId);

                    var opponentDeck = ExtractString(json, "OpponentDeckId");
                    if (string.IsNullOrWhiteSpace(opponentDeck) || !modDecks.Contains(opponentDeck))
                        errors.Add($"{encounterFile}: OpponentDeckId '{opponentDeck}' is not a loaded mod deck.");

                    if (!json.Contains("m_AssetGUID"))
                        warnings.Add($"{encounterFile}: no DuelScene AssetReference GUID; EncounterPoint cannot start this duel.");
                }
            }

            infos.Add($"Mod cards: {modCards.Count}; decks: {modDecks.Count}; encounters: {modEncounters.Count}.");
            if (!modEncounters.Contains(Exploration.ModProofEncounterBootstrap.EncounterId))
                errors.Add($"Missing proof encounter id {Exploration.ModProofEncounterBootstrap.EncounterId}.");
            else
                infos.Add($"Proof encounter {Exploration.ModProofEncounterBootstrap.EncounterId} is present and can be spawned through the explicit modded-duel hub pipeline (`mods hub [maxTables]`), not automatically in every ExplorationMode scene.");

            return Finish(builder, errors, warnings, infos);
        }

        private static string Finish(StringBuilder builder, List<string> errors, List<string> warnings, List<string> infos)
        {
            builder.AppendLine("## Summary");
            builder.AppendLine($"Errors: {errors.Count}");
            builder.AppendLine($"Warnings: {warnings.Count}");
            builder.AppendLine($"Info: {infos.Count}");
            builder.AppendLine();
            AppendSection(builder, "Errors", errors);
            AppendSection(builder, "Warnings", warnings);
            AppendSection(builder, "Info", infos);
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string title, List<string> items)
        {
            builder.AppendLine($"## {title}");
            if (items.Count == 0)
                builder.AppendLine("- None");
            else
                foreach (var item in items) builder.AppendLine("- " + item);
            builder.AppendLine();
        }

        private static IEnumerable<string> JsonFiles(string modDir, string child)
        {
            var dir = Path.Combine(modDir, child);
            return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").OrderBy(p => p) : Enumerable.Empty<string>();
        }

        private static string ExtractString(string json, string field)
        {
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static IEnumerable<string> ExtractArray(string json, string field)
        {
            var pattern = "\"" + Regex.Escape(field) + "\"\\s*:\\s*\\[(.*?)\\]";
            var match = Regex.Match(json, pattern, RegexOptions.Singleline);
            if (!match.Success) yield break;
            foreach (Match item in Regex.Matches(match.Groups[1].Value, "\"([^\"]*)\""))
                yield return item.Groups[1].Value;
        }

        private static List<T> FindAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        private static void WriteExampleModFiles()
        {
            Directory.CreateDirectory(ExampleModDir);
            Directory.CreateDirectory(ExampleModDir + "/cards");
            Directory.CreateDirectory(ExampleModDir + "/decks");
            Directory.CreateDirectory(ExampleModDir + "/encounters");
            Directory.CreateDirectory(ExampleModDir + "/localization");
            File.WriteAllText(ExampleModDir + "/README.md", ExampleReadme, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/modinfo.json", ExampleManifest, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/cards/BloodCultAcolyte.json", BloodCultAcolyte, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/cards/BloodTitheCollector.json", BloodTitheCollector, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/cards/RitualKnifeBearer.json", RitualKnifeBearer, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/decks/ExampleBloodCultDeck.json", ExampleDeck, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/encounters/MOD_PROOF_BLOOD_CULT_INITIATE.json", ExampleEncounter, Encoding.UTF8);
            File.WriteAllText(ExampleModDir + "/localization/en.json", ExampleLocalization, Encoding.UTF8);
        }

        private const string ExampleManifest = @"{
  ""name"": ""Example Blood Cult"",
  ""version"": ""1.0.0"",
  ""catalogPath"": """",
  ""scenes"": [],
  ""cards"": [""BloodCultAcolyte"", ""BloodTitheCollector"", ""RitualKnifeBearer""],
  ""decks"": [""ExampleBloodCultDeck""],
  ""encounters"": [""MOD_PROOF_BLOOD_CULT_INITIATE""]
}";
        private const string BloodCultAcolyte = @"{ ""CardName"": ""BloodCultAcolyte"", ""CardNameKey"": ""mod.example_blood_cult.card.acolyte.name"", ""Type"": 0, ""RowType"": 0, ""MinSpeed"": 1, ""MaxSpeed"": 2, ""Health"": 2, ""Attack"": 1, ""Costs"": [], ""Effects"": [], ""InnateEnchantments"": [], ""CombatVfxProfileId"": ""blood"", ""CombatVfxTint"": { ""r"": 0.65, ""g"": 0.05, ""b"": 0.08, ""a"": 1.0 } }";
        private const string BloodTitheCollector = @"{ ""CardName"": ""BloodTitheCollector"", ""CardNameKey"": ""mod.example_blood_cult.card.tithe_collector.name"", ""Type"": 2, ""RowType"": 2, ""MinSpeed"": 0, ""MaxSpeed"": 1, ""Health"": 3, ""Attack"": 0, ""Costs"": [], ""Effects"": [], ""InnateEnchantments"": [], ""CombatVfxProfileId"": ""ritual"", ""CombatVfxTint"": { ""r"": 0.55, ""g"": 0.0, ""b"": 0.12, ""a"": 1.0 } }";
        private const string RitualKnifeBearer = @"{ ""CardName"": ""RitualKnifeBearer"", ""CardNameKey"": ""mod.example_blood_cult.card.knife_bearer.name"", ""Type"": 0, ""RowType"": 0, ""MinSpeed"": 2, ""MaxSpeed"": 3, ""Health"": 1, ""Attack"": 2, ""Costs"": [], ""Effects"": [], ""InnateEnchantments"": [], ""CombatVfxProfileId"": ""blood"", ""CombatVfxTint"": { ""r"": 0.85, ""g"": 0.1, ""b"": 0.1, ""a"": 1.0 } }";
        private const string ExampleDeck = @"{ ""CardNames"": [""Town"", ""Human"", ""Building"", ""BloodCultAcolyte"", ""BloodTitheCollector"", ""RitualKnifeBearer"", ""VampireFodder""] }";
        private const string ExampleEncounter = @"{
  ""EncounterId"": ""MOD_PROOF_BLOOD_CULT_INITIATE"",
  ""PlayerBoardLayoutId"": ""Standard_BoardLayout"",
  ""OpponentBoardLayoutId"": ""Standard_BoardLayout"",
  ""PhaseGraphId"": ""Standard_Phases"",
  ""WinConditionId"": ""Standard_TakeTownWinCondition"",
  ""OpponentDeckId"": ""ExampleBloodCultDeck"",
  ""HintIds"": [],
  ""RewardCardPool"": [""BloodCultAcolyte"", ""BloodTitheCollector"", ""RitualKnifeBearer""],
  ""DuelScene"": { ""m_AssetGUID"": ""5a6050c62f77eca428f6c6bd526692f1"", ""m_SubObjectName"": """", ""m_SubObjectType"": """", ""m_SubObjectGUID"": """", ""m_EditorAssetChanged"": 0 },
  ""WinFlag"": ""mod.example_blood_cult.defeated"",
  ""LoseFlag"": ""mod.example_blood_cult.lost""
}";
        private const string ExampleLocalization = @"{
  ""mod.example_blood_cult.card.acolyte.name"": ""Blood Cult Acolyte"",
  ""mod.example_blood_cult.card.tithe_collector.name"": ""Blood Tithe Collector"",
  ""mod.example_blood_cult.card.knife_bearer.name"": ""Ritual Knife-Bearer"",
  ""interaction.start_mod_duel"": ""Press [E] to challenge the Blood Cult mod table""
}";
        private const string ExampleReadme = @"# Example Blood Cult mod

This is the modding proof slice. It adds three cards, one opponent deck, one encounter, localization placeholders, and a reward card pool. Runtime bootstrap spawns an EncounterPoint in ExplorationMode when this encounter is loaded, so the duel can be started from an exploration scene.

The current mod API supports JSON cards/decks/encounters plus Addressables catalog loading. Art/VFX/audio are intentionally placeholder-friendly: use CombatVfxProfileId/CombatVfxTint and leave art empty until a real asset pipeline exists.
";
    }
}
#endif
