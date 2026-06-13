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
using Shared.Localization;

namespace Exploration
{
    public class EncounterPoint : MonoBehaviour
    {
        public string EncounterId;
        public string UniqueTableId;

        [Header("Fallback Deck")]
        [SerializeField] private DeckData DefaultPlayerDeck;

        [Header("World Table")]
        [SerializeField] private GameObject _worldTableVisual;

        [Header("Prompt")]
        [SerializeField] private string _promptKey = "interaction.start_duel";
        [SerializeField] private string _promptText = "Press [E] to start duel";

        [Header("Camera Seat")]
        [SerializeField] private Transform _cameraSeat;

        public bool CanStartDuel => !IsEncounterCompleted() && !IsDuelModeActive();

        public bool CanShowPrompt => CanStartDuel && !IsExplorationTutorialBlockingDuelStart();

        public string PromptText => CanShowPrompt ? LocalizationService.T(_promptKey, _promptText) : string.Empty;


        public void ConfigureRuntime(string encounterId, string uniqueTableId, DeckData defaultPlayerDeck, Transform cameraSeat, GameObject worldTableVisual = null, string promptKey = "interaction.start_mod_duel", string promptText = "Press [E] to challenge the modded cult table")
        {
            EncounterId = encounterId;
            UniqueTableId = uniqueTableId;
            DefaultPlayerDeck = defaultPlayerDeck;
            _cameraSeat = cameraSeat;
            _worldTableVisual = worldTableVisual;
            _promptKey = promptKey;
            _promptText = promptText;
        }

        private bool IsEncounterCompleted()
        {
            var completedEncounterIds = GlobalServices.GameStateService?.State?.CompletedEncounterIds;
            return completedEncounterIds != null &&
                   !string.IsNullOrEmpty(EncounterId) &&
                   completedEncounterIds.Contains(EncounterId);
        }

        private static bool IsDuelModeActive()
        {
            return GlobalServices.Director?.CurrentMode is DuelManager;
        }

        private static bool IsExplorationTutorialBlockingDuelStart()
        {
            var tutorial = FindFirstObjectByType<MovementTutorial>(FindObjectsInactive.Include);
            return tutorial != null && tutorial.BlocksDuelStart;
        }

