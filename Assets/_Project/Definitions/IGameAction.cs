using Cysharp.Threading.Tasks;

namespace Definitions
{
    public interface IGameAction
    {
        UniTask ExecuteAsync();
        string Description { get; }
    }
}
