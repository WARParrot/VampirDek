using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Core
{
    public class GameDirector
    {
        private readonly Stack<IGameMode> _modeStack = new();

        public IGameMode CurrentMode => _modeStack.Count > 0 ? _modeStack.Peek() : null;

        public async UniTask PushModeAsync(IGameMode mode, object context = null)
        {
            if (_modeStack.Count > 0)
            {
                var current = _modeStack.Peek();
                if (current != null)
                    await current.OnPauseAsync();
            }
            _modeStack.Push(mode);
            await mode.EnterAsync(context);
        }

        public async UniTask PopModeAsync()
        {
            if (_modeStack.Count == 0) return;
            var exiting = _modeStack.Pop();
            await exiting.ExitAsync();
            if (_modeStack.Count > 0)
            {
                var resumed = _modeStack.Peek();
                await resumed.OnResumeAsync();
            }
        }
    }
}
