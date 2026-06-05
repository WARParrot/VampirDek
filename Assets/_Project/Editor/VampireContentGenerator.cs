#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Combat;
using Definitions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace VampirDek11.EditorTools
{
    public static class VampireContentGenerator
    {
        private const string CardsDir = "Assets/_Project/Combat/Data/Cards/Generated";
        private const string EnchDir = "Assets/_Project/Combat/Data/Enchantments/Generated";
        private const string CostsDir = "Assets/_Project/Combat/Data/Costs/Generated";
        private const string DeckPath = "Assets/_Project/Combat/Data/Decks/FallbackDeck.asset";
        private const string ResourcesDir = "Assets/Resources";
        private const string RegistryPath = "Assets/Resources/CardRegistry.asset";

        private const string ExistingHumanPayGuid = "dfcdab09cda31684fa68f04c9067488e";
        private const string ExistingManaPayGuidFallback = ""; // not assumed; will look up by type

        [MenuItem("Tools/VampirDek11/Generate Vampire Content Pass")]
        public static void Generate()
        {
            EnsureDir(CardsDir);
            EnsureDir(EnchDir);
            EnsureDir(CostsDir);

            var humanPay = LoadByGuid<HumanResourcePayActionDefinition>(ExistingHumanPayGuid)
                ?? FindOrCreate<HumanResourcePayActionDefinition>($"{CostsDir}/Standard_HumanResourcePay.asset");
            var manaPay = FindFirst<ManaPayActionDefinition>()
                ?? FindOrCreate<ManaPayActionDefinition>($"{CostsDir}/Standard_ManaPay.asset");

            var hr1 = CreateHumanResourceCost("HumanResourceCost_1", 1, humanPay);
            var hr2 = CreateHumanResourceCost("HumanResourceCost_2_gen", 2, humanPay);
            var mana2 = CreateManaCost("ManaCost_2", 2, manaPay);
            var mana3 = CreateManaCost("ManaCost_3", 3, manaPay);

            // -------- Enchantments / passives --------

            // Ghoul: recount on placed + on died (any side, any card)
            var ghoulRecountAttack = CreateRecountSiblingsAction("Ghoul_Recount", CardBehaviorTags.Ghoul, baseAttack: 1, bonusPerSibling: 1);
            var ghoulEnch = CreateEnchantment("Enchantment_GhoulPack", new[]
            {
                Trigger("Combat.PlacedCardEvent", ghoulRecountAttack),
                Trigger("Combat.EntityDiedEvent", ghoulRecountAttack),
            });

            // Blood Witch
            var witchOnSpawnDamage = CreateAutoDamageAction("BloodWitch_OnSpawn", amount: 3, preferHighest: true);
            var witchEotDamage = CreateRandomDamageAction("BloodWitch_EoT", amount: 2, includeTown: true);
            var witchEnch = CreateEnchantment("Enchantment_BloodWitch", new[]
            {
                Trigger("Combat.PlacedCardEvent", witchOnSpawnDamage, ownerOnly: true),
                Trigger("Combat.PhaseExitEvent", witchEotDamage, phaseFilter: "EndOfTurn"),
            });

            // Ritualist: +1 atk per round on EoT
            var ritualistEotPlusOne = CreateModifyAttackAction("Ritualist_EoT_PlusOne", delta: 1);
            var ritualistEnch = CreateEnchantment("Enchantment_Ritualist", new[]
            {
                Trigger("Combat.PhaseExitEvent", ritualistEotPlusOne, phaseFilter: "EndOfTurn", ownerOnly: false),
            });

            // Blood Altar: +1 HR on StartOfTurn
            var altarGenHr = CreateGenerateHrAction("BloodAltar_GenHR", amount: 1);
            var altarEnch = CreateEnchantment("Enchantment_BloodAltar", new[]
            {
                Trigger("Combat.PhaseEnterEvent", altarGenHr, phaseFilter: "StartOfTurn"),
            });

            // Crypt: spawn Ghoul when friendly vampire dies (once per round)
            // We create card defs in a phased pass so Crypt can refer to Ghoul. Build Ghoul first.

            // -------- Card definitions --------

            var ghoul = CreateCard("Ghoul", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 1, maxSpeed: 1, hp: 2, atk: 1,
                costs: new List<CardCost> { hr1 },
                enchantments: new List<EnchantmentData> { ghoulEnch });

            var bloodWitch = CreateCard("BloodWitch", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 1, maxSpeed: 1, hp: 1, atk: 3,
                costs: new List<CardCost> { mana2 },
                enchantments: new List<EnchantmentData> { witchEnch });

            var nightFury = CreateCard("NightFury", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 4, maxSpeed: 4, hp: 2, atk: 2,
                costs: new List<CardCost> { hr2 },
                enchantments: new List<EnchantmentData>());

            var vampireLoner = CreateCard("VampireLoner", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 2, maxSpeed: 2, hp: 3, atk: 1,
                costs: new List<CardCost> { hr1 },
                enchantments: new List<EnchantmentData>());

            var freshSpawn = CreateCard("FreshSpawn", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 1, maxSpeed: 1, hp: 4, atk: 2,
                costs: new List<CardCost> { hr2 },
                enchantments: new List<EnchantmentData>());

            var ritualist = CreateCard("Ritualist", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 1, maxSpeed: 1, hp: 1, atk: 1,
                costs: new List<CardCost> { hr1 },
                enchantments: new List<EnchantmentData> { ritualistEnch });

            var decoy = CreateCard("Decoy", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 0, maxSpeed: 0, hp: 1, atk: 0,
                costs: new List<CardCost> { hr1 },
                enchantments: new List<EnchantmentData>());

            // Gourmet: spawn Decoy right of self on placement
            var gourmetSpawnDecoy = CreateSpawnAction("Gourmet_SpawnDecoy", decoy, Definitions.RowType.Vanguard, SpawnSlotRule.AdjacentRight, enforceRow: true);
            var gourmetEnch = CreateEnchantment("Enchantment_Gourmet", new[]
            {
                Trigger("Combat.PlacedCardEvent", gourmetSpawnDecoy, ownerOnly: true),
            });
            var gourmet = CreateCard("Gourmet", CardType.Vanguard, Definitions.RowType.Vanguard,
                minSpeed: 3, maxSpeed: 3, hp: 2, atk: 4,
                costs: new List<CardCost> { hr2 },
                enchantments: new List<EnchantmentData> { gourmetEnch });

            var bloodAltar = CreateCard("BloodAltar", CardType.Building, Definitions.RowType.Building,
                minSpeed: 0, maxSpeed: 0, hp: 4, atk: 0,
                costs: new List<CardCost> { mana2 },
                enchantments: new List<EnchantmentData> { altarEnch });

            var cryptSpawnGhoul = CreateSpawnOnFriendlyDeathAction("Crypt_SpawnGhoul", ghoul,
                filterName: null, // any friendly death — gameplay intent says "friendly vampire"; we'll keep open and rely on no enemy deaths cross-firing via OwnerOnly carrier logic
                row: Definitions.RowType.Vanguard, oncePerRound: true);
            var cryptEnch = CreateEnchantment("Enchantment_Crypt", new[]
            {
                Trigger("Combat.EntityDiedEvent", cryptSpawnGhoul),
            });
            var crypt = CreateCard("Crypt", CardType.Building, Definitions.RowType.Building,
                minSpeed: 0, maxSpeed: 0, hp: 5, atk: 0,
                costs: new List<CardCost> { mana3 },
                enchantments: new List<EnchantmentData> { cryptEnch });

            // -------- Add to fallback deck --------

            var deck = AssetDatabase.LoadAssetAtPath<DeckData>(DeckPath);
            if (deck != null)
            {
                if (deck.Cards == null) deck.Cards = new List<CardDef>();
                void AddIfMissing(CardDef c) { if (c != null && !deck.Cards.Contains(c)) deck.Cards.Add(c); }
                AddIfMissing(ghoul);
                AddIfMissing(bloodWitch);
                AddIfMissing(nightFury);
                AddIfMissing(vampireLoner);
                AddIfMissing(freshSpawn);
                AddIfMissing(ritualist);
                AddIfMissing(decoy);
                AddIfMissing(gourmet);
                AddIfMissing(bloodAltar);
                AddIfMissing(crypt);
                EditorUtility.SetDirty(deck);
            }
            else
            {
                Debug.LogWarning($"[VampireContentGen] FallbackDeck.asset not found at {DeckPath} — skip deck update");
            }

            // Mark all generated cards as Addressable with label "Cards" (best-effort, may no-op if Addressables not initialized)
            var allGenerated = new List<CardDef> { ghoul, bloodWitch, nightFury, vampireLoner, freshSpawn, ritualist, decoy, gourmet, bloodAltar, crypt };
            MarkAddressable(allGenerated);

            // Bullet-proof path: Resources-based registry that the runtime loads on startup.
            RebuildCardRegistry();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VampireContentGen] Content pass generated. NEXT: run Tools/VampirDek11/Reset Player Save, then Play.");
        }

        [MenuItem("Tools/VampirDek11/Rebuild Card Registry (Resources)")]
        public static void RebuildCardRegistry()
        {
            EnsureDir(ResourcesDir);
            var registry = AssetDatabase.LoadAssetAtPath<CardRegistry>(RegistryPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<CardRegistry>();
                AssetDatabase.CreateAsset(registry, RegistryPath);
            }
            registry.Cards = new List<CardDef>();
            var allCardGuids = AssetDatabase.FindAssets("t:CardDef");
            foreach (var g in allCardGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<CardDef>(path);
                if (c != null && !registry.Cards.Contains(c)) registry.Cards.Add(c);
            }
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssetIfDirty(registry);
            Debug.Log($"[VampireContentGen] CardRegistry rebuilt with {registry.Cards.Count} CardDef references (all project CardDefs).");
        }

        [MenuItem("Tools/VampirDek11/Reset Player Save")]
        public static void ResetPlayerSave()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "playerdata.json");
            if (File.Exists(path)) { File.Delete(path); Debug.Log($"[VampireContentGen] Deleted {path}"); }
            string activeBattle = System.IO.Path.Combine(Application.persistentDataPath, "active_battle.json");
            if (File.Exists(activeBattle)) { File.Delete(activeBattle); Debug.Log($"[VampireContentGen] Deleted {activeBattle}"); }
            // Also dump every file in persistentDataPath for safety
            if (Directory.Exists(Application.persistentDataPath))
                foreach (var f in Directory.GetFiles(Application.persistentDataPath))
                    Debug.Log($"[VampireContentGen] Remaining save file: {f}");
            Debug.Log("[VampireContentGen] Player save reset. Next launch will re-seed deck from default.");
        }

        [MenuItem("Tools/VampirDek11/Force Re-Mark Cards Addressable")]
        public static void ForceReMarkAddressable()
        {
            var allCardGuids = AssetDatabase.FindAssets("t:CardDef", new[] { CardsDir });
            var cards = new List<CardDef>();
            foreach (var g in allCardGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var c = AssetDatabase.LoadAssetAtPath<CardDef>(path);
                if (c != null) cards.Add(c);
            }
            MarkAddressable(cards);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void MarkAddressable(List<CardDef> cards)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError("[VampireContentGen] Addressables not initialized. Open Window/Asset Management/Addressables/Groups (click 'Create Addressables Settings' button), then re-run this menu.");
                return;
            }
            var group = settings.DefaultGroup;
            if (group == null) { Debug.LogError("[VampireContentGen] No default Addressables group. Open Addressables Groups window and create one."); return; }

            settings.AddLabel("Cards", false);
            int marked = 0;
            foreach (var card in cards)
            {
                if (card == null) continue;
                var path = AssetDatabase.GetAssetPath(card);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogWarning($"[VampireContentGen] Card '{card.name}' has no asset path. Skipping.");
                    continue;
                }
                var guid = AssetDatabase.AssetPathToGUID(path);
                var entry = settings.CreateOrMoveEntry(guid, group, false, false);
                if (entry == null)
                {
                    Debug.LogError($"[VampireContentGen] CreateOrMoveEntry returned null for {card.CardName} at {path}");
                    continue;
                }
                entry.address = card.CardName;
                entry.SetLabel("Cards", true, true, false);
                Debug.Log($"[VampireContentGen]  - registered '{card.CardName}' → group '{group.Name}' (path: {path})");
                marked++;
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(group);
            Debug.Log($"[VampireContentGen] Marked {marked}/{cards.Count} cards as Addressable (label 'Cards'). Settings saved.");
        }

        // ---------- helpers ----------

        private static void EnsureDir(string dir)
        {
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static T LoadByGuid<T>(string guid) where T : Object
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static T FindFirst<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static T FindOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var inst = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(inst, path);
            return inst;
        }

        private static HumanResourceCost CreateHumanResourceCost(string name, int amount, HumanResourcePayActionDefinition pay)
        {
            var path = $"{CostsDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HumanResourceCost>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HumanResourceCost>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var so = new SerializedObject(asset);
            so.FindProperty("_amount").intValue = amount;
            so.FindProperty("_payAction").objectReferenceValue = pay;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ManaCost CreateManaCost(string name, int amount, ManaPayActionDefinition pay)
        {
            var path = $"{CostsDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ManaCost>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ManaCost>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var so = new SerializedObject(asset);
            so.FindProperty("_amount").intValue = amount;
            so.FindProperty("_payAction").objectReferenceValue = pay;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static RecountSiblingsAttackActionDefinition CreateRecountSiblingsAction(string name, string siblingName, int baseAttack, int bonusPerSibling)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<RecountSiblingsAttackActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<RecountSiblingsAttackActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_siblingCardName").stringValue = siblingName;
            so.FindProperty("_baseAttack").intValue = baseAttack;
            so.FindProperty("_bonusPerSibling").intValue = bonusPerSibling;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static DealDamageAutoTargetActionDefinition CreateAutoDamageAction(string name, int amount, bool preferHighest)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<DealDamageAutoTargetActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<DealDamageAutoTargetActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_amount").intValue = amount;
            so.FindProperty("_preferHighestHealth").boolValue = preferHighest;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static DealDamageToRandomEnemyActionDefinition CreateRandomDamageAction(string name, int amount, bool includeTown)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<DealDamageToRandomEnemyActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<DealDamageToRandomEnemyActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_amount").intValue = amount;
            so.FindProperty("_includeTown").boolValue = includeTown;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static ModifyOwnerAttackActionDefinition CreateModifyAttackAction(string name, int delta)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<ModifyOwnerAttackActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<ModifyOwnerAttackActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_delta").intValue = delta;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static GenerateHumanResourceActionDefinition CreateGenerateHrAction(string name, int amount)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<GenerateHumanResourceActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<GenerateHumanResourceActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_amount").intValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static SpawnCardActionDefinition CreateSpawnAction(string name, CardDef cardToSpawn, Definitions.RowType row, SpawnSlotRule rule, bool enforceRow)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<SpawnCardActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<SpawnCardActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_cardToSpawn").objectReferenceValue = cardToSpawn;
            so.FindProperty("_targetRow").enumValueIndex = (int)row;
            so.FindProperty("_slotRule").enumValueIndex = (int)rule;
            so.FindProperty("_onlyIfTargetRowMatchesCard").boolValue = enforceRow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private static SpawnOnFriendlyDeathActionDefinition CreateSpawnOnFriendlyDeathAction(string name, CardDef cardToSpawn, string filterName, Definitions.RowType row, bool oncePerRound)
        {
            var path = $"{EnchDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<SpawnOnFriendlyDeathActionDefinition>(path);
            if (a == null) { a = ScriptableObject.CreateInstance<SpawnOnFriendlyDeathActionDefinition>(); AssetDatabase.CreateAsset(a, path); }
            var so = new SerializedObject(a);
            so.FindProperty("_diedCardNameFilter").stringValue = filterName ?? "";
            so.FindProperty("_cardToSpawn").objectReferenceValue = cardToSpawn;
            so.FindProperty("_targetRow").enumValueIndex = (int)row;
            so.FindProperty("_oncePerRound").boolValue = oncePerRound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return a;
        }

        private struct TriggerSpec
        {
            public string EventType;
            public string PhaseFilter;
            public bool OwnerOnly;
            public ActionDefinition Action;
        }

        private static TriggerSpec Trigger(string eventType, ActionDefinition action, string phaseFilter = null, bool ownerOnly = false)
            => new TriggerSpec { EventType = eventType, PhaseFilter = phaseFilter ?? "", OwnerOnly = ownerOnly, Action = action };

        private static EnchantmentData CreateEnchantment(string name, TriggerSpec[] triggers, ModifierEntry[] modifiers = null)
        {
            var path = $"{EnchDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnchantmentData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnchantmentData>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.DisplayName = name;
            asset.Duration = -1;
            asset.Triggers = new List<TriggerEntry>();
            foreach (var t in triggers)
            {
                asset.Triggers.Add(new TriggerEntry
                {
                    EventType = t.EventType,
                    PhaseFilter = t.PhaseFilter ?? "",
                    OwnerOnly = t.OwnerOnly,
                    Actions = new List<ActionDefinition> { t.Action }
                });
            }
            asset.Modifiers = modifiers != null ? new List<ModifierEntry>(modifiers) : new List<ModifierEntry>();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static CardDef CreateCard(string name, CardType type, Definitions.RowType row,
            int minSpeed, int maxSpeed, int hp, int atk,
            List<CardCost> costs, List<EnchantmentData> enchantments)
        {
            var path = $"{CardsDir}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<CardDef>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CardDef>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.CardName = name;
            asset.Type = type;
            asset.RowType = row;
            asset.MinSpeed = minSpeed;
            asset.MaxSpeed = maxSpeed;
            asset.Health = hp;
            asset.Attack = atk;
            asset.Costs = costs;
            asset.Effects = new List<EffectActionDefinition>();
            asset.InnateEnchantments = enchantments;
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
#endif
