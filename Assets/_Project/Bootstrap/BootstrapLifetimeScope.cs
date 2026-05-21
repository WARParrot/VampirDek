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


namespace Bootstrap
{
    public class BootstrapLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private GameSettings _gameSettings;
        [SerializeField] private HintUI _hintUIPrefab;

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

            var saveSystem = Container.Resolve<ISaveSystem>();
            var playerData = await LoadPlayerDataAsync(saveSystem);
            GlobalServices.PlayerData = playerData;
            Debug.Log("[Bootstrap] SaveSystem instantiated.");

            var hintUIInstance = Instantiate(_hintUIPrefab);
            DontDestroyOnLoad(hintUIInstance.gameObject);
            Container.Inject(hintUIInstance);
            Debug.Log("[Bootstrap] HintUI instantiated.");

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
                player.SetPosition(state.PlayerPosition + UnityEngine.Vector3.up, state.PlayerRotation);
            }
            Debug.Log("[Bootstrap] Set player position from save.");
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