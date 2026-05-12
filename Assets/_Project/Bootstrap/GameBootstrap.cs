using Core;
using Combat;
using Cysharp.Threading.Tasks;
using Exploration;
using UnityEngine;

namespace Bootstrap
{
    public class GameBootstrap : MonoBehaviour
    {
        async void Start()
        {
            await UniTask.WaitUntil(() => GlobalServices.Resolver != null);

            var modManager = (ModManager) GlobalServices.Resolver.Resolve(typeof(ModManager));
            var modDirs = await modManager.LoadModsAsync();

            var importer = new ModContentImporter();
            foreach (var dir in modDirs)
            {
                await importer.ImportModAsync(dir);
            }

            Debug.Log($"[GameBootstrap] Loaded {modDirs.Count} mod(s). Starting exploration...");

            var explorationMode = new ExplorationMode("TestWorld");
            await GlobalServices.Director.PushModeAsync(explorationMode);
        }
    }
}