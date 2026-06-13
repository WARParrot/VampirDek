using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using Combat;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Exploration;
using System.Numerics;
using System.Linq;

namespace Bootstrap
{
    public class BootstrapLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private GameSettings _gameSettings;
        [SerializeField] private HintUI _hintUIPrefab;
        [SerializeField] private GameObject _escapeMenuPrefab;

        [Header("Default Deck")]
        [SerializeField] private DeckData _defaultPlayerDeck;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);

            builder.Register<SceneRegistry>(Lifetime.Singleton);
            builder.Register<IGameStateService, GameStateService>(Lifetime.Singleton);

            builder.Register<ProgressionManager>(Lifetime.Singleton);
            builder.Register<IProgressionService>(resolver => resolver.Resolve<ProgressionManager>(), Lifetime.Singleton);

            builder.Register<HintManager>(Lifetime.Singleton);
            builder.RegisterInstance(_gameSettings);
        }

        private async void Start()
        {
            Debug.Log("[Bootstrap] Start called.");

            await UniTask.WaitUntil(() => GlobalServices.Resolver != null);
            Debug.Log("[Bootstrap] DI container ready.");

            var stateService = Container.Resolve<IGameStateService>();
            GlobalServices.GameStateService = stateService;
            Debug.Log("[Bootstrap] GameStateService instantiated.");

            var progression = Container.Resolve<ProgressionManager>();
            GlobalServices.Progression = progression;
            Debug.Log("[Bootstrap] ProgressionManager initialized.");
            
            await stateService.LoadAsync();

            // Start independent Addressables loads together, then register them in dependency order below.
            // This preserves CardDatabase-before-DeckDatabase semantics while avoiding purely sequential IO waits.
            var cardHandle = Addressables.LoadAssetsAsync<CardDef>("Cards", null);
            var enchHandle = Addressables.LoadAssetsAsync<EnchantmentData>("Enchantments", null);
            var deckHandle = Addressables.LoadAssetsAsync<DeckData>("Decks", null);
            var hintHandle = Addressables.LoadAssetsAsync<HintData>("Hints", null);
            var encHandle = Addressables.LoadAssetsAsync<CombatEncounter>("Encounters", null);
            var layoutHandle = Addressables.LoadAssetsAsync<BoardLayoutData>("Layouts", null);
            var graphHandle = Addressables.LoadAssetsAsync<PhaseGraph>("PhaseGraphs", null);
            var winHandle = Addressables.LoadAssetsAsync<WinCondition>("WinConditions", null);

            await cardHandle.Task;
            if (cardHandle.Result != null)
            {
                foreach (var card in cardHandle.Result)
                    CardDatabase.RegisterCard(card);
            }

            // Load base game enchantments
            await enchHandle.Task;
            if (enchHandle.Result != null)
            {
                foreach (var ench in enchHandle.Result)
                    EnchantmentDatabase.RegisterEnchantment(ench);
            }

            // Load base game decks
            await deckHandle.Task;
            if (deckHandle.Result != null)
            {
                foreach (var deck in deckHandle.Result)
                {
                    PopulateDeckCards(deck);
                    DeckDatabase.RegisterDeck(deck);
                }
            }

            if (_defaultPlayerDeck != null)
            {
                PopulateDeckCards(_defaultPlayerDeck);
                DeckDatabase.RegisterDeck(_defaultPlayerDeck);
                Debug.Log($"[Bootstrap] Registered default deck asset '{_defaultPlayerDeck.name}'.");
            }

            // Hints
            await hintHandle.Task;
            if (hintHandle.Result != null)
                foreach (var h in hintHandle.Result) HintDatabase.RegisterHint(h);

            await encHandle.Task;
            if (encHandle.Result != null)
            {
                foreach (var enc in encHandle.Result)
                    EncounterDatabase.RegisterEncounter(enc);
            }

            await layoutHandle.Task;
            if (layoutHandle.Result != null)
            {
                foreach (var layout in layoutHandle.Result)
                    BoardLayoutDatabase.RegisterLayout(layout);
            }

            await graphHandle.Task;
            if (graphHandle.Result != null)
            {
                foreach (var graph in graphHandle.Result)
                {
                    // Resolve internal node references
                    foreach (var node in graph.Nodes)
                    {
                        foreach (var trans in node.Transitions)
                            trans.Target = graph.Nodes.Find(n => n.PhaseId == trans.Target?.PhaseId);
                    }
                    PhaseGraphDatabase.RegisterPhaseGraph(graph);
                }
            }

            await winHandle.Task;
            if (winHandle.Result != null)
            {
                foreach (var cond in winHandle.Result)
                    WinConditionDatabase.RegisterWinCondition(cond);
            }

            var saveSystem = Container.Resolve<ISaveSystem>();
            var playerData = await LoadPlayerDataAsync(saveSystem) ?? new PersistentPlayerData();
            if (playerData.ActiveDeckCardIds == null || playerData.ActiveDeckCardIds.Count == 0)
            {
                var defaultDeckCardIds = ResolveDeckCardIds(_defaultPlayerDeck);
                if (defaultDeckCardIds.Count > 0)
                {
                    playerData.ActiveDeckCardIds = defaultDeckCardIds;
                    Debug.Log($"[Bootstrap] Assigned default deck from '{_defaultPlayerDeck.name}'.");
                }
                else
                {
                    playerData.ActiveDeckCardIds = new List<string>
                    {
                        "Town",
                        "Human",
                        "Human",
                        "Building",
                        "Vampire"
                    };
                    Debug.LogWarning("[Bootstrap] No default deck asset found - using hardcoded fallback with only town.");
                }

                var json = JsonUtility.ToJson(playerData);
                await saveSystem.SaveAsync("playerdata.json", System.Text.Encoding.UTF8.GetBytes(json));
            }
            GlobalServices.PlayerData = playerData;
            Debug.Log("[Bootstrap] SaveSystem instantiated.");

            var hintUIInstance = Instantiate(_hintUIPrefab);
            DontDestroyOnLoad(hintUIInstance.gameObject);
            Container.Inject(hintUIInstance);
            Debug.Log("[Bootstrap] HintUI instantiated.");

            var escapeMenuInstance = Instantiate(_escapeMenuPrefab);
            DontDestroyOnLoad(escapeMenuInstance);
            Debug.Log("[Bootstrap] EscapeMenu instantiated.");

            var hintManager = Container.Resolve<HintManager>();
            hintManager.SetHintUI(hintUIInstance);
            hintManager.Initialize();
            Debug.Log("[Bootstrap] HintManager initialized.");

            var sceneRegistry = Container.Resolve<SceneRegistry>();
            Debug.Log("[Bootstrap] SceneRegistry initialized.");

            Debug.Log("[Bootstrap] Loading base game scenes...");
            var baseHandle = Addressables.LoadAssetsAsync<WorldSceneInfo>("BaseScenes", null);
            await baseHandle.Task;
            foreach (var world in baseHandle.Result)
                sceneRegistry.WorldScenes.Add(world);

            var baseDuelHandle = Addressables.LoadAssetsAsync<DuelSceneInfo>("BaseScenes", null);
            await baseDuelHandle.Task;
            foreach (var duel in baseDuelHandle.Result)
                sceneRegistry.DuelScenes.Add(duel);
            Debug.Log("[Bootstrap] Base game scenes loaded.");

            var modManager = Container.Resolve<ModManager>();
            Debug.Log("[Bootstrap] Loading mods...");
            var modDirs = await modManager.LoadModsAsync();
            Debug.Log($"[Bootstrap] Mods loaded: {modDirs.Count} directories.");

            var dataLoader = new ModDataLoader();
            await dataLoader.LoadAllDataAsync(modDirs);
            Debug.Log("[Bootstrap] Mod data loader initialized...");

            var importer = new ModContentImporter();
            foreach (var dir in modDirs)
            {
                await importer.ImportModAsync(dir);
                Debug.Log($"[Bootstrap] Mod imported: {dir}");
            }

            Debug.Log("[Bootstrap] Loading mod scenes...");
            foreach (var modInfo in modManager.LoadedMods)
            {
                if (modInfo.scenes != null)
                {
                    foreach (var entry in modInfo.scenes)
                    {
                        if (entry.type == "world")
                        {
                            var info = ScriptableObject.CreateInstance<WorldSceneInfo>();
                            info.SceneId = entry.id;
                            info.AddressableKey = entry.addressKey;
                            info.DisplayName = entry.displayName;
                            sceneRegistry.WorldScenes.Add(info);
                        }
                        else if (entry.type == "duel")
                        {
                            var info = ScriptableObject.CreateInstance<DuelSceneInfo>();
                            info.SceneId = entry.id;
                            info.AddressableKey = entry.addressKey;
                            info.DisplayName = entry.displayName;
                            sceneRegistry.DuelScenes.Add(info);
                        }
                    }
                }
            }

            Debug.Log("[Bootstrap] Pushing ExplorationMode...");
            var state = stateService.State;
            if (state.CurrentWorldSceneAddress == null)
            {
                var defaultExplorationMode = new Exploration.ExplorationMode("DefaultWorld");
                await GlobalServices.Director.PushModeAsync(defaultExplorationMode);
                Debug.Log("[Bootstrap] ExplorationMode pushed saveless.");
                return;
            }

            var explorationMode = new Exploration.ExplorationMode(state.CurrentWorldSceneAddress);
            await GlobalServices.Director.PushModeAsync(explorationMode);
            Debug.Log("[Bootstrap] ExplorationMode pushed from save.");

            var player = Object.FindObjectOfType<ExplorationController>();
            if (player != null)
            {
                player.SetPosition(state.PlayerPosition, state.PlayerRotation);
            }
            Debug.Log("[Bootstrap] Set player position from save.");

            if (!string.IsNullOrEmpty(state.ActiveDuelTableId))
            {
                var points = Object.FindObjectsOfType<EncounterPoint>();
                var point = points.FirstOrDefault(p => p.UniqueTableId == state.ActiveDuelTableId);
                if (point != null)
                {
                    Debug.Log($"[Bootstrap] Resuming duel at table {state.ActiveDuelTableId}");
                    await point.StartDuelAsync(true);
                }
                else
                {
                    Debug.LogWarning($"[Bootstrap] Could not find EncounterPoint with ID {state.ActiveDuelTableId}");
                }
            }
        }

        private static void PopulateDeckCards(DeckData deck)
        {
            if (deck == null) return;

            deck.Cards ??= new List<CardDef>();
            deck.Cards.Clear();

            if (deck.CardNames == null) return;
            foreach (var name in deck.CardNames)
            {
                var def = CardDatabase.GetCard(name);
                if (def != null) deck.Cards.Add(def);
            }
        }

        private static List<string> ResolveDeckCardIds(DeckData deck)
        {
            if (deck == null) return new List<string>();

            PopulateDeckCards(deck);
            if (deck.Cards != null && deck.Cards.Count > 0)
            {
                return deck.Cards
                    .Where(c => c != null && !string.IsNullOrEmpty(c.CardName))
                    .Select(c => c.CardName)
                    .ToList();
            }

            return deck.CardNames?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList() ?? new List<string>();
        }

        private async UniTask<PersistentPlayerData> LoadPlayerDataAsync(ISaveSystem saveSystem)
        {
            var bytes = await saveSystem.LoadAsync("playerdata.json");
            if (bytes != null)
            {
                string json = System.Text.Encoding.UTF8.GetString(bytes);
                return JsonUtility.FromJson<PersistentPlayerData>(json);
            }
            return new PersistentPlayerData
            {
                ActiveDeckCardIds = new List<string> { }
            };
        }
    }
}