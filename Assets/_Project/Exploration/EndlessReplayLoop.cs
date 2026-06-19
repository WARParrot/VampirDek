using System;
using System.Collections.Generic;
using System.Linq;
using Core;
using Combat;
using Definitions;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Exploration
{
    /// <summary>
    /// Runtime rules that turn a finite escape-room/duel slice into an endless run loop.
    /// Current-run state is reset between nights; lifetime progress is retained only as history
    /// so it never blocks replaying encounter tables.
    /// </summary>
    public static class EndlessReplayLoop
    {
        private const string DefaultWorldAddress = "DefaultWorld";

        public static void MarkEncounterWon(PersistentGameState state, string encounterId)
        {
            if (state == null || string.IsNullOrEmpty(encounterId)) return;

            EnsureInitialized(state);

            if (!state.CompletedEncounterIds.Contains(encounterId))
                state.CompletedEncounterIds.Add(encounterId);

            if (!state.LifetimeCompletedEncounterIds.Contains(encounterId))
                state.LifetimeCompletedEncounterIds.Add(encounterId);
        }

        public static int GetStableSeed(string salt = null)
        {
            var state = GlobalServices.GameStateService?.State;
            var seed = state?.ReplayRunSeed ?? 0;
            if (seed == 0)
                seed = Environment.TickCount;

            unchecked
            {
                var hash = seed;
                if (!string.IsNullOrEmpty(salt))
                {
                    foreach (var ch in salt)
                        hash = (hash * 397) ^ ch;
                }
                return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
            }
        }

        public static bool IsAwaitingNextNightPortal(PersistentGameState state)
        {
            return state != null && state.AwaitingNextNightPortal;
        }

        public static async UniTask MarkAwaitingNextNightPortalAsync()
        {
            var stateService = GlobalServices.GameStateService;
            var state = stateService?.State;
            if (state == null) return;

            EnsureInitialized(state);
            state.EndlessReplayEnabled = true;
            state.AwaitingNextNightPortal = true;
            await stateService.SaveAsync();
        }

        public static async UniTask AddDeckExpansionCardAsync(CardDef chosen)
        {
            if (chosen == null || string.IsNullOrEmpty(chosen.CardName)) return;

            var playerData = GlobalServices.PlayerData ??= new PersistentPlayerData();
            playerData.ActiveDeckCardIds ??= new();
            playerData.OwnedCardIds ??= new();

            playerData.ActiveDeckCardIds.Add(chosen.CardName);
            if (!playerData.OwnedCardIds.Contains(chosen.CardName))
                playerData.OwnedCardIds.Add(chosen.CardName);

            var state = GlobalServices.GameStateService?.State;
            if (state != null)
            {
                EnsureInitialized(state);
                if (!state.CollectedCardIds.Contains(chosen.CardName))
                    state.CollectedCardIds.Add(chosen.CardName);
            }

            var saveSystem = GlobalServices.SaveSystem;
            if (saveSystem != null)
            {
                var json = JsonUtility.ToJson(playerData);
                await saveSystem.SaveAsync("playerdata.json", System.Text.Encoding.UTF8.GetBytes(json));
            }
        }

        public static async UniTask AdvanceToNextRunAsync(string worldAddress = null, Vector3? playerPosition = null, Quaternion? playerRotation = null)
        {
            var stateService = GlobalServices.GameStateService;
            if (stateService?.State == null)
            {
                Debug.LogWarning("[EndlessReplayLoop] Cannot advance run: GameStateService is unavailable.");
                return;
            }

            var state = stateService.State;
            EnsureInitialized(state);

            var currentWorld = string.IsNullOrWhiteSpace(worldAddress) ? ResolveCurrentWorldAddress(state) : worldAddress;

            state.EndlessReplayEnabled = true;
            state.AwaitingNextNightPortal = false;
            state.ReplayRunNumber = Math.Max(1, state.ReplayRunNumber) + 1;
            state.ReplayRunSeed = NextSeed(state.ReplayRunSeed, state.ReplayRunNumber);
            ExpandEnemyDeckForNextRun(state);
            state.CompletedEncounterIds.Clear();
            state.Flags.Clear();
            state.Inventory.Clear();
            state.ActiveDuelTableId = null;
            state.CurrentWorldSceneAddress = currentWorld;
            state.PlayerPosition = playerPosition ?? Vector3.zero;
            state.PlayerRotation = playerRotation ?? Quaternion.identity;

            EscapeQuestState.Reset();
            await stateService.SaveAsync();

            var director = GlobalServices.Director;
            if (director?.CurrentMode is ExplorationMode)
            {
                await director.PopModeAsync();
                await director.PushModeAsync(new ExplorationMode(currentWorld));
            }

            Debug.Log($"[EndlessReplayLoop] Started replay run {state.ReplayRunNumber} with seed {state.ReplayRunSeed}.");
        }

        private static void EnsureInitialized(PersistentGameState state)
        {
            state.CompletedEncounterIds ??= new();
            state.LifetimeCompletedEncounterIds ??= new();
            state.CollectedCardIds ??= new();
            state.EnemyDeckCardIds ??= new();
            state.Flags ??= new();
            state.Inventory ??= new();

            if (state.ReplayRunNumber < 1)
                state.ReplayRunNumber = 1;

            if (state.ReplayRunSeed == 0)
                state.ReplayRunSeed = NextSeed(Environment.TickCount, state.ReplayRunNumber);
        }


        private static void ExpandEnemyDeckForNextRun(PersistentGameState state)
        {
            if (state == null) return;
            state.EnemyDeckCardIds ??= new List<string>();

            var candidates = CardDatabase.AllCards
                .Where(card => card != null && !string.IsNullOrEmpty(card.CardName) && card.Type != CardType.Town)
                .ToList();
            if (candidates.Count == 0) return;

            var rng = new System.Random(GetStableSeed($"enemy-next-night:{state.ReplayRunNumber}"));
            var chosen = candidates.OrderBy(_ => rng.Next()).FirstOrDefault();
            if (chosen == null) return;

            state.EnemyDeckCardIds.Add(chosen.CardName);
            Debug.Log($"[EndlessReplayLoop] Enemy deck expanded for night {state.ReplayRunNumber}: {chosen.CardName}.");
        }

        private static string ResolveCurrentWorldAddress(PersistentGameState state)
        {
            if (GlobalServices.Director?.CurrentMode is ExplorationMode exploration && !string.IsNullOrWhiteSpace(exploration.CurrentWorldAddress))
                return exploration.CurrentWorldAddress;

            if (!string.IsNullOrWhiteSpace(state.CurrentWorldSceneAddress))
                return state.CurrentWorldSceneAddress;

            return DefaultWorldAddress;
        }

        private static int NextSeed(int previousSeed, int runNumber)
        {
            unchecked
            {
                var seed = previousSeed;
                if (seed == 0) seed = Environment.TickCount;
                seed = (seed * 1103515245) + 12345 + (runNumber * 1009);
                return seed == 0 ? 1 : seed;
            }
        }
    }
}
