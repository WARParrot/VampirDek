using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using Combat;
using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace Bootstrap
{
    public class BootstrapLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private GameSettings _gameSettings;
        [SerializeField] private HintUI _hintUIPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            base.Configure(builder);
            builder.Register<HintManager>(Lifetime.Singleton);
            builder.RegisterInstance(_gameSettings);

            builder.Register<IGameStateService, GameStateService>(Lifetime.Singleton);
            builder.Register<ProgressionManager>(Lifetime.Singleton);
        }

        private async void Start()
        {
            Debug.Log("[Bootstrap] Start called.");

            await UniTask.WaitUntil(() => GlobalServices.Resolver != null);
            Debug.Log("[Bootstrap] DI container ready.");

            var stateService = Container.Resolve<IGameStateService>();
            await stateService.LoadAsync();
            Debug.Log("[Bootstrap] GameStateService instantiated.");

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

            var progression = Container.Resolve<ProgressionManager>();
            Debug.Log("[Bootstrap] ProgressionManager initialized.");

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

            Debug.Log("[Bootstrap] Pushing ExplorationMode...");
            var explorationMode = new Exploration.ExplorationMode("TestWorld");
            await GlobalServices.Director.PushModeAsync(explorationMode);
            Debug.Log("[Bootstrap] ExplorationMode pushed.");
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