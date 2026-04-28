using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace Exploration
{
    public class ExplorationMode : IGameMode
    {
        private readonly string _worldSceneAddress;
        private List<CardDef> _playerDeck;

        private ExplorationController _playerController;
        private GameObject _playerInstance;
        private AsyncOperationHandle<SceneInstance>? _sceneHandle;

        public ExplorationMode(string worldSceneAddress, List<CardDef> playerDeck)
        {
            _worldSceneAddress = worldSceneAddress;
            _playerDeck = playerDeck;
        }

        public async UniTask EnterAsync(object context)
        {
            Debug.Log("[ExplorationMode] Entering...");

            _sceneHandle = Addressables.LoadSceneAsync(_worldSceneAddress, LoadSceneMode.Additive);
            await _sceneHandle.Value;

            var spawnPoint = GameObject.FindGameObjectWithTag("PlayerSpawn");
            if (spawnPoint == null)
            {
                Debug.LogError("No PlayerSpawn found in scene!");
                return;
            }
            _playerInstance = await Addressables.InstantiateAsync("Player", spawnPoint.transform.position, Quaternion.identity);
            UnityEngine.Object.DontDestroyOnLoad(_playerInstance);

            _playerController = _playerInstance.GetComponent<ExplorationController>();
            if (_playerController == null)
                _playerController = _playerInstance.AddComponent<ExplorationController>();
            _playerController.Activate();
        }

        public async UniTask ExitAsync()
        {
            Debug.Log("[ExplorationMode] Exiting...");
            if (_playerController != null)
                _playerController.Deactivate();

            if (_sceneHandle.HasValue)
            {
                await Addressables.UnloadSceneAsync(_sceneHandle.Value);
                _sceneHandle = null;
            }

            if (_playerInstance != null)
                UnityEngine.Object.Destroy(_playerInstance);
        }

        public async UniTask OnPauseAsync()
        {
            Debug.Log("[ExplorationMode] Pausing...");
            _playerController?.Deactivate();
            GlobalServices.Resolver.Resolve<InputController>().EnableCombatMap();
        }

        public async UniTask OnResumeAsync()
        {
            Debug.Log("[ExplorationMode] Resuming...");
            GlobalServices.Resolver.Resolve<InputController>().EnableExplorationMap();
            _playerController?.Activate();
        }
    }
}
