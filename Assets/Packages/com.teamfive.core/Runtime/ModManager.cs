using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Core
{
    public class ModManager
    {
        private readonly SceneRegistry _sceneRegistry;
        private readonly string _modsPath;

        public ModManager(SceneRegistry sceneRegistry)
        {
            _sceneRegistry = sceneRegistry;
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

                if (modInfo.Scenes != null)
                {
                    foreach (var entry in modInfo.Scenes)
                    {
                        switch (entry.Type)
                        {
                            case "world":
                                _sceneRegistry.WorldScenes.Add(new WorldSceneInfo
                                {
                                    SceneId = entry.Id,
                                    AddressableKey = entry.AddressKey,
                                    DisplayName = entry.DisplayName
                                });
                                break;
                            case "duel":
                                _sceneRegistry.DuelScenes.Add(new DuelSceneInfo
                                {
                                    SceneId = entry.Id,
                                    AddressableKey = entry.AddressKey,
                                    DisplayName = entry.DisplayName
                                });
                                break;
                        }
                    }
                }

                loadedDirs.Add(modDir);
            }

            return loadedDirs;
        }
    }
}