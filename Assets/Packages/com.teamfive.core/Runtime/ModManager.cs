using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Core
{
    public class ModManager
    {
        public List<ModInfo> LoadedMods { get; private set; } = new();
        private readonly string _modsPath;

        public ModManager()
        {
            _modsPath = Path.Combine(Application.dataPath, "../Mods");
        }

        public async UniTask<List<string>> LoadModsAsync()
        {
            var loadedDirs = new List<string>();

            if (!Directory.Exists(_modsPath))
            {
                Debug.Log("[ModManager] No Mods folder found.");
                return loadedDirs;
            }

            foreach (var modDir in Directory.GetDirectories(_modsPath))
            {
                string modInfoPath = Path.Combine(modDir, "modinfo.json");
                if (!File.Exists(modInfoPath)) continue;

                string json = await File.ReadAllTextAsync(modInfoPath);
                var modInfo = JsonUtility.FromJson<ModInfo>(json);
                if (modInfo == null)
                {
                    Debug.LogWarning($"[ModManager] Invalid modinfo.json in {modDir}");
                    continue;
                }

                string catalogPath = Path.Combine(modDir, modInfo.CatalogPath);
                if (!File.Exists(catalogPath))
                {
                    Debug.LogWarning($"[ModManager] Catalog not found: {catalogPath}");
                    continue;
                }

                await Addressables.LoadContentCatalogAsync(catalogPath).Task;
                Debug.Log($"[ModManager] Loaded catalog: {modInfo.Name}");

                LoadedMods.Add(modInfo);
                loadedDirs.Add(modDir);
            }

            return loadedDirs;
        }
    }
}