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

        public string CurrentWorldAddress => _worldSceneAddress;

        public sealed class ExplorationModeContext
        {
            public bool EnableModdedDuelTables;
            public int ModdedDuelTableLimit = int.MaxValue;
        }

        public ExplorationMode(string worldSceneAddress)
        {
            _worldSceneAddress = worldSceneAddress;
        }

        public async UniTask EnterAsync(object context)
        {
            Debug.Log($"[ExplorationMode] Loading scene: {_worldSceneAddress}");
            _sceneHandle = Addressables.LoadSceneAsync(_worldSceneAddress, LoadSceneMode.Additive);
            await _sceneHandle.Task;
            Debug.Log($"[ExplorationMode] Scene loaded successfully.");

            if (context is ExplorationModeContext modeContext && modeContext.EnableModdedDuelTables)
                ModProofEncounterBootstrap.EnsureEncounterPoints(_worldSceneAddress, _sceneHandle.Result.Scene, modeContext.ModdedDuelTableLimit);

            _player = Object.FindAnyObjectByType<ExplorationController>();
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

        public UniTask OnPauseAsync()
        {
            SceneTransitionManager.Instance.SaveCameraState();

            if (_player != null)
            {
                var state = GlobalServices.GameStateService?.State;
                if (state != null)
                {
                    state.PlayerPosition = _player.transform.position;
                    state.PlayerRotation = _player.transform.rotation;
                    state.CurrentWorldSceneAddress = _worldSceneAddress;
                }
                _player.Deactivate();
            }
            return UniTask.CompletedTask;
        }

        public async UniTask OnResumeAsync()
        {
            await SceneTransitionManager.Instance.RestoreCameraAsync(1.0f);

            if (_player != null)
                _player.Activate();
        }
    }
}