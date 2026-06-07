using Cysharp.Threading.Tasks;

namespace Core
{
    public interface ISaveSystem
    {
        UniTask SaveAsync(string fileName, byte[] data);
        UniTask<byte[]> LoadAsync(string fileName);
        bool Exists(string fileName);
        void Delete(string fileName);
        void SaveActiveBattle(string tableId, string matchDataJson);
        string LoadActiveBattleJson(string tableId);
        void ClearActiveBattle(string tableId);
        string LoadJson(string fileName);
    }
}
