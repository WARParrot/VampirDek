using Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Definitions;

namespace Exploration
{
    public class WorldPortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private WorldSceneInfo _targetWorld;
        [SerializeField] private Transform _spawnPoint;

        public string PromptText => "Enter " + _targetWorld?.DisplayName;

        private IProgressionService _progression;

        private void Start()
        {
            _progression = GlobalServices.Progression;
        }

        public async void Interact(ExplorationController player)
        {
            if (_targetWorld == null) return;

            var progression = GlobalServices.Progression;
            var stateService = GlobalServices.GameStateService;

            Debug.Log($"[WorldPortal] progression: {progression == null}; stateService: {stateService == null}.");

            if (progression == null || stateService == null) return;

            if (!progression.CanAccessWorld(_targetWorld.SceneId))
            {
                Debug.Log("Cannot access yet.");
                return;
            }

            Debug.Log("[WorldPortal] accessing world.");

            var state = stateService.State;
            state.PlayerPosition = _spawnPoint != null ? _spawnPoint.position : player.transform.position;
            state.PlayerRotation = _spawnPoint != null ? _spawnPoint.rotation : player.transform.rotation;
            state.CurrentWorldSceneAddress = _targetWorld.AddressableKey;
            await stateService.SaveAsync();

            await GlobalServices.Director.PopModeAsync();
            var newExploration = new ExplorationMode(_targetWorld.AddressableKey);
            await GlobalServices.Director.PushModeAsync(newExploration);
        }
    }
}