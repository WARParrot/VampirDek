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
using Combat.UI;

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

        public async UniTask StartDuelAsync(bool instant = false)
        {
            Debug.Log($"[EncounterPoint] Starting duel at table {UniqueTableId}");

            var player = FindAnyObjectByType<ExplorationController>();
            if (player != null) player.Deactivate();

            if (_worldTableVisual != null)
                _worldTableVisual.SetActive(false);

            if (!instant)
                SceneTransitionManager.Instance.SaveCameraState();

            var loadHandle = Addressables.LoadSceneAsync(Encounter.DuelScene, LoadSceneMode.Additive);
            var duelLoadUniTask = loadHandle.Task.AsUniTask();

            if (instant)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                    mainCam.enabled = false;

                await duelLoadUniTask;
                
                if (mainCam != null)
                    mainCam.enabled = true;
                if (mainCam != null && _cameraSeat != null)
                {
                    mainCam.transform.position = _cameraSeat.position;
                    mainCam.transform.rotation = _cameraSeat.rotation;
                }
            }
            else
            {
                var camMoveTask = SceneTransitionManager.Instance.MoveCameraToTransform(_cameraSeat, 1.0f);
                await UniTask.WhenAll(duelLoadUniTask, camMoveTask);
            }

            var switcher = Camera.main?.GetComponent<DuelCameraSwitcher>();
            if (switcher != null)
            {
                var seat = GameObject.Find("SeatView")?.transform;
                var overhead = GameObject.Find("OverheadView")?.transform;
                if (seat != null && overhead != null)
                {
                    switcher.SeatView = seat;
                    switcher.OverheadView = overhead;
                    switcher.enabled = true;
                }
                else
                    Debug.LogWarning("[EncounterPoint] Could not find SeatView or OverheadView in the duel scene.");
            }

            var playerDeck = await GetPlayerDeckAsync();

            string savedJson = GlobalServices.SaveSystem.LoadActiveBattleJson(UniqueTableId);

            var duelGO = new GameObject("DuelManager");
            var duelManager = duelGO.AddComponent<DuelManager>();
            DuelManagerProxy.Instance = duelManager;

            var context = new DuelStartContext
            {
                Encounter = Encounter,
                PlayerDeck = playerDeck,
                PlayerPersistentDeck = DefaultPlayerDeck,
                TableId = UniqueTableId,
                SavedMatchJson = savedJson,
                DuelSceneHandle = loadHandle
            };

            await GlobalServices.Director.PushModeAsync(duelManager, context);
        }

        private async UniTask<List<CardDef>> GetPlayerDeckAsync()
        {
            if (GlobalServices.PlayerData?.ActiveDeckCardIds?.Count > 0)
            {
                var deck = new List<CardDef>();
                foreach (var cardId in GlobalServices.PlayerData.ActiveDeckCardIds)
                {
                    var cardDef = await CardDatabase.GetCardAsync(cardId);
                    if (cardDef != null) deck.Add(cardDef);
                }
                if (deck.Count > 0) return deck;
            }
            Debug.LogWarning("No player data found - returning fallback deck.");
            return new List<CardDef>(DefaultPlayerDeck.Cards);

           /* string json = System.Text.Encoding.UTF8.GetString(dataBytes);
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
            return deck;*/
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