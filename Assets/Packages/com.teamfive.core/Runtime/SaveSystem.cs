using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    public class SaveSystem : ISaveSystem
    {
        private string GetPath(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

        public async UniTask SaveAsync(string fileName, byte[] data)
        {
            var path = GetPath(fileName);
            await File.WriteAllBytesAsync(path, data);
        }

        public async UniTask<byte[]> LoadAsync(string fileName)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path)) return null;
            return await File.ReadAllBytesAsync(path);
        }

        public bool Exists(string fileName) => File.Exists(GetPath(fileName));
        public void Delete(string fileName) => File.Delete(GetPath(fileName));
        
        public void SaveActiveBattle(string tableId, string matchDataJson)
        {
            var fileName = $"battle_{tableId}.json";
            File.WriteAllText(GetPath(fileName), matchDataJson);
        }

        public string LoadActiveBattleJson(string tableId)
        {
            var fileName = $"battle_{tableId}.json";
            var path = GetPath(fileName);
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }

        public string LoadJson(string fileName)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }
    }
}