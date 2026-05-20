using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public class GameStateService : IGameStateService
    {
        private readonly ISaveSystem _saveSystem;
        private const string StateFileName = "gamestate.json";

        public PersistentGameState State { get; private set; }

        public GameStateService(ISaveSystem saveSystem)
        {
            _saveSystem = saveSystem;
        }

        public async UniTask LoadAsync()
        {
            var data = await _saveSystem.LoadAsync(StateFileName);
            if (data != null)
            {
                string json = System.Text.Encoding.UTF8.GetString(data);
                State = JsonUtility.FromJson<PersistentGameState>(json);
            }
            State ??= new PersistentGameState();
        }

        public async UniTask SaveAsync()
        {
            string json = JsonUtility.ToJson(State);
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
            await _saveSystem.SaveAsync(StateFileName, data);
        }
    }
}