        public async UniTask StartDuelAsync(bool instant = false)
        {
            if (!CanStartDuel)
            {
                Debug.Log($"[EncounterPoint] Encounter '{EncounterId}' cannot start because it is completed or another duel is active. Ignoring duel start.");
                return;
            }

            if (!instant && IsExplorationTutorialBlockingDuelStart())
            {
                Debug.Log("[EncounterPoint] Exploration tutorial is active. Blocking duel start until onboarding is complete.");
                return;
            }

            Debug.Log($"[EncounterPoint] Starting duel at table {UniqueTableId}");

            var player = FindAnyObjectByType<ExplorationController>();
            if (player != null) player.Deactivate();

            if (!instant)
                SceneTransitionManager.Instance.SaveCameraState();

            var encounter = EncounterDatabase.GetEncounter(EncounterId);
            if (encounter == null)
            {
                Debug.LogError($"[EncounterPoint] Encounter '{EncounterId}' not found in database.");
                return;
            }

            if (encounter.DuelScene == null || !encounter.DuelScene.RuntimeKeyIsValid())
            {
                Debug.LogError($"[EncounterPoint] Encounter '{EncounterId}' has no valid DuelScene.");
                return;
            }

            var cameraSeat = _cameraSeat != null ? _cameraSeat : transform;
            if (_cameraSeat == null)
                Debug.LogWarning($"[EncounterPoint] Encounter '{EncounterId}' has no camera seat assigned. Falling back to encounter transform.", this);

            var loadHandle = encounter.DuelScene.LoadSceneAsync(LoadSceneMode.Additive);
            var duelLoadUniTask = loadHandle.Task.AsUniTask();

            if (instant)
            {
                var mainCam = Camera.main;
                if (mainCam != null)
                    mainCam.enabled = false;

                await duelLoadUniTask;
                HideWorldTableVisualForLoadedDuel();
                
                if (mainCam != null)
                    mainCam.enabled = true;
                if (mainCam != null && cameraSeat != null)
                {
                    mainCam.transform.position = cameraSeat.position;
                    mainCam.transform.rotation = cameraSeat.rotation;
                }
            }
            else
            {
                var camMoveTask = SceneTransitionManager.Instance.MoveCameraToTransform(cameraSeat, 1.0f);
                await duelLoadUniTask;
                HideWorldTableVisualForLoadedDuel();
                await camMoveTask;
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

            var director = GlobalServices.Director;

            var context = new DuelStartContext
            {
                Encounter = encounter,
                PlayerDeck = playerDeck,
                PlayerPersistentDeck = DefaultPlayerDeck,
                TableId = UniqueTableId,
                SavedMatchJson = savedJson,
                DuelSceneHandle = loadHandle,
                Director = director
            };

            await director.PushModeAsync(duelManager, context);
        }

        private void HideWorldTableVisualForLoadedDuel()
        {
            if (_worldTableVisual != null && _worldTableVisual != gameObject)
                _worldTableVisual.SetActive(false);
        }

        private UniTask<List<CardDef>> GetPlayerDeckAsync()
        {
            if (GlobalServices.PlayerData?.ActiveDeckCardIds?.Count > 0)
            {
                var deck = new List<CardDef>();
                foreach (var cardId in GlobalServices.PlayerData.ActiveDeckCardIds)
                {
                    var cardDef = CardDatabase.GetCard(cardId);
                    if (cardDef != null) deck.Add(cardDef);
                }
                if (deck.Count > 0) return deck;
            }
            var fallbackDeck = ResolveDeckCards(DefaultPlayerDeck);
            if (fallbackDeck.Count > 0)
            {
                Debug.LogWarning($"No player data found - returning fallback deck asset '{DefaultPlayerDeck.name}'.");
                return fallbackDeck;
            }

            var builtInFallback = new List<CardDef>();
            foreach (var cardId in new[] { "Town", "Human", "Human", "Building", "Vampire" })
            {
                var cardDef = CardDatabase.GetCard(cardId);
                if (cardDef != null) builtInFallback.Add(cardDef);
            }

            if (builtInFallback.Count > 0)
            {
                Debug.LogWarning($"No player data or fallback deck asset found for encounter '{EncounterId}' - using built-in starter card ids.");
                return builtInFallback;
            }

            Debug.LogError($"[EncounterPoint] No player deck, fallback deck, or built-in fallback cards are available for encounter '{EncounterId}'.");
            return new List<CardDef>();

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

        private static List<CardDef> ResolveDeckCards(DeckData deckData)
        {
            var result = new List<CardDef>();
            if (deckData == null) return result;

            if (deckData.Cards != null)
            {
                foreach (var card in deckData.Cards)
                {
                    if (card != null) result.Add(card);
                }
            }

            if (result.Count > 0) return result;

            if (deckData.CardNames == null) return result;
            foreach (var cardId in deckData.CardNames)
            {
                var cardDef = CardDatabase.GetCard(cardId);
                if (cardDef != null) result.Add(cardDef);
            }

            if (deckData.Cards != null)
            {
                deckData.Cards.Clear();
                deckData.Cards.AddRange(result);
            }

            return result;
        }

        void OnEnable()
        {
            try
            {
                GlobalServices.EventBus?.Subscribe<DuelEndedEvent>(OnDuelEnded);
            }
            catch (System.Exception)
            {
                // EventBus not initialized yet, skip subscription
            }
        }

        void OnDisable()
        {
            try
            {
                GlobalServices.EventBus?.Unsubscribe<DuelEndedEvent>(OnDuelEnded);
            }
            catch (System.Exception)
            {
                // EventBus not available, skip unsubscription
            }
        }

        void OnDuelEnded(DuelEndedEvent e)
        {
            if (_worldTableVisual != null && !_worldTableVisual.activeSelf)
                _worldTableVisual.SetActive(true);
        }
    }
}