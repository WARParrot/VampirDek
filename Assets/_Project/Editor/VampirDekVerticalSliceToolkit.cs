#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Combat;
using Definitions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace VampirDek11.EditorTools
{
    /// <summary>
    /// Developer-facing tooling for turning the current prototype base into a complete-feeling vertical slice.
    /// This file intentionally avoids scene/prefab YAML mutation: it creates/updates ScriptableObject assets,
    /// validates references, and writes reports/hooks that designers can review in the Editor.
    /// </summary>
    public static class VampirDekVerticalSliceToolkit
    {
        private const string ReportDir = "Assets/_Project/Editor/GeneratedReports";
        private const string VerticalSliceDeckDir = "Assets/_Project/Combat/Data/Decks/VerticalSlice";
        private const string VerticalSliceEncounterDir = "Assets/_Project/Combat/Data/Encounters/VerticalSlice";
        private const string ReadinessMarkdownPath = ReportDir + "/vertical-slice-readiness.md";
        private const string ReadinessJsonPath = ReportDir + "/vertical-slice-readiness.json";
        private const string HooksMarkdownPath = ReportDir + "/subjective-polish-hooks.md";
        private const string TemplateEncounterPath = "Assets/_Project/Combat/Data/Encounters/Test_Encounter.asset";

        private enum Severity
        {
            Info,
            Warning,
            Error
        }

        private sealed class Issue
        {
            public Severity Severity;
            public string Area;
            public string Message;
            public string Path;
        }

        private sealed class ValidationSnapshot
        {
            public List<Issue> Issues = new List<Issue>();
            public List<CardDef> Cards = new List<CardDef>();
            public List<DeckData> Decks = new List<DeckData>();
            public List<CombatEncounter> Encounters = new List<CombatEncounter>();
            public List<PhaseGraph> PhaseGraphs = new List<PhaseGraph>();
            public List<BoardLayoutData> BoardLayouts = new List<BoardLayoutData>();
            public List<WinCondition> WinConditions = new List<WinCondition>();
            public List<WorldSceneInfo> Worlds = new List<WorldSceneInfo>();
        }

        [MenuItem("Tools/VampirDek11/Vertical Slice/Validate Content & Write Readiness Report")]
        public static void ValidateContentAndWriteReport()
        {
            var snapshot = BuildValidationSnapshot();
            WriteReadinessReports(snapshot);

            var errors = snapshot.Issues.Count(i => i.Severity == Severity.Error);
            var warnings = snapshot.Issues.Count(i => i.Severity == Severity.Warning);
            var infos = snapshot.Issues.Count(i => i.Severity == Severity.Info);

            if (errors > 0)
                Debug.LogError($"[VampirDekVerticalSlice] Validation complete: {errors} errors, {warnings} warnings, {infos} info. Report: {ReadinessMarkdownPath}");
            else if (warnings > 0)
                Debug.LogWarning($"[VampirDekVerticalSlice] Validation complete: 0 errors, {warnings} warnings, {infos} info. Report: {ReadinessMarkdownPath}");
            else
                Debug.Log($"[VampirDekVerticalSlice] Validation passed: {infos} info. Report: {ReadinessMarkdownPath}");
        }

        [MenuItem("Tools/VampirDek11/Vertical Slice/Generate 1-Hour Campaign Scaffold")]
        public static void GenerateVerticalSliceCampaignScaffold()
        {
            EnsureAssetDirectory(VerticalSliceDeckDir);
            EnsureAssetDirectory(VerticalSliceEncounterDir);

            var cards = FindAssets<CardDef>().Where(c => c != null && !string.IsNullOrWhiteSpace(c.CardName)).ToList();
            var cardNames = new HashSet<string>(cards.Select(c => c.CardName), StringComparer.OrdinalIgnoreCase);
            var template = AssetDatabase.LoadAssetAtPath<CombatEncounter>(TemplateEncounterPath) ?? FindAssets<CombatEncounter>().FirstOrDefault();

            if (template == null)
            {
                Debug.LogError("[VampirDekVerticalSlice] Cannot scaffold encounters: no template CombatEncounter found.");
                return;
            }

            var createdOrUpdated = new List<string>();
            var missingCards = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            CreateOrUpdateDeck("VS_Encounter01_HungryScoutsDeck", new[]
            {
                "Town", "Human", "Human", "Building", "Vampire", "VampireFodder", "FreshSpawn"
            }, cardNames, missingCards, createdOrUpdated);

            CreateOrUpdateDeck("VS_Encounter02_BloodlingAmbushDeck", new[]
            {
                "Town", "Human", "Building", "Vampire", "VampireFodder", "FreshSpawn", "Ghoul", "Decoy"
            }, cardNames, missingCards, createdOrUpdated);

            CreateOrUpdateDeck("VS_Encounter03_CryptRitualDeck", new[]
            {
                "Town", "Human", "Human", "Building", "Crypt", "Ghoul", "Ritualist", "VampireFodder", "Decoy"
            }, cardNames, missingCards, createdOrUpdated);

            CreateOrUpdateDeck("VS_Boss_NightMatriarchDeck", new[]
            {
                "Town", "Human", "Building", "BloodAltar", "BloodWitch", "NightFury", "Crypt", "Gourmet", "Ritualist", "Ghoul"
            }, cardNames, missingCards, createdOrUpdated);

            CreateOrUpdateEncounter(template, "VS_Encounter01_HungryScouts", "VS_Encounter01_HungryScoutsDeck", "vs.encounter01.win", "vs.encounter01.loss",
                new[] { "Human", "FreshSpawn", "VampireFodder", "Decoy" }, createdOrUpdated);
            CreateOrUpdateEncounter(template, "VS_Encounter02_BloodlingAmbush", "VS_Encounter02_BloodlingAmbushDeck", "vs.encounter02.win", "vs.encounter02.loss",
                new[] { "Ghoul", "Ritualist", "BloodAltar", "VampireLoner" }, createdOrUpdated);
            CreateOrUpdateEncounter(template, "VS_Encounter03_CryptRitual", "VS_Encounter03_CryptRitualDeck", "vs.encounter03.win", "vs.encounter03.loss",
                new[] { "Crypt", "BloodWitch", "Gourmet", "NightFury" }, createdOrUpdated);
            CreateOrUpdateEncounter(template, "VS_Boss_NightMatriarch", "VS_Boss_NightMatriarchDeck", "vs.boss.win", "vs.boss.loss",
                new[] { "NightFury", "BloodWitch", "Gourmet", "Crypt" }, createdOrUpdated);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missingCards.Count > 0)
                Debug.LogWarning("[VampirDekVerticalSlice] Scaffold generated with missing optional card ids skipped: " + string.Join(", ", missingCards));

            WriteSubjectivePolishHooks(createdOrUpdated, missingCards);
            ValidateContentAndWriteReport();
            Debug.Log("[VampirDekVerticalSlice] Scaffold created/updated:\n" + string.Join("\n", createdOrUpdated));
        }

        [MenuItem("Tools/VampirDek11/Vertical Slice/Write Subjective Polish Hooks")]
        public static void WritePolishHooksOnly()
        {
            WriteSubjectivePolishHooks(new List<string>(), new SortedSet<string>());
            AssetDatabase.Refresh();
            Debug.Log($"[VampirDekVerticalSlice] Wrote subjective polish hooks: {HooksMarkdownPath}");
        }

        [MenuItem("Tools/VampirDek11/Developer/Clear Local Saves (PlayerPrefs + JSON saves)")]
        public static void ClearLocalSaves()
        {
            if (!EditorUtility.DisplayDialog("Clear VampirDek local saves?", "This deletes PlayerPrefs and known JSON save files in Application.persistentDataPath for this Editor user. It does not touch project assets.", "Clear", "Cancel"))
                return;

            PlayerPrefs.DeleteAll();
            var removed = 0;
            var root = Application.persistentDataPath;
            if (Directory.Exists(root))
            {
                foreach (var pattern in new[] { "*.json", "battle_*.json", "save*.dat", "player*.dat" })
                {
                    foreach (var file in Directory.GetFiles(root, pattern))
                    {
                        File.Delete(file);
                        removed++;
                    }
                }
            }

            Debug.Log($"[VampirDekDeveloper] Cleared PlayerPrefs and removed {removed} save-like files from {root}");
        }

        [MenuItem("Tools/VampirDek11/Developer/Print Runtime Progression State")]
        public static void PrintRuntimeProgressionState()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[VampirDekDeveloper] Enter Play Mode to print GlobalServices progression/player state.");
                return;
            }

            var state = Core.GlobalServices.GameStateService?.State;
            var player = Core.GlobalServices.PlayerData;
            var builder = new StringBuilder();
            builder.AppendLine("[VampirDekDeveloper] Runtime progression state");
            builder.AppendLine($"CurrentWorldSceneAddress: {state?.CurrentWorldSceneAddress ?? "<null>"}");
            builder.AppendLine($"ActiveDuelTableId: {state?.ActiveDuelTableId ?? "<null>"}");
            builder.AppendLine("CompletedEncounterIds: " + JoinOrEmpty(state?.CompletedEncounterIds));
            builder.AppendLine("Flags: " + (state?.Flags == null ? "<null>" : string.Join(", ", state.Flags.Select(p => p.Key + "=" + p.Value))));
            builder.AppendLine("OwnedCardIds: " + JoinOrEmpty(player?.OwnedCardIds));
            builder.AppendLine("ActiveDeckCardIds: " + JoinOrEmpty(player?.ActiveDeckCardIds));
            Debug.Log(builder.ToString());
        }

        private static ValidationSnapshot BuildValidationSnapshot()
        {
            var snapshot = new ValidationSnapshot
            {
                Cards = FindAssets<CardDef>(),
                Decks = FindAssets<DeckData>(),
                Encounters = FindAssets<CombatEncounter>(),
                PhaseGraphs = FindAssets<PhaseGraph>(),
                BoardLayouts = FindAssets<BoardLayoutData>(),
                WinConditions = FindAssets<WinCondition>(),
                Worlds = FindAssets<WorldSceneInfo>()
            };

            ValidateCards(snapshot);
            ValidateDecks(snapshot);
            ValidateEncounters(snapshot);
            ValidatePhaseGraphs(snapshot);
            ValidateWorldProgression(snapshot);
            ValidateCompletionReadiness(snapshot);
            return snapshot;
        }

        private static void ValidateCards(ValidationSnapshot snapshot)
        {
            AddInfo(snapshot, "Inventory", $"Detected {snapshot.Cards.Count} CardDef assets.", "Assets/_Project/Combat/Data/Cards");
            var seen = new Dictionary<string, CardDef>(StringComparer.OrdinalIgnoreCase);
            foreach (var card in snapshot.Cards)
            {
                var path = AssetDatabase.GetAssetPath(card);
                if (card == null) continue;
                if (string.IsNullOrWhiteSpace(card.CardName))
                {
                    AddError(snapshot, "Cards", "CardDef has an empty CardName.", path);
                    continue;
                }

                if (seen.TryGetValue(card.CardName, out var other))
                    AddError(snapshot, "Cards", $"Duplicate CardName '{card.CardName}' also used by {AssetDatabase.GetAssetPath(other)}.", path);
                else
                    seen[card.CardName] = card;

                if (card.Health <= 0)
                    AddWarning(snapshot, "Cards", $"Card '{card.CardName}' has non-positive Health ({card.Health}).", path);
                if (card.Attack < 0)
                    AddWarning(snapshot, "Cards", $"Card '{card.CardName}' has negative Attack ({card.Attack}).", path);
                if (card.MinSpeed > card.MaxSpeed)
                    AddError(snapshot, "Cards", $"Card '{card.CardName}' has MinSpeed > MaxSpeed.", path);
                if (card.Costs != null && card.Costs.Any(c => c == null))
                    AddWarning(snapshot, "Cards", $"Card '{card.CardName}' has a null cost entry.", path);
                if (card.Effects != null && card.Effects.Any(e => e == null))
                    AddWarning(snapshot, "Cards", $"Card '{card.CardName}' has a null effect entry.", path);
                if (card.InnateEnchantments != null && card.InnateEnchantments.Any(e => e == null))
                    AddWarning(snapshot, "Cards", $"Card '{card.CardName}' has a null innate enchantment entry.", path);
                if (string.IsNullOrWhiteSpace(card.CardNameKey))
                    AddInfo(snapshot, "Localization", $"Card '{card.CardName}' has no explicit CardNameKey; runtime fallback should be checked during localization polish.", path);
                if (string.IsNullOrWhiteSpace(card.CombatVfxProfileId) && card.Type != CardType.Human && card.Type != CardType.Town)
                    AddInfo(snapshot, "Presentation", $"Card '{card.CardName}' has no CombatVfxProfileId; default VFX will be used until subjective polish assigns a profile.", path);
            }
        }

        private static void ValidateDecks(ValidationSnapshot snapshot)
        {
            AddInfo(snapshot, "Inventory", $"Detected {snapshot.Decks.Count} DeckData assets.", "Assets/_Project/Combat/Data/Decks");
            var cardNames = new HashSet<string>(snapshot.Cards.Where(c => c != null && !string.IsNullOrWhiteSpace(c.CardName)).Select(c => c.CardName), StringComparer.OrdinalIgnoreCase);
            foreach (var deck in snapshot.Decks)
            {
                var path = AssetDatabase.GetAssetPath(deck);
                if (deck == null) continue;
                if (deck.CardNames == null || deck.CardNames.Count == 0)
                {
                    AddError(snapshot, "Decks", $"Deck '{deck.name}' has no CardNames.", path);
                    continue;
                }

                foreach (var cardName in deck.CardNames)
                {
                    if (string.IsNullOrWhiteSpace(cardName))
                        AddError(snapshot, "Decks", $"Deck '{deck.name}' contains an empty card id.", path);
                    else if (!cardNames.Contains(cardName))
                        AddError(snapshot, "Decks", $"Deck '{deck.name}' references missing card '{cardName}'.", path);
                }

                if (!deck.CardNames.Any(c => string.Equals(c, "Town", StringComparison.OrdinalIgnoreCase)))
                    AddWarning(snapshot, "Decks", $"Deck '{deck.name}' has no Town card; verify duel initialization/win condition still works.", path);
            }
        }

        private static void ValidateEncounters(ValidationSnapshot snapshot)
        {
            AddInfo(snapshot, "Inventory", $"Detected {snapshot.Encounters.Count} CombatEncounter assets.", "Assets/_Project/Combat/Data/Encounters");
            var cards = new HashSet<string>(snapshot.Cards.Where(c => c != null && !string.IsNullOrWhiteSpace(c.CardName)).Select(c => c.CardName), StringComparer.OrdinalIgnoreCase);
            var decks = new HashSet<string>(snapshot.Decks.Where(d => d != null).Select(d => d.name), StringComparer.OrdinalIgnoreCase);
            var layouts = new HashSet<string>(snapshot.BoardLayouts.Where(l => l != null).Select(l => l.name), StringComparer.OrdinalIgnoreCase);
            var graphs = new HashSet<string>(snapshot.PhaseGraphs.Where(g => g != null).Select(g => g.name), StringComparer.OrdinalIgnoreCase);
            var wins = new HashSet<string>(snapshot.WinConditions.Where(w => w != null).Select(w => w.name), StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var encounter in snapshot.Encounters)
            {
                var path = AssetDatabase.GetAssetPath(encounter);
                if (encounter == null) continue;
                if (string.IsNullOrWhiteSpace(encounter.EncounterId))
                    AddError(snapshot, "Encounters", "Encounter has empty EncounterId.", path);
                else if (!ids.Add(encounter.EncounterId))
                    AddError(snapshot, "Encounters", $"Duplicate EncounterId '{encounter.EncounterId}'.", path);

                ValidateLookup(snapshot, "Encounters", path, encounter.EncounterId, "OpponentDeckId", encounter.OpponentDeckId, decks);
                ValidateLookup(snapshot, "Encounters", path, encounter.EncounterId, "PlayerBoardLayoutId", encounter.PlayerBoardLayoutId, layouts);
                ValidateLookup(snapshot, "Encounters", path, encounter.EncounterId, "OpponentBoardLayoutId", encounter.OpponentBoardLayoutId, layouts);
                ValidateLookup(snapshot, "Encounters", path, encounter.EncounterId, "PhaseGraphId", encounter.PhaseGraphId, graphs);
                ValidateLookup(snapshot, "Encounters", path, encounter.EncounterId, "WinConditionId", encounter.WinConditionId, wins);

                if (encounter.DuelScene == null || string.IsNullOrWhiteSpace(encounter.DuelScene.AssetGUID))
                    AddError(snapshot, "Encounters", $"Encounter '{encounter.EncounterId}' has no DuelScene AssetReference GUID.", path);

                if (string.Equals(encounter.WinFlag, encounter.LoseFlag, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(encounter.WinFlag))
                    AddWarning(snapshot, "Progression", $"Encounter '{encounter.EncounterId}' uses the same WinFlag and LoseFlag. This is usually wrong for campaign progression.", path);

                if (encounter.RewardCardPool == null || encounter.RewardCardPool.Count == 0)
                    AddWarning(snapshot, "Rewards", $"Encounter '{encounter.EncounterId}' has an empty RewardCardPool.", path);
                else
                {
                    foreach (var reward in encounter.RewardCardPool)
                    {
                        if (string.IsNullOrWhiteSpace(reward))
                            AddError(snapshot, "Rewards", $"Encounter '{encounter.EncounterId}' contains an empty reward card id.", path);
                        else if (!cards.Contains(reward))
                            AddError(snapshot, "Rewards", $"Encounter '{encounter.EncounterId}' reward references missing card '{reward}'.", path);
                    }
                }
            }
        }

        private static void ValidatePhaseGraphs(ValidationSnapshot snapshot)
        {
            foreach (var graph in snapshot.PhaseGraphs)
            {
                var path = AssetDatabase.GetAssetPath(graph);
                if (graph == null) continue;
                if (graph.Nodes == null || graph.Nodes.Count == 0)
                {
                    AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' has no nodes.", path);
                    continue;
                }

                if (graph.StartingNode == null)
                    AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' has no StartingNode.", path);
                else if (!graph.Nodes.Contains(graph.StartingNode))
                    AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' StartingNode is not in Nodes.", path);

                var nodeSet = new HashSet<PhaseNode>(graph.Nodes.Where(n => n != null));
                var phaseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in graph.Nodes)
                {
                    if (node == null)
                    {
                        AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' has a null node.", path);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(node.PhaseId))
                        AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' has a node with empty PhaseId.", AssetDatabase.GetAssetPath(node));
                    else if (!phaseIds.Add(node.PhaseId))
                        AddError(snapshot, "PhaseGraphs", $"PhaseGraph '{graph.name}' has duplicate PhaseId '{node.PhaseId}'.", AssetDatabase.GetAssetPath(node));

                    var sawDefault = false;
                    if (node.Transitions == null) continue;
                    foreach (var transition in node.Transitions)
                    {
                        if (transition == null)
                        {
                            AddError(snapshot, "PhaseGraphs", $"Node '{node.PhaseId}' has a null transition.", AssetDatabase.GetAssetPath(node));
                            continue;
                        }
                        if (transition.Target == null)
                            AddError(snapshot, "PhaseGraphs", $"Node '{node.PhaseId}' has a transition with null Target.", AssetDatabase.GetAssetPath(node));
                        else if (!nodeSet.Contains(transition.Target))
                            AddError(snapshot, "PhaseGraphs", $"Node '{node.PhaseId}' targets a node outside graph '{graph.name}'.", AssetDatabase.GetAssetPath(node));

                        var type = transition.Condition == null ? ConditionType.None : transition.Condition.Type;
                        if (type == ConditionType.None)
                            sawDefault = true;
                        else if (sawDefault)
                            AddWarning(snapshot, "PhaseGraphs", $"Node '{node.PhaseId}' has a conditional transition after an unconditional/default transition. Verify runtime transition priority matches intent.", AssetDatabase.GetAssetPath(node));
                    }
                }
            }
        }

        private static void ValidateWorldProgression(ValidationSnapshot snapshot)
        {
            var emittedFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var encounter in snapshot.Encounters)
            {
                if (!string.IsNullOrWhiteSpace(encounter.WinFlag)) emittedFlags.Add(encounter.WinFlag);
                if (!string.IsNullOrWhiteSpace(encounter.LoseFlag)) emittedFlags.Add(encounter.LoseFlag);
            }

            var requiredFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var world in snapshot.Worlds)
            {
                var path = AssetDatabase.GetAssetPath(world);
                if (world.RequiredFlags == null) continue;
                foreach (var flag in world.RequiredFlags)
                {
                    if (string.IsNullOrWhiteSpace(flag))
                        AddError(snapshot, "WorldProgression", $"World '{world.SceneId}' contains an empty RequiredFlags entry.", path);
                    else
                    {
                        requiredFlags.Add(flag);
                        if (!emittedFlags.Contains(flag))
                            AddWarning(snapshot, "WorldProgression", $"World '{world.SceneId}' requires flag '{flag}', but no encounter currently emits it.", path);
                    }
                }
            }

            foreach (var flag in emittedFlags.Where(f => f.StartsWith("vs.", StringComparison.OrdinalIgnoreCase) && !requiredFlags.Contains(f) && !f.Contains("boss.win")))
                AddInfo(snapshot, "WorldProgression", $"Vertical-slice flag '{flag}' is emitted but not yet used as a WorldSceneInfo.RequiredFlags gate. This is a hook for authored progression wiring.", "Assets/_Project/Data/SceneInfos");
        }

        private static void ValidateCompletionReadiness(ValidationSnapshot snapshot)
        {
            if (snapshot.Cards.Count < 24)
                AddWarning(snapshot, "CompletionReadiness", $"Only {snapshot.Cards.Count} cards detected. A complete-feeling 1-hour slice likely wants 20-30+ available cards or very deliberate encounter scripting.", "Assets/_Project/Combat/Data/Cards");
            if (snapshot.Encounters.Count < 4)
                AddWarning(snapshot, "CompletionReadiness", $"Only {snapshot.Encounters.Count} encounters detected. A 1-hour slice should have at least 3 encounters plus a boss/finale.", "Assets/_Project/Combat/Data/Encounters");
            if (snapshot.Decks.Count < 5)
                AddWarning(snapshot, "CompletionReadiness", $"Only {snapshot.Decks.Count} decks detected. A 1-hour slice should have a starter/fallback deck plus several distinct enemy decks.", "Assets/_Project/Combat/Data/Decks");
        }

        private static void ValidateLookup(ValidationSnapshot snapshot, string area, string path, string ownerId, string field, string value, HashSet<string> validIds)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(snapshot, area, $"Encounter '{ownerId}' has empty {field}.", path);
                return;
            }
            if (!validIds.Contains(value))
                AddError(snapshot, area, $"Encounter '{ownerId}' {field} references missing id/asset name '{value}'.", path);
        }

        private static void CreateOrUpdateDeck(string deckName, IEnumerable<string> desiredCards, HashSet<string> availableCards, SortedSet<string> missingCards, List<string> changed)
        {
            var path = $"{VerticalSliceDeckDir}/{deckName}.asset";
            var deck = LoadOrCreateAsset<DeckData>(path);
            deck.CardNames ??= new List<string>();
            deck.CardNames.Clear();
            foreach (var card in desiredCards)
            {
                if (availableCards.Contains(card)) deck.CardNames.Add(card);
                else missingCards.Add(card);
            }

            if (!deck.CardNames.Any(c => string.Equals(c, "Town", StringComparison.OrdinalIgnoreCase)))
            {
                var town = availableCards.FirstOrDefault(c => string.Equals(c, "Town", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(town)) deck.CardNames.Insert(0, town);
            }

            EditorUtility.SetDirty(deck);
            MarkAssetAddressable(path, deckName, "Decks");
            changed.Add(path);
        }

        private static void CreateOrUpdateEncounter(CombatEncounter template, string encounterId, string opponentDeckId, string winFlag, string loseFlag, IEnumerable<string> rewards, List<string> changed)
        {
            var path = $"{VerticalSliceEncounterDir}/{encounterId}.asset";
            var encounter = LoadOrCreateAsset<CombatEncounter>(path);
            encounter.EncounterId = encounterId;
            encounter.PlayerBoardLayoutId = string.IsNullOrWhiteSpace(template.PlayerBoardLayoutId) ? "Standard_BoardLayout" : template.PlayerBoardLayoutId;
            encounter.OpponentBoardLayoutId = string.IsNullOrWhiteSpace(template.OpponentBoardLayoutId) ? "Standard_BoardLayout" : template.OpponentBoardLayoutId;
            encounter.PhaseGraphId = string.IsNullOrWhiteSpace(template.PhaseGraphId) ? "Standard_Phases" : template.PhaseGraphId;
            encounter.WinConditionId = string.IsNullOrWhiteSpace(template.WinConditionId) ? "Standard_TakeTownWinCondition" : template.WinConditionId;
            encounter.OpponentDeckId = opponentDeckId;
            encounter.HintIds ??= new List<string>();
            encounter.RewardCardPool = rewards.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            encounter.DuelScene = template.DuelScene;
            encounter.WinFlag = winFlag;
            encounter.LoseFlag = loseFlag;
            EditorUtility.SetDirty(encounter);
            MarkAssetAddressable(path, encounterId, "Encounters");
            changed.Add(path);
        }

        private static void MarkAssetAddressable(string assetPath, string address, string label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning($"[VampirDekVerticalSlice] Addressables settings are not initialized; '{assetPath}' was created but not marked with label '{label}'.");
                return;
            }

            var group = settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogWarning($"[VampirDekVerticalSlice] Addressables default group is missing; '{assetPath}' was created but not marked with label '{label}'.");
                return;
            }

            settings.AddLabel(label, false);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"[VampirDekVerticalSlice] Could not resolve GUID for '{assetPath}', so it was not marked Addressable.");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
            {
                Debug.LogWarning($"[VampirDekVerticalSlice] Addressables CreateOrMoveEntry returned null for '{assetPath}'.");
                return;
            }

            entry.address = string.IsNullOrWhiteSpace(address) ? Path.GetFileNameWithoutExtension(assetPath) : address;
            entry.SetLabel(label, true, true, false);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(group);
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            EnsureAssetDirectory(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static List<T> FindAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<T>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        private static void WriteReadinessReports(ValidationSnapshot snapshot)
        {
            EnsureAssetDirectory(ReportDir);
            File.WriteAllText(ReadinessMarkdownPath, BuildMarkdownReport(snapshot));
            File.WriteAllText(ReadinessJsonPath, BuildJsonReport(snapshot));
            AssetDatabase.ImportAsset(ReadinessMarkdownPath);
            AssetDatabase.ImportAsset(ReadinessJsonPath);
        }

        private static string BuildMarkdownReport(ValidationSnapshot snapshot)
        {
            var errors = snapshot.Issues.Count(i => i.Severity == Severity.Error);
            var warnings = snapshot.Issues.Count(i => i.Severity == Severity.Warning);
            var builder = new StringBuilder();
            builder.AppendLine("# VampirDek Vertical Slice Readiness Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();
            builder.AppendLine($"- Status: {(errors > 0 ? "Blocked" : warnings > 0 ? "Playable with warnings" : "Ready for playtest")}");
            builder.AppendLine($"- Errors: {errors}");
            builder.AppendLine($"- Warnings: {warnings}");
            builder.AppendLine($"- Info: {snapshot.Issues.Count(i => i.Severity == Severity.Info)}");
            builder.AppendLine($"- Cards: {snapshot.Cards.Count}");
            builder.AppendLine($"- Decks: {snapshot.Decks.Count}");
            builder.AppendLine($"- Encounters: {snapshot.Encounters.Count}");
            builder.AppendLine($"- Phase graphs: {snapshot.PhaseGraphs.Count}");
            builder.AppendLine($"- Worlds: {snapshot.Worlds.Count}");
            builder.AppendLine();
            builder.AppendLine("## Developer next actions");
            builder.AppendLine();
            builder.AppendLine("1. Fix every Error entry first; these can break play or content loading.");
            builder.AppendLine("2. Review Warning entries before a full playtest; these are likely completeness or progression gaps.");
            builder.AppendLine("3. Use Info entries as polish hooks; they are intentionally non-blocking.");
            builder.AppendLine("4. After subjective content/art/audio changes, run this validator again.");
            builder.AppendLine();
            AppendIssues(builder, snapshot, Severity.Error, "Errors");
            AppendIssues(builder, snapshot, Severity.Warning, "Warnings");
            AppendIssues(builder, snapshot, Severity.Info, "Info / polish hooks");
            return builder.ToString();
        }

        private static void AppendIssues(StringBuilder builder, ValidationSnapshot snapshot, Severity severity, string title)
        {
            builder.AppendLine($"## {title}");
            builder.AppendLine();
            var issues = snapshot.Issues.Where(i => i.Severity == severity).OrderBy(i => i.Area).ThenBy(i => i.Path).ThenBy(i => i.Message).ToList();
            if (issues.Count == 0)
            {
                builder.AppendLine("- None");
                builder.AppendLine();
                return;
            }

            foreach (var issue in issues)
            {
                builder.AppendLine($"- **{issue.Area}**: {issue.Message}");
                if (!string.IsNullOrWhiteSpace(issue.Path)) builder.AppendLine($"  - Path: `{issue.Path}`");
            }
            builder.AppendLine();
        }

        private static string BuildJsonReport(ValidationSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"generatedAtUtc\": \"{DateTime.UtcNow:O}\",");
            builder.AppendLine($"  \"cardCount\": {snapshot.Cards.Count},");
            builder.AppendLine($"  \"deckCount\": {snapshot.Decks.Count},");
            builder.AppendLine($"  \"encounterCount\": {snapshot.Encounters.Count},");
            builder.AppendLine($"  \"errorCount\": {snapshot.Issues.Count(i => i.Severity == Severity.Error)},");
            builder.AppendLine($"  \"warningCount\": {snapshot.Issues.Count(i => i.Severity == Severity.Warning)},");
            builder.AppendLine("  \"issues\": [");
            for (var i = 0; i < snapshot.Issues.Count; i++)
            {
                var issue = snapshot.Issues[i];
                builder.Append("    { ");
                builder.Append($"\"severity\": \"{issue.Severity}\", ");
                builder.Append($"\"area\": \"{JsonEscape(issue.Area)}\", ");
                builder.Append($"\"message\": \"{JsonEscape(issue.Message)}\", ");
                builder.Append($"\"path\": \"{JsonEscape(issue.Path)}\" ");
                builder.Append(i == snapshot.Issues.Count - 1 ? "}\n" : "},\n");
            }
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void WriteSubjectivePolishHooks(IEnumerable<string> changedAssets, IEnumerable<string> missingCards)
        {
            EnsureAssetDirectory(ReportDir);
            var builder = new StringBuilder();
            builder.AppendLine("# VampirDek Subjective Polish Hooks");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            builder.AppendLine();
            builder.AppendLine("This file captures work that is intentionally not objectively completable by an automated code pass. Use it as a checklist for art/audio/story/playtest polish after the technical scaffold is in place.");
            builder.AppendLine();
            builder.AppendLine("## Generated or touched technical assets");
            builder.AppendLine();
            var assets = changedAssets?.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().ToList() ?? new List<string>();
            if (assets.Count == 0) builder.AppendLine("- None in this run; this report was written as a standalone hook checklist.");
            else foreach (var asset in assets) builder.AppendLine($"- `{asset}`");
            builder.AppendLine();
            var missing = missingCards?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList() ?? new List<string>();
            if (missing.Count > 0)
            {
                builder.AppendLine("## Optional card IDs absent during scaffold generation");
                builder.AppendLine();
                foreach (var card in missing) builder.AppendLine($"- `{card}`");
                builder.AppendLine();
            }
            builder.AppendLine("## Subjective polish checklist");
            builder.AppendLine();
            builder.AppendLine("- [ ] Rename scaffold encounters/decks to final player-facing names.");
            builder.AppendLine("- [ ] Replace placeholder encounter reward pools with tuned rewards after playtesting.");
            builder.AppendLine("- [ ] Assign final card/encounter art and inspect UI composition in the Unity Editor.");
            builder.AppendLine("- [ ] Assign card-specific `CombatVfxProfileId`/tints where default VFX feels generic.");
            builder.AppendLine("- [ ] Add/choose music and SFX for menu, exploration, card play, impact, death, victory, loss, and rewards.");
            builder.AppendLine("- [ ] Write final intro, between-encounter, boss, victory, and loss copy through the Novel/localization systems.");
            builder.AppendLine("- [ ] Play through the whole 1-hour slice and tune encounter difficulty by observed player win rate/turn count.");
            builder.AppendLine("- [ ] Capture screenshots/video after Editor/Gate reconnection; layout cannot be certified from static code alone.");
            builder.AppendLine();
            builder.AppendLine("## Objective follow-up hooks");
            builder.AppendLine();
            builder.AppendLine("- Run `Tools/VampirDek11/Vertical Slice/Validate Content & Write Readiness Report` after every content pass.");
            builder.AppendLine("- Run `Tools/VampirDek11/Developer/Clear Local Saves` before first-run tutorial/progression testing.");
            builder.AppendLine("- Use `Tools/VampirDek11/Developer/Print Runtime Progression State` in Play Mode when diagnosing encounter completion/unlock bugs.");
            File.WriteAllText(HooksMarkdownPath, builder.ToString());
            AssetDatabase.ImportAsset(HooksMarkdownPath);
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) return;
            var normalized = assetPath.Replace('\\', '/');
            var parts = normalized.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                Directory.CreateDirectory(normalized);
                return;
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void AddInfo(ValidationSnapshot snapshot, string area, string message, string path) => AddIssue(snapshot, Severity.Info, area, message, path);
        private static void AddWarning(ValidationSnapshot snapshot, string area, string message, string path) => AddIssue(snapshot, Severity.Warning, area, message, path);
        private static void AddError(ValidationSnapshot snapshot, string area, string message, string path) => AddIssue(snapshot, Severity.Error, area, message, path);

        private static void AddIssue(ValidationSnapshot snapshot, Severity severity, string area, string message, string path)
        {
            snapshot.Issues.Add(new Issue { Severity = severity, Area = area ?? "General", Message = message ?? string.Empty, Path = path ?? string.Empty });
        }

        private static string JoinOrEmpty(IEnumerable<string> values)
        {
            if (values == null) return "<null>";
            var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            return list.Count == 0 ? "<empty>" : string.Join(", ", list);
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
#endif
