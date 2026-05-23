using System;
using System.Linq;
using Core;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Bootstrap
{
    public class ProgressionManager : IProgressionService, IDisposable
    {
        private readonly IGameStateService _stateService;
        private readonly SceneRegistry _sceneRegistry;

        public ProgressionManager(IGameStateService stateService, SceneRegistry sceneRegistry)
        {
            _stateService = stateService;
            _sceneRegistry = sceneRegistry;
            GlobalServices.EventBus.Subscribe<DuelResultEvent>(OnDuelResult);
        }

        public bool CanAccessWorld(string sceneId)
        {
            var world = _sceneRegistry.WorldScenes.Find(w => w.SceneId == sceneId);
            if (world == null) return true;

            if (world.RequiredFlags == null || world.RequiredFlags.Count == 0)
                return true;

            var state = _stateService.State;
            return world.RequiredFlags.All(f => state.Flags.TryGetValue(f, out bool val) && val);
        }

        private async void OnDuelResult(DuelResultEvent e)
        {
            var state = _stateService.State;

            if (!string.IsNullOrEmpty(e.EncounterId))
                state.CompletedEncounterIds.Add(e.EncounterId);

            if (e.PlayerWon && !string.IsNullOrEmpty(e.WinFlag))
                state.Flags[e.WinFlag] = true;
            else if (!e.PlayerWon && !string.IsNullOrEmpty(e.LoseFlag))
                state.Flags[e.LoseFlag] = true;

            if (!string.IsNullOrEmpty(state.ActiveDuelTableId))
                state.ActiveDuelTableId = null;

            await _stateService.SaveAsync();
        }

        public void Dispose()
        {
            GlobalServices.EventBus.Unsubscribe<DuelResultEvent>(OnDuelResult);
        }
    }
}