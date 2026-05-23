using Cysharp.Threading.Tasks;

namespace Core
{
    public interface IGameStateService
    {
        PersistentGameState State { get; }
        UniTask LoadAsync();
        UniTask SaveAsync();
    }
}