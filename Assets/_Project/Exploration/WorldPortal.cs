using Core;
using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;
using Shared.Localization;

namespace Exploration
{
    public class WorldPortal : MonoBehaviour, IInteractable
    {
        private static bool _nextNightTransitionInProgress;

        [SerializeField] private WorldSceneInfo _targetWorld;
        [SerializeField] private Transform _spawnPoint;

        public string PromptText
        {
            get
            {
                var state = GlobalServices.GameStateService?.State;
                if (EndlessReplayLoop.IsAwaitingNextNightPortal(state))
                    return LocalizationService.T("interaction.next_night", "Enter the next night");
                if (ShouldBlockOrdinaryPortalTravel(state))
                    return LocalizationService.T("interaction.win_duel_to_enter", "Win the duel to enter.");
                return LocalizationService.TFormat("interaction.enter_world", "Enter {0}", LocalizedWorldName());
            }
        }

        private static bool ShouldBlockOrdinaryPortalTravel(PersistentGameState state)
        {
            // In endless replay mode, WorldPortal is a loop-control affordance only:
            // it advances the next night when explicitly armed, otherwise it teaches
            // the player to win the duel instead of allowing ordinary scene travel.
            return state == null || state.EndlessReplayEnabled || state.BlockWorldPortalTravelTriggers;
        }

        private string LocalizedWorldName()
        {
            if (_targetWorld == null) return string.Empty;
            var fallback = string.IsNullOrWhiteSpace(_targetWorld.DisplayName) ? _targetWorld.name : _targetWorld.DisplayName;
            var key = LocalizationService.FirstNonEmpty(
                LocalizationService.KeyFromName("world", _targetWorld.SceneId, "name"),
                LocalizationService.KeyFromName("world", _targetWorld.name, "name"),
                LocalizationService.KeyFromName("world", fallback, "name"));
            return LocalizationService.T(key, fallback);
        }

        public async void Interact(ExplorationController player)
        {
            var stateService = GlobalServices.GameStateService;
            var state = stateService?.State;
            if (EndlessReplayLoop.IsAwaitingNextNightPortal(state))
            {
                if (_nextNightTransitionInProgress)
                {
                    Debug.Log("[WorldPortal] Next-night transition is already in progress; ignoring duplicate portal trigger.");
                    return;
                }

                _nextNightTransitionInProgress = true;
                try
                {
                    await EnterNextNightAsync();
                }
                finally
                {
                    _nextNightTransitionInProgress = false;
                }
                return;
            }

            if (ShouldBlockOrdinaryPortalTravel(state))
            {
                Debug.Log("[WorldPortal] Ordinary portal travel is blocked by the replay loop; win the duel to enter the next night.");
                return;
            }

            if (_targetWorld == null) return;

            var progression = GlobalServices.Progression;
            Debug.Log($"[WorldPortal] progression: {progression == null}; stateService: {stateService == null}.");

            if (progression == null || stateService == null) return;

            if (!progression.CanAccessWorld(_targetWorld.SceneId))
            {
                Debug.Log("Cannot access yet.");
                return;
            }

            Debug.Log("[WorldPortal] accessing world.");

            state = stateService.State;
            state.PlayerPosition = _spawnPoint != null ? _spawnPoint.position : player.transform.position;
            state.PlayerRotation = _spawnPoint != null ? _spawnPoint.rotation : player.transform.rotation;
            state.CurrentWorldSceneAddress = _targetWorld.AddressableKey;
            await stateService.SaveAsync();

            await GlobalServices.Director.PopModeAsync();
            var newExploration = new ExplorationMode(_targetWorld.AddressableKey);
            await GlobalServices.Director.PushModeAsync(newExploration);
        }

        private async UniTask EnterNextNightAsync()
        {
            // Next-night is a replay reset, not an ordinary portal transfer. Keep the player in
            // the current world and reload it at the default spawn so cleared duel tables can
            // reinitialize from the reset CompletedEncounterIds.
            await EndlessReplayLoop.AdvanceToNextRunAsync(null, Vector3.zero, Quaternion.identity);
        }
    }
}
