using System.Collections.Generic;
using System.Linq;
using Core;
using Combat;
using Definitions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using Shared.Localization;

namespace Exploration
{
    public class WorldPortal : MonoBehaviour, IInteractable
    {
        [SerializeField] private WorldSceneInfo _targetWorld;
        [SerializeField] private Transform _spawnPoint;

        public string PromptText
        {
            get
            {
                if (EndlessReplayLoop.IsAwaitingNextNightPortal(GlobalServices.GameStateService?.State))
                    return LocalizationService.T("interaction.next_night", "Enter the next night");
                return LocalizationService.TFormat("interaction.enter_world", "Enter {0}", LocalizedWorldName());
            }
        }

        private IProgressionService _progression;

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

            if (EndlessReplayLoop.IsAwaitingNextNightPortal(stateService.State))
            {
                await EnterNextNightAsync(player, stateService);
                return;
            }

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

        private async UniTask EnterNextNightAsync(ExplorationController player, IGameStateService stateService)
        {
            var choices = SelectDeckExpansionChoices(stateService.State);
            if (choices.Count > 0)
            {
                var chosen = await DeckExpansionSelectionUI.ShowAsync(choices);
                await EndlessReplayLoop.AddDeckExpansionCardAsync(chosen);
                Debug.Log($"[WorldPortal] Added next-night deck card: {chosen?.CardName ?? "none"}.");
            }
            else
            {
                Debug.LogWarning("[WorldPortal] No deck-expansion card choices available for next-night transition.");
            }

            var spawnPosition = _spawnPoint != null ? _spawnPoint.position : player.transform.position;
            var spawnRotation = _spawnPoint != null ? _spawnPoint.rotation : player.transform.rotation;
            await EndlessReplayLoop.AdvanceToNextRunAsync(_targetWorld.AddressableKey, spawnPosition, spawnRotation);
        }

        private static List<CardDef> SelectDeckExpansionChoices(PersistentGameState state)
        {
            var allCards = CardDatabase.AllCards
                .Where(card => card != null && !string.IsNullOrEmpty(card.CardName) && card.Type != CardType.Town)
                .ToList();

            if (allCards.Count <= 3) return allCards;

            var rng = new System.Random(EndlessReplayLoop.GetStableSeed($"next-night-reward:{state?.ReplayRunNumber ?? 1}"));
            return allCards
                .OrderBy(_ => rng.Next())
                .Take(3)
                .ToList();
        }

    }
}