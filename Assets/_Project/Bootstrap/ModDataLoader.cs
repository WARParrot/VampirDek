using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using Combat;
using Definitions;
using UnityEngine;

namespace Bootstrap
{
    public class ModDataLoader
    {
        private readonly string _coreDataPath;

        public ModDataLoader()
        {
            _coreDataPath = Path.Combine(Application.streamingAssetsPath, "GameData");
        }

        public async UniTask LoadAllDataAsync(List<string> modDirs)
        {
            if (!string.IsNullOrEmpty(_coreDataPath) && Directory.Exists(_coreDataPath))
                await LoadFolderAsync(_coreDataPath);

            if (modDirs == null) return;
            foreach (var dir in modDirs)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    Debug.LogWarning("[ModDataLoader] Skipping null or empty mod directory.");
                    continue;
                }
                if (!Directory.Exists(dir))
                {
                    Debug.LogWarning($"[ModDataLoader] Mod directory does not exist: {dir}");
                    continue;
                }
                await LoadFolderAsync(dir);
            }
        }

        private async UniTask LoadFolderAsync(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[ModDataLoader] Invalid folder path: {folderPath}");
                return;
            }

            string cardsDir = Path.Combine(folderPath, "cards");
            if (Directory.Exists(cardsDir))
            {
                foreach (var file in Directory.GetFiles(cardsDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var card = ScriptableObject.CreateInstance<CardDef>();
                    JsonUtility.FromJsonOverwrite(json, card);
                    CardDatabase.RegisterCard(card);
                    Debug.Log($"[ModDataLoader] Registered card '{card.CardName}' from {file}");
                }
            }

            string enchDir = Path.Combine(folderPath, "enchantments");
            if (Directory.Exists(enchDir))
            {
                foreach (var file in Directory.GetFiles(enchDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var ench = ScriptableObject.CreateInstance<EnchantmentData>();
                    JsonUtility.FromJsonOverwrite(json, ench);
                    EnchantmentDatabase.RegisterEnchantment(ench);
                }
            }

            string decksDir = Path.Combine(folderPath, "decks");
            if (Directory.Exists(decksDir))
            {
                foreach (var file in Directory.GetFiles(decksDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var deck = ScriptableObject.CreateInstance<DeckData>();
                    JsonUtility.FromJsonOverwrite(json, deck);
                    deck.name = Path.GetFileNameWithoutExtension(file);

                    deck.Cards.Clear();
                    foreach (var name in deck.CardNames)
                    {
                        var def = CardDatabase.GetCard(name);
                        if (def != null) deck.Cards.Add(def);
                    }
                    DeckDatabase.RegisterDeck(deck);
                    Debug.Log($"[ModDataLoader] Registered deck '{deck.name}' with {deck.Cards.Count}/{deck.CardNames.Count} resolved cards from {file}");
                }
            }

            string encDir = Path.Combine(folderPath, "encounters");
            if (Directory.Exists(encDir))
            {
                foreach (var file in Directory.GetFiles(encDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var enc = ScriptableObject.CreateInstance<CombatEncounter>();
                    JsonUtility.FromJsonOverwrite(json, enc);
                    EnsureEncounterDuelSceneReference(enc, json, file);
                    EncounterDatabase.RegisterEncounter(enc);
                    Debug.Log($"[ModDataLoader] Registered encounter '{enc.EncounterId}' from {file}");
                }
            }

            string layoutsDir = Path.Combine(folderPath, "layouts");
            if (Directory.Exists(layoutsDir))
            {
                foreach (var file in Directory.GetFiles(layoutsDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var layout = ScriptableObject.CreateInstance<BoardLayoutData>();
                    JsonUtility.FromJsonOverwrite(json, layout);
                    BoardLayoutDatabase.RegisterLayout(layout);
                }
            }

            string graphsDir = Path.Combine(folderPath, "phasegraphs");
            if (Directory.Exists(graphsDir))
            {
                foreach (var file in Directory.GetFiles(graphsDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var graph = ScriptableObject.CreateInstance<PhaseGraph>();
                    JsonUtility.FromJsonOverwrite(json, graph);

                    foreach (var node in graph.Nodes)
                        foreach (var trans in node.Transitions)
                            trans.Target = graph.Nodes.Find(n => n.PhaseId == trans.Target?.PhaseId);
                    PhaseGraphDatabase.RegisterPhaseGraph(graph);
                }
            }

            string winDir = Path.Combine(folderPath, "winconditions");
            if (Directory.Exists(winDir))
            {
                foreach (var file in Directory.GetFiles(winDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    WinCondition cond;
                    if (json.Contains("\"type\":\"TakeTownWinCondition\""))
                        cond = ScriptableObject.CreateInstance<TakeTownWinCondition>();
                    else
                        cond = ScriptableObject.CreateInstance<WinCondition>();
                    JsonUtility.FromJsonOverwrite(json, cond);
                    WinConditionDatabase.RegisterWinCondition(cond);
                }
            }

            string hintsDir = Path.Combine(folderPath, "hints");
            if (Directory.Exists(hintsDir))
            {
                foreach (var file in Directory.GetFiles(hintsDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var hint = ScriptableObject.CreateInstance<HintData>();
                    JsonUtility.FromJsonOverwrite(json, hint);
                    HintDatabase.RegisterHint(hint);
                }
            }
        }

        private static void EnsureEncounterDuelSceneReference(CombatEncounter encounter, string json, string sourceFile)
        {
            if (encounter == null)
                return;

            if (encounter.DuelScene != null && encounter.DuelScene.RuntimeKeyIsValid())
                return;

            var guid = ExtractJsonString(json, "m_AssetGUID");
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogWarning($"[ModDataLoader] Encounter '{encounter.EncounterId}' from {sourceFile} has no valid DuelScene AssetReference or m_AssetGUID.");
                return;
            }

            encounter.DuelScene = new UnityEngine.AddressableAssets.AssetReference(guid);
            Debug.Log($"[ModDataLoader] Rebuilt DuelScene AssetReference for encounter '{encounter.EncounterId}' from m_AssetGUID '{guid}'.");
        }

        private static string ExtractJsonString(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
                return string.Empty;

            var quotedField = "\"" + fieldName + "\"";
            var fieldIndex = json.IndexOf(quotedField, System.StringComparison.Ordinal);
            if (fieldIndex < 0)
                return string.Empty;

            var colonIndex = json.IndexOf(':', fieldIndex + quotedField.Length);
            if (colonIndex < 0)
                return string.Empty;

            var openQuoteIndex = json.IndexOf('"', colonIndex + 1);
            if (openQuoteIndex < 0)
                return string.Empty;

            var closeQuoteIndex = json.IndexOf('"', openQuoteIndex + 1);
            if (closeQuoteIndex < 0)
                return string.Empty;

            return json.Substring(openQuoteIndex + 1, closeQuoteIndex - openQuoteIndex - 1);
        }
    }
}
