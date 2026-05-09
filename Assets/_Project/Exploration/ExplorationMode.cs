// Assets/_Project/Exploration/ExplorationMode.cs
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Core;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Exploration
{
    public class ExplorationMode : IGameMode
    {
        private ExplorationController _player;
        private string _worldSceneAddress;
        private AsyncOperationHandle<SceneInstance> _sceneHandle;

        public ExplorationMode(string worldSceneAddress)
        {
            _worldSceneAddress = worldSceneAddress;
        }

        public async UniTask EnterAsync(object context)
        {
            _sceneHandle = Addressables.LoadSceneAsync(_worldSceneAddress, LoadSceneMode.Additive);
            await _sceneHandle.Task;

            _player = Object.FindObjectOfType<ExplorationController>();
            if (_player != null)
                _player.Activate();
        }

        public async UniTask ExitAsync()
        {
            if (_player != null)
                _player.Deactivate();

            if (_sceneHandle.IsValid())
                await Addressables.UnloadSceneAsync(_sceneHandle, true);
        }

        public async UniTask OnPauseAsync()
        {
            if (_player != null)
                _player.Deactivate();
        }

        public async UniTask OnResumeAsync()
        {
            if (_player != null)
                _player.Activate();
        }
    }
}