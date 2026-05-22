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

            var cardHandle = Addressables.LoadAssetsAsync<CardDef>("Cards", null);
            await cardHandle.Task;
            if (cardHandle.Result != null)
            {
                foreach (var card in cardHandle.Result)
                    CardDatabase.RegisterCard(card);
            }

            // Load base game enchantments
            var enchHandle = Addressables.LoadAssetsAsync<EnchantmentData>("Enchantments", null);
            await enchHandle.Task;
            if (enchHandle.Result != null)
            {
                foreach (var ench in enchHandle.Result)
                    EnchantmentDatabase.RegisterEnchantment(ench);
            }

            // Load base game decks
            var deckHandle = Addressables.LoadAssetsAsync<DeckData>("Decks", null);
            await deckHandle.Task;
            if (deckHandle.Result != null)
            {
                foreach (var deck in deckHandle.Result)
                {
                    deck.Cards.Clear();
                    foreach (var name in deck.CardNames)
                    {
                        var def = CardDatabase.GetCard(name);
                        if (def != null) deck.Cards.Add(def);
                    }
                    DeckDatabase.RegisterDeck(deck);
                }
            }

            // Hints
            var hintHandle = Addressables.LoadAssetsAsync<HintData>("Hints", null);
            await hintHandle.Task;
            if (hintHandle.Result != null)
                foreach (var h in hintHandle.Result) HintDatabase.RegisterHint(h);

            var encHandle = Addressables.LoadAssetsAsync<CombatEncounter>("Encounters", null);
            await encHandle.Task;
            if (encHandle.Result != null)
            {
                foreach (var enc in encHandle.Result)
                    EncounterDatabase.RegisterEncounter(enc);
            }

            var layoutHandle = Addressables.LoadAssetsAsync<BoardLayoutData>("Layouts", null);
            await layoutHandle.Task;
            if (layoutHandle.Result != null)
            {
                foreach (var layout in layoutHandle.Result)
                    BoardLayoutDatabase.RegisterLayout(layout);
            }

            var graphHandle = Addressables.LoadAssetsAsync<PhaseGraph>("PhaseGraphs", null);
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

            var winHandle = Addressables.LoadAssetsAsync<WinCondition>("WinConditions", null);
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
                if (_defaultPlayerDeck != null && _defaultPlayerDeck.Cards.Count > 0)
                {
                    playerData.ActiveDeckCardIds = _defaultPlayerDeck.Cards
                        .Where(c => c != null)
                        .Select(c => c.CardName)
                        .ToList();
                    Debug.Log($"[Bootstrap] Assigned default deck from '{_defaultPlayerDeck.name}'.");
                }
                else
                {
                    playerData.ActiveDeckCardIds = new List<string>
                    {
                        "Town"
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

            var importer = new ModContentImporter();
            foreach (var dir in modDirs)
            {
                await importer.ImportModAsync(dir);
                Debug.Log($"[Bootstrap] Mod imported: {dir}");
            }

            Debug.Log("[Bootstrap] Loading mod scenes...");
            foreach (var modInfo in modManager.LoadedMods)
            {
                if (modInfo.Scenes != null)
                {
                    foreach (var entry in modInfo.Scenes)
                    {
                        if (entry.Type == "world")
                        {
                            var info = ScriptableObject.CreateInstance<WorldSceneInfo>();
                            info.SceneId = entry.Id;
                            info.AddressableKey = entry.AddressKey;
                            info.DisplayName = entry.DisplayName;
                            sceneRegistry.WorldScenes.Add(info);
                        }
                        else if (entry.Type == "duel")
                        {
                            var info = ScriptableObject.CreateInstance<DuelSceneInfo>();
                            info.SceneId = entry.Id;
                            info.AddressableKey = entry.AddressKey;
                            info.DisplayName = entry.DisplayName;
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