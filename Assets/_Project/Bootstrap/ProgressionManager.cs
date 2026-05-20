using System;
using System.Linq;
using Core;
using Cysharp.Threading.Tasks;

namespace Bootstrap
{
    public class ProgressionManager : IProgressionService, IDisposable
    {
        private readonly IGameStateService _stateService;

        public ProgressionManager(IGameStateService stateService)
        {
            _stateService = stateService;
            GlobalServices.EventBus.Subscribe<DuelResultEvent>(OnDuelResult);
        }

        public bool CanAccessWorld(WorldSceneInfo world)
        {
            if (world.RequiredFlags == null || world.RequiredFlags.Count == 0) return true;
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

            await _stateService.SaveAsync();
        }

        public void Dispose()
        {
            GlobalServices.EventBus.Unsubscribe<DuelResultEvent>(OnDuelResult);
        }
    }
}