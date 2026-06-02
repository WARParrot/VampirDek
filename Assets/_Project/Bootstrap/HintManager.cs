using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Definitions;
using Combat;
using UnityEngine;

namespace Core
{
    public class HintManager : IDisposable
    {
        private HintUI _hintUI;
        private readonly GameDirector _gameDirector;
        private readonly GameSettings _gameSettings;

        private readonly Queue<HintData> _hintQueue = new();
        private readonly HashSet<string> _shownHints = new();
        private readonly Dictionary<string, List<HintData>> _encounterHints = new();
        private readonly float _minIntervalBetweenHints = 2f;
        private bool _isShowing = false;

        public HintManager(GameDirector gameDirector, GameSettings gameSettings)
        {
            _gameDirector = gameDirector;
            _gameSettings = gameSettings;
        }

        public void SetHintUI(HintUI hintUI)
        {
            _hintUI = hintUI;
        }

        public void Initialize()
        {
            LoadShownHints();
            GlobalServices.EventBus.Subscribe<HintEvent>(OnHintEvent);
            GlobalServices.EventBus.Subscribe<DuelStartedEvent>(OnDuelStarted);
            DuelEndedSignal.OnDuelEnded += OnDuelEnded;
        }

        public void LoadEncounterHints(CombatEncounter encounter)
        {
            if (encounter == null) return;
            //if (encounter.Hints == null || encounter.Hints.Count == 0) return;
            if (string.IsNullOrEmpty(encounter.EncounterId))
            {
                Debug.LogWarning("[HintManager] EncounterId is null or empty - skipping hints.");
                return;
            }

            _encounterHints[encounter.EncounterId] = new List<HintData>();
        }

        private void OnDuelStarted(DuelStartedEvent e)
        {
            if (e.Encounter == null) return;
            LoadEncounterHints(e.Encounter);
        }

        private void OnDuelEnded()
        {
            _encounterHints.Clear();
        }

        private void OnHintEvent(HintEvent evt)
        {
            Debug.Log($"[HintManager] Received event: {evt.Tag}, Mode: {evt.Mode}, Context: {evt.Context != null}");

            GameMode currentMode = evt.Mode;

            var candidates = new List<HintData>(_gameSettings.GlobalHints ?? new List<HintData>());

            if (evt.Mode == GameMode.Combat && _encounterHints.Count == 0)
            {
                var duelManager = _gameDirector.CurrentMode as DuelManager;
                if (duelManager?.CurrentDuelState?.Encounter != null)
                {
                    LoadEncounterHints(duelManager.CurrentDuelState.Encounter);
                }
            }

            foreach (var list in _encounterHints.Values)
                candidates.AddRange(list);

            Debug.Log($"[HintManager] Candidates total: {candidates.Count}");

            foreach (var hint in candidates)
            {
                if (hint.EventTag != evt.Tag) continue;
                if (_shownHints.Contains(hint.name)) continue;
                if (hint.AllowedMode != GameMode.Both && hint.AllowedMode != currentMode) continue;
                if (hint.Condition != null && !hint.Condition.IsMet(evt.Context)) continue;

                Debug.Log($"[HintManager] Queueing hint: {hint.name}");
                _hintQueue.Enqueue(hint);
                _shownHints.Add(hint.name);
                if (hint.ShownOncePerGame) SaveShownHints();
            }

            if (!_isShowing)
                ProcessQueueAsync().Forget();
        }

        private async UniTaskVoid ProcessQueueAsync()
        {
            _isShowing = true;
            while (_hintQueue.Count > 0)
            {
                var hint = _hintQueue.Dequeue();
                if (_hintUI != null)
                {
                    await _hintUI.ShowAsync(hint.Message);
                }
                if (_hintQueue.Count > 0)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(_minIntervalBetweenHints));
            }
            _isShowing = false;
        }

        private void LoadShownHints()
        {
            try
            {
                var json = PlayerPrefs.GetString("shown_hints", "{\"Ids\":[]}");
                var list = JsonUtility.FromJson<HintIdList>(json);
                if (list?.Ids != null)
                    _shownHints.UnionWith(list.Ids);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HintManager] Failed to load shown hints: {ex.Message}. Resetting.");
                PlayerPrefs.DeleteKey("shown_hints");
            }
        }

        private void SaveShownHints()
        {
            var list = new HintIdList { Ids = new List<string>(_shownHints) };
            var json = JsonUtility.ToJson(list);
            PlayerPrefs.SetString("shown_hints", json);
            PlayerPrefs.Save();
        }

        public void Dispose()
        {
            DuelEndedSignal.OnDuelEnded -= OnDuelEnded;
            GlobalServices.EventBus.Unsubscribe<HintEvent>(OnHintEvent);
            GlobalServices.EventBus.Unsubscribe<DuelStartedEvent>(OnDuelStarted);
        }

        [Serializable]
        private class HintIdList
        {
            public List<string> Ids;
        }
    }
}