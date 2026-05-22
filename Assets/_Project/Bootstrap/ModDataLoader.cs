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
            await LoadFolderAsync(_coreDataPath);
            foreach (var dir in modDirs)
                await LoadFolderAsync(dir);
        }

        private async UniTask LoadFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            string cardsDir = Path.Combine(folderPath, "cards");
            if (Directory.Exists(cardsDir))
            {
                foreach (var file in Directory.GetFiles(cardsDir, "*.json"))
                {
                    var json = await File.ReadAllTextAsync(file);
                    var card = ScriptableObject.CreateInstance<CardDef>();
                    JsonUtility.FromJsonOverwrite(json, card);
                    CardDatabase.RegisterCard(card);
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

                    deck.Cards.Clear();
                    foreach (var name in deck.CardNames)
                    {
                        var def = CardDatabase.GetCard(name);
                        if (def != null) deck.Cards.Add(def);
                    }
                    DeckDatabase.RegisterDeck(deck);
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
                    EncounterDatabase.RegisterEncounter(enc);
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
    }
}