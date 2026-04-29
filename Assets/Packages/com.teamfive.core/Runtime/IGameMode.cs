using Cysharp.Threading.Tasks;

namespace Core
{
    public interface IGameMode
    {
        UniTask EnterAsync(object context);
        UniTask ExitAsync();
        UniTask OnPauseAsync();
        UniTask OnResumeAsync();
    }
}
