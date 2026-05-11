using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using Definitions;
using Core;
using Combat;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Exploration
{
    public class EncounterPoint : MonoBehaviour
    {
        public CombatEncounter Encounter;
        public string UniqueTableId;

        [Header("Fallback Deck")]
        [SerializeField] private DeckData DefaultPlayerDeck;

        [Header("World Table")]
        [SerializeField] private GameObject _worldTableVisual;

        [Header("Camera Seat")]
        [SerializeField] private Transform _cameraSeat;

        public async UniTask StartDuelAsync()
        {
            Debug.Log($"[EncounterPoint] Starting duel at table {UniqueTableId}");

            var player = FindAnyObjectByType<ExplorationController>();
            if (player != null) player.Deactivate();

            if (_worldTableVisual != null)
                _worldTableVisual.SetActive(false);

            SceneTransitionManager.Instance.SaveCameraState();

            var loadHandle = Addressables.LoadSceneAsync(Encounter.DuelScene, LoadSceneMode.Additive);
            var duelLoadUniTask = loadHandle.Task.AsUniTask();

            var camMoveTask = SceneTransitionManager.Instance.MoveCameraToTransform(_cameraSeat, 1.0f);

            await UniTask.WhenAll(duelLoadUniTask, camMoveTask);

            var playerDeck = await GetPlayerDeckAsync();

            string savedJson = GlobalServices.SaveSystem.LoadActiveBattleJson(UniqueTableId);

            var duelGO = new GameObject("DuelManager");
            var duelManager = duelGO.AddComponent<DuelManager>();
            DuelManagerProxy.Instance = duelManager;

            var context = new DuelStartContext
            {
                Encounter = Encounter,
                PlayerDeck = playerDeck,
                TableId = UniqueTableId,
                SavedMatchJson = savedJson,
                DuelSceneHandle = loadHandle
            };

            await GlobalServices.Director.PushModeAsync(duelManager, context);
        }

        private async UniTask<List<CardDef>> GetPlayerDeckAsync()
        {
            var dataBytes = await GlobalServices.SaveSystem.LoadAsync("playerdata.json");
            if (dataBytes == null)
            {
                Debug.LogWarning("No player data found - returning fallback deck.");
                return new List<CardDef>(DefaultPlayerDeck.Cards);
            }

            string json = System.Text.Encoding.UTF8.GetString(dataBytes);
            var playerData = JsonUtility.FromJson<PersistentPlayerData>(json);
            if (playerData == null || playerData.ActiveDeckCardIds == null)
                return new List<CardDef>(DefaultPlayerDeck.Cards);

            var deck = new List<CardDef>();
            foreach (var cardId in playerData.ActiveDeckCardIds)
            {
                var cardDef = await CardDatabase.GetCardAsync(cardId);
                if (cardDef != null)
                    deck.Add(cardDef);
                else
                    Debug.LogWarning($"Card '{cardId}' not found in database.");
            }
            return deck;
        }

        void OnEnable() => GlobalServices.EventBus.Subscribe<DuelEndedEvent>(OnDuelEnded);
        void OnDisable() => GlobalServices.EventBus.Unsubscribe<DuelEndedEvent>(OnDuelEnded);

        void OnDuelEnded(DuelEndedEvent e)
        {
            if (_worldTableVisual != null && !_worldTableVisual.activeSelf)
                _worldTableVisual.SetActive(true);
        }
    }
}