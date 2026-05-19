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
        }

        private async void Start()
        {
            Debug.Log("[Bootstrap] Start called.");

            await UniTask.WaitUntil(() => GlobalServices.Resolver != null);
            Debug.Log("[Bootstrap] DI container ready.");

            var hintUIInstance = Instantiate(_hintUIPrefab);
            DontDestroyOnLoad(hintUIInstance.gameObject);
            Container.Inject(hintUIInstance);
            Debug.Log("[Bootstrap] HintUI instantiated.");

            var hintManager = Container.Resolve<HintManager>();
            hintManager.SetHintUI(hintUIInstance);
            hintManager.Initialize();
            Debug.Log("[Bootstrap] HintManager initialized.");

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
    }
}