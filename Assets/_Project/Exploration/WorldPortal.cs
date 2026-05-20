using Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

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
            _progression = GlobalServices.Resolver?.Resolve<IProgressionService>();
        }

        public async void Interact(ExplorationController player)
        {
            if (_progression == null || _targetWorld == null) return;

            if (!_progression.CanAccessWorld(_targetWorld))
            {
                Debug.Log("Cannot access yet.");
                return;
            }

            var stateService = GlobalServices.Resolver.Resolve<IGameStateService>();
